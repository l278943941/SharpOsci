using SDL3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static SDL3.SDL;
using static System.Windows.Forms.DataFormats;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SharpOsci
{
    public class sdlRander : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "SetParent")]
        private static extern int SetParent(IntPtr chilshwnd, IntPtr Parenthwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(nint hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        private const int GWL_STYLE = -16;
        private const uint WS_CHILD = 0x40000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_SYSMENU = 0x00080000;
        private const int SWP_NOZORDER = 0x0004;

        private const string SDL_PROP_WINDOW_WIN32_PARENT_HWND = "SDL_PROP_WINDOW_CREATE_WIN32_HWND_POINTER";
        private const string SDL_PROP_WINDOW_WIN32_HWND = "SDL_PROP_WINDOW_CREATE_WIN32_HWND_POINTER";

        private Control _parent;
        private nint _window;      // SDL3 窗口
        private nint _renderer;   // SDL3 渲染器
        private nint _gpuDevice; //gpu设备

        private nint globalParams;//gpu全局参数

        private nint _pointBuffer;             // gpu点缓冲区
        private nint _computePipeline;    // 计算管线
        private nint _computeTexture;     // 计算输出纹理
        private nint _vertexBuffer;             // 全屏四边形顶点缓冲区
        private nint _graphicsPipeline;      // 全屏渲染管线
        private nint _sampler;                     // 纹理采样器
        //绑定到计算管线和片段管线的纹理
        GPUStorageTextureReadWriteBinding[] computeTextureBindings;
        //绑定到计算管线的缓冲区
        SDL.GPUStorageBufferReadWriteBinding[] computerBufferBindings;

        uint VulkanInstanceExtensions; // Vulkan实例扩展
        FunctionPointer VulkanFunctionPointer; // Vulkan函数指针

        nint Vulkansurface;// Vulkan表面

        [StructLayout(LayoutKind.Sequential)]
        public struct GlobalParams
        {
            public Vector4 color;
            public float pointSize;
            public float intensity;
            public int Sample; //采样位深

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PositionTextureVertex
        {
            public float X;  // 位置 X
            public float Y;  // 位置 Y
            public float Z;  // 位置 Z（2D 渲染通常设为0）
            public float U;  // 纹理坐标 U
            public float V;  // 纹理坐标 V

            public PositionTextureVertex(float x, float y, float z, float u, float v)
            {
                X = x;
                Y = y;
                Z = z;
                U = u;
                V = v;
            }
        }

        public static class Win32Helper
        {
            // Windows 平台的结构体定义
            [StructLayout(LayoutKind.Sequential)]
            public struct SDL_SysWMinfo
            {
                public IntPtr hwnd;     // 窗口句柄
                public IntPtr hdc;      // 设备上下文
                public int hinstance;   // 实例句柄
            }

            // 获取控件的原生窗口信息
            public static SDL_SysWMinfo GetWindowInfo(Control control)
            {
                var info = new SDL_SysWMinfo();
                info.hwnd = control.Handle;
                return info;
            }
        }




        public sdlRander(Control parent)
        {
            _parent = parent;
            if (!SDL.Init(SDL.InitFlags.Video))
            {
                throw new Exception("SDL_Init Error: " + SDL.GetError());
            }
            _gpuDevice = SDL.CreateGPUDevice(GPUShaderFormat.SPIRV , true, null);

            var props = SDL.CreateProperties();
            var winInfo = Win32Helper.GetWindowInfo(_parent);

            SDL.SetNumberProperty(props, SDL_PROP_WINDOW_WIN32_PARENT_HWND, winInfo.hwnd);
            SDL.SetNumberProperty(props, "SDL_PROPERTY_WINDOW_CREATE_WIDTH_NUMBER", parent.Width);
            SDL.SetNumberProperty(props, "SDL_PROPERTY_WINDOW_CREATE_HEIGHT_NUMBER", parent.Height);

            //// 子窗口必要样式
            SDL.SetBooleanProperty(props, "SDL_PROPERTY_WINDOW_CREATE_RESIZABLE_BOOLEAN", false);   // 允许调整大小
            SetBooleanProperty(props, "SDL_PROPERTY_WINDOW_CREATE_BORDERLESS_BOOLEAN", true); // 无边框
            _window = SDL.CreateWindowWithProperties(props);


            SDL.ClaimWindowForGPUDevice(_gpuDevice, _window);

            //SDL.SetWindowBordered(_window, true); // 去掉边框
            SDL.SetWindowTitle(_window, "SDL3 Window"); // 设置窗口标题
            IntPtr p = FindWindow(null, "SDL3 Window");
            SDL.DestroyProperties(props); // 释放属性对象



            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetWindowLongPtr(p, GWL_STYLE, (nint)(WS_VISIBLE));
                SetWindowPos(p, IntPtr.Zero, 0, 0, _parent.Width, _parent.Height, SWP_NOZORDER);
            }


            //VulkanGetInstanceExtensions(out  VulkanInstanceExtensions);
            //FunctionPointer VulkanFunctionPointer = VulkanGetVkGetInstanceProcAddr();

            //VulkanCreateSurface(_window,  (nint)VulkanInstanceExtensions, 0, out nint Vulkansurface);

            _renderer = SDL.CreateRenderer(_window, null);
            SetParent(p, winInfo.hwnd);

           
            //      初始化渲染管线
            InitializeComputeResources();
            // 准备渲染资源
            bindParames(500, 500);

            RenderFrame();
        }

        private void UpdateGlobalParams()
        {

            //this._pointBuffer = SDL.CreateGPUBuffer(_gpuDevice, new SDL.GPUBufferCreateInfo
            //{
            //    Usage = SDL.GPUBufferUsageFlags.ComputeStorageRead | SDL.GPUBufferUsageFlags.ComputeStorageWrite | GPUBufferUsageFlags.Vertex,
            //    Size = (uint)(sizeof(float) * 200 * 2),
            //    Props = 0
            //});

            //this.globalParams = SDL.CreateGPUBuffer(
            //_gpuDevice,
            //new SDL.GPUBufferCreateInfo
            //{
            //    Usage = SDL.GPUBufferUsageFlags.ComputeStorageWrite | SDL.GPUBufferUsageFlags.ComputeStorageRead | GPUBufferUsageFlags.Vertex, // 明确用途为Uniform Buffer
            //    Size = (uint)Marshal.SizeOf<GlobalParams>(),
            //    Props = 0
            //}
            //);

            this._computeTexture = SDL.CreateGPUTexture(
               _gpuDevice,
              new SDL.GPUTextureCreateInfo
              {
                  Type = SDL.GPUTextureType.Texturetype2D,
                  Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
                  Usage = SDL.GPUTextureUsageFlags.ComputeStorageWrite | GPUTextureUsageFlags.ComputeStorageRead,
                  Width = (uint)500,
                  Height = (uint)500,
                  LayerCountOrDepth = 1,
                  NumLevels = 1,
                  SampleCount = GPUSampleCount.SampleCount1,
              }
            );

            ////绑定texttrue到计算管线
            computeTextureBindings = new SDL.GPUStorageTextureReadWriteBinding[]
            {
                new SDL.GPUStorageTextureReadWriteBinding
                {
                    Texture =this. _computeTexture,
                    MipLevel=0,
                    Layer=0
                }
            };
            if (_computeTexture == IntPtr.Zero)
            {
                throw new Exception($"ComputeTexture 创建失败: {SDL.GetError()}");
            }

            //computerBufferBindings = new SDL.GPUStorageBufferReadWriteBinding[]
            //{
            //    new SDL.GPUStorageBufferReadWriteBinding
            //     {
            //         Buffer = this.globalParams

            //     },
            //     new SDL.GPUStorageBufferReadWriteBinding
            //     {
            //         Buffer = this._pointBuffer

            //     }
            //};
            //if (this._pointBuffer == IntPtr.Zero)
            //{
            //    throw new Exception($"PointBuffer 创建失败: {SDL.GetError()}");
            //}


            //if (this.globalParams == IntPtr.Zero)
            //{
            //    throw new Exception($"GlobalParams Buffer 创建失败: {SDL.GetError()}");
            //}


            nint computeCmdBuf = SDL.AcquireGPUCommandBuffer(_gpuDevice);


            var computePass = SDL.BeginGPUComputePass(computeCmdBuf, computeTextureBindings, (uint)1, null, 0);
            SDL.BindGPUComputePipeline(computePass, _computePipeline);

            SDL.DispatchGPUCompute(computePass, 8, 8, 1); // 调用计算着色器

            SDL.EndGPUComputePass(computePass);
            SDL.SubmitGPUCommandBuffer(computeCmdBuf);


        }

        private void bindParames(int width, int height) {





        }


        public void RenderFrame()
        {
            // 清空屏幕为黑色
            SDL.SetRenderDrawColor(Vulkansurface, 0, 0, 0, 255);
            SDL.RenderClear(Vulkansurface);

            Random p = new Random();
            // 绘制绿色线
            SDL.SetRenderDrawColor(Vulkansurface, 0, (byte)p.Next(0, 255), 0, 255);
            SDL.RenderLine(Vulkansurface, 0, 0, (byte)p.Next(0, 255), (byte)p.Next(0, 255));

            // 提交渲染
            SDL.RenderPresent(Vulkansurface);


            var parameters = new GlobalParams
            {
                color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f), // 纯红色
                pointSize = 0.0f, // 暂时设为0，关闭点绘制
                intensity = 1.0f,
                Sample = 3
            };
            UpdateGlobalParams();
        }

        // 调整窗口尺寸
        public void Resize(int width, int height)
        {
            SDL.SetWindowSize(_window, width, height);
            if (_computeTexture != 0)
            {
                SDL.ReleaseGPUTexture(_gpuDevice, _computeTexture);
                _computeTexture = 0;
            }
            bindParames(width, height);

            RenderFrame();
        }
        /// <summary>
        /// 设置点缓冲区
        /// </summary>
        /// <param name="bufflen"></param> bufflen-点缓冲区大小 根据音乐的采样率/帧数*位深(1:8位 2:16位 3:24位)*2(2是音乐的声道暂时不考虑单声道)
        public void play(int bufflen)
        {
            var gpubugger = new SDL.GPUBufferCreateInfo
            {
                Usage = SDL.GPUBufferUsageFlags.ComputeStorageWrite | SDL.GPUBufferUsageFlags.ComputeStorageRead | GPUBufferUsageFlags.Vertex,
                Size = (uint)bufflen,
                Props = 0
            };
            if (_pointBuffer != 0)
                SDL.ReleaseGPUBuffer(_gpuDevice, _pointBuffer);
            this._pointBuffer = SDL.CreateGPUBuffer(_gpuDevice, gpubugger);
        }

        public void Dispose()
        {
            if (_renderer != 0)
                SDL.DestroyRenderer(_renderer);
            if (_window != 0)
                SDL.DestroyWindow(_window);
            SDL.ReleaseGPUBuffer(_gpuDevice, _pointBuffer);
            SDL.ReleaseWindowFromGPUDevice(_gpuDevice, _window);
            SDL.Quit();
        }



        private void InitializeComputeResources( )
        {
            // 1. 创建计算管线
            byte[] computeShaderSpv = File.ReadAllBytes("Oscilloscope.comp.spv");
            GCHandle computeShaderHandle = GCHandle.Alloc(computeShaderSpv, GCHandleType.Pinned);
            nint computeShaderPtr = computeShaderHandle.AddrOfPinnedObject();
            UIntPtr computeShaderSize = (UIntPtr)computeShaderSpv.Length;
            nint computeShaderEntrypoint = Marshal.StringToHGlobalAnsi("main");

            _computePipeline = SDL.CreateGPUComputePipeline(
                _gpuDevice,
                new SDL.GPUComputePipelineCreateInfo
                {
                    CodeSize = computeShaderSize,
                    Code = computeShaderPtr,
                    Entrypoint = computeShaderEntrypoint,
                    Format = GPUShaderFormat.SPIRV,
                    NumReadwriteStorageTextures = 1,// 绑定1个可读写存储纹理
                    //NumReadwriteStorageBuffers = 0, // 绑定0个可读写存储缓冲区
                    ThreadcountX = 8,
                    ThreadcountY = 8,
                    ThreadcountZ = 1,
                }
            );
            computeShaderHandle.Free();

            if (_computePipeline == 0)
            {
                throw new Exception("计算管线创建失败: " + SDL.GetError());
            }

            //// 3. 创建全屏四边形顶点缓冲区
            //_vertexBuffer = SDL.CreateGPUBuffer(
            //    _gpuDevice,
            //    new SDL.GPUBufferCreateInfo
            //    {
            //        Usage = SDL.GPUBufferUsageFlags.Vertex,
            //        Size = (uint)(6 * Marshal.SizeOf<PositionTextureVertex>()),
            //        Props=0
            //    }
            //);

            //// 上传顶点数据（全屏四边形）
            //nint transferBuffer = SDL.CreateGPUTransferBuffer(
            //    _gpuDevice,
            //    new SDL.GPUTransferBufferCreateInfo
            //    {
            //        Usage = SDL.GPUTransferBufferUsage.Upload,
            //        Size = (uint)(6 * Marshal.SizeOf<PositionTextureVertex>()),
            //        Props=0
            //    }
            //);

            //nint mappedPtr = SDL.MapGPUTransferBuffer(_gpuDevice, transferBuffer, cycle: false);
            //PositionTextureVertex[] vertices = new PositionTextureVertex[]
            //{
            //    new PositionTextureVertex(-1.0f, -1.0f, 0.0f, 0.0f, 0.0f),
            //    new PositionTextureVertex(1.0f, -1.0f, 0.0f, 1.0f, 0.0f),
            //    new PositionTextureVertex(1.0f, 1.0f, 0.0f, 1.0f, 1.0f),
            //    new PositionTextureVertex(-1.0f, -1.0f, 0.0f, 0.0f, 0.0f),
            //    new PositionTextureVertex(1.0f, 1.0f, 0.0f, 1.0f, 1.0f),
            //    new PositionTextureVertex(-1.0f, 1.0f, 0.0f, 0.0f, 1.0f)
            //};

            //int vertexSize = Marshal.SizeOf<PositionTextureVertex>();
            //byte[] vertexBytes = new byte[vertices.Length * vertexSize];

            //// 逐个复制结构体到字节数组
            //for (int i = 0; i < vertices.Length; i++)
            //{
            //    IntPtr ptr = Marshal.AllocHGlobal(vertexSize);
            //    Marshal.StructureToPtr(vertices[i], ptr, false);
            //    Marshal.Copy(ptr, vertexBytes, i * vertexSize, vertexSize);
            //    Marshal.FreeHGlobal(ptr);
            //}

            //// 将字节数组复制到映射的GPU内存
            //Marshal.Copy(vertexBytes, 0, mappedPtr, vertexBytes.Length);

            //SDL.UnmapGPUTransferBuffer(_gpuDevice, transferBuffer);


            //var source = new SDL.GPUTransferBufferLocation
            //{
            //    TransferBuffer = transferBuffer,
            //    Offset = 0
            //};

            //var destination = new SDL.GPUBufferRegion
            //{
            //    Buffer = _vertexBuffer, // 目标 Uniform Buffer
            //    Offset = 0,                   // 目标偏移
            //    Size = (uint)Marshal.SizeOf<GlobalParams>() // 数据大小
            //};

            //nint cmdBuf = SDL.AcquireGPUCommandBuffer(_gpuDevice);
            //var copyPass = SDL.BeginGPUCopyPass(cmdBuf);
            //SDL.UploadToGPUBuffer(copyPass,source,destination,true);
            //SDL.EndGPUCopyPass(copyPass);
            //SDL.SubmitGPUCommandBuffer(cmdBuf);
            //SDL.ReleaseGPUTransferBuffer(_gpuDevice, transferBuffer);

            //// 4. 创建全屏渲染管线
            //byte[] vertShaderSpvByte = File.ReadAllBytes("FullscreenQuad.vert.spv");
            //byte[] fragShaderSpvByte = File.ReadAllBytes("FullscreenQuad.frag.spv");



            //GCHandle vertShaderHandle = GCHandle.Alloc(vertShaderSpvByte, GCHandleType.Pinned);
            //GCHandle fragShaderHandle = GCHandle.Alloc(fragShaderSpvByte, GCHandleType.Pinned);

            //var vertShader = SDL.CreateGPUShader(_gpuDevice, new GPUShaderCreateInfo
            //{
            //    CodeSize = (nuint)vertShaderSpvByte.Length * sizeof(byte),
            //    Code = vertShaderHandle.AddrOfPinnedObject(),
            //    Entrypoint = Marshal.StringToHGlobalAnsi("main"),
            //    Format=GPUShaderFormat.SPIRV,
            //    Stage = GPUShaderStage.Vertex,
            //    NumSamplers=0,
            //    NumStorageTextures = 0,
            //    NumStorageBuffers= 0,
            //    NumUniformBuffers=0,
            //    //Props = 0
            //});
            //var fragShader = SDL.CreateGPUShader(_gpuDevice, new SDL.GPUShaderCreateInfo
            //{
            //    Code = fragShaderHandle.AddrOfPinnedObject(), // SPIR-V数据指针
            //    CodeSize = (nuint)fragShaderSpvByte.Length *sizeof(byte),
            //    Format = SDL.GPUShaderFormat.SPIRV,
            //    Stage = SDL.GPUShaderStage.Fragment, // 必须明确阶段
            //    Entrypoint = Marshal.StringToHGlobalAnsi("main"),
            //    NumSamplers = 1, // 关键：片段着色器使用1个采样器
            //    NumStorageTextures = 0,
            //    NumStorageBuffers = 0,
            //    NumUniformBuffers = 0,
            //    //Props = 0
            //});
          

            //var VertexbufferDesc = new GPUVertexBufferDescription
            //{
            //    Slot = 0,                          // 绑定到槽位0
            //    Pitch = (uint)Marshal.SizeOf<PositionTextureVertex>(), // 每个顶点的总字节数 = 20
            //    InputRate = GPUVertexInputRate.Vertex, // 按顶点更新数据（非按实例）
            //    InstanceStepRate = 0                 // 保留字段（必须为0）
            //};
            //var vertexattributes = new GPUVertexAttribute[]
            //{
            //    // 位置属性（Location=0）
            //    new GPUVertexAttribute
            //    {
            //                Location = 0,                   // 对应着色器中的 layout(location=0)
            //                BufferSlot = 0,                 // 使用槽位0的顶点缓冲区
            //                Format = GPUVertexElementFormat.Float3, // 3个float表示位置
            //                Offset = 0                      // 从顶点数据的第0字节开始
            //     },
            //        // UV属性（Location=1）
            //     new GPUVertexAttribute
            //     {
            //                Location = 1,                   // 对应着色器中的 layout(location=1)
            //                BufferSlot = 0,                 // 使用槽位0的顶点缓冲区
            //                Format = GPUVertexElementFormat.Float2, // 2个float表示UV
            //                Offset = 12                     // 位置占12字节，UV从第12字节开始
            //      }
            //  };
            //IntPtr VertexbufferDescPtr = Marshal.AllocHGlobal(Marshal.SizeOf<GPUVertexBufferDescription>());
            //Marshal.StructureToPtr(VertexbufferDesc, VertexbufferDescPtr, false);

            //// 转换顶点属性描述
            //IntPtr vertexattributesPtr = Marshal.AllocHGlobal(Marshal.SizeOf<GPUVertexAttribute>() * vertexattributes.Length);
            //for (int i = 0; i < vertexattributes.Length; i++)
            //{
            //    IntPtr attrPtr = vertexattributesPtr + i * Marshal.SizeOf<GPUVertexAttribute>();
            //    Marshal.StructureToPtr(vertexattributes[i], attrPtr, false);
            //}

            //GPUColorTargetDescription[] colorTargetDescs = new GPUColorTargetDescription[]
            //{
            //    new GPUColorTargetDescription
            //    {
            //        Format = GPUTextureFormat.R8G8B8A8Unorm, // 必须与_computeTexture的格式一致
            //        BlendState = new GPUColorTargetBlendState
            //        {
            //          EnableBlend = 0 // 禁用混合（默认）
            //     }
            //  }
            //};

            //GCHandle colorTargetDescsHandle = GCHandle.Alloc(colorTargetDescs, GCHandleType.Pinned);
            //nint colorTargetDescsPtr = colorTargetDescsHandle.AddrOfPinnedObject();

            //try
            //{
            //    var createInfo = new SDL.GPUGraphicsPipelineCreateInfo
            //    {
            //        VertexShader = vertShader,
            //        FragmentShader = fragShader,
            //        VertexInputState = new GPUVertexInputState
            //        {
            //            VertexBufferDescriptions = VertexbufferDescPtr,
            //            NumVertexBuffers = 1,
            //            VertexAttributes = vertexattributesPtr,
            //            NumVertexAttributes = (uint)vertexattributes.Length
            //        },

            //        PrimitiveType = GPUPrimitiveType.TriangleList,
            //        RasterizerState = new GPURasterizerState
            //        {
            //            FillMode = GPUFillMode.Fill,
            //            CullMode = GPUCullMode.Back,
            //            FrontFace = GPUFrontFace.CounterClockwise,
            //            DepthBiasConstantFactor = 0.0f,
            //            DepthBiasClamp = 0.0f,
            //            DepthBiasSlopeFactor = 0.0f,
            //            EnableDepthBias = 0,
            //            EnableDepthClip = 1,
            //        },
            //        MultisampleState = new GPUMultisampleState
            //        {
            //            SampleCount = GPUSampleCount.SampleCount1,
            //            SampleMask = 0,
            //            EnableMask = 0
            //        },
            //        DepthStencilState = new GPUDepthStencilState
            //        {
            //            CompareOp = GPUCompareOp.Less,
            //            WriteMask = 1,
            //            EnableDepthTest = 0,  // 禁用深度测试
            //            EnableDepthWrite = 0,  // 无需写入
            //            EnableStencilTest = 0,
            //            BackStencilState = new GPUStencilOpState
            //            {
            //                FailOp = GPUStencilOp.Keep,
            //                DepthFailOp = GPUStencilOp.Keep,
            //                PassOp = GPUStencilOp.Keep,
            //                CompareOp = GPUCompareOp.Always

            //            },
            //        },
            //        TargetInfo = new GPUGraphicsPipelineTargetInfo
            //        {
            //            ColorTargetDescriptions = colorTargetDescsPtr,
            //            NumColorTargets = 1,
            //            HasDepthStencilTarget = 0
            //        },
            //        Props = 0
            //    };

            //    _graphicsPipeline = SDL.CreateGPUGraphicsPipeline(
            //        _gpuDevice, createInfo
            //    );
            //}
            //catch {
            //    throw new Exception($"管线创建失败: {SDL.GetError()}");
            //}
            //vertShaderHandle.Free();
            //fragShaderHandle.Free();
            //colorTargetDescsHandle.Free();

            //// 5. 创建采样器
            //_sampler = SDL.CreateGPUSampler(
            //    _gpuDevice,
            //    new SDL.GPUSamplerCreateInfo
            //    {
            //        MinFilter=GPUFilter.Linear,//控制纹理缩小时的过滤方式Linear=过度插值
            //        MagFilter =GPUFilter.Linear,//控制纹理放大时的过滤方式Linear=过度插值
            //        MipmapMode = GPUSamplerMipmapMode.Linear,//果纹理有mipmap，需要设置如何选择mip级别我这里可能不需要设置
            //        AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,//纹理坐标超出范围时的处理方式
            //        AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            //        AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
            //        MipLodBias = 0.0f,      // 无Mip偏移
            //        MinLod = 0.0f,          // 最小LOD级别（无Mip时设为0）
            //        MaxLod = 0.0f,
            //        EnableAnisotropy = 0,//是否开启各向异性过滤
            //        MaxAnisotropy = 0f, // 最大各向异性过滤级别
            //        EnableCompare = 0,//是否开启深度比较
            //        CompareOp =GPUCompareOp.Always, // 深度比较测试Always总是通过
            //    }
            //);
        }
    }
}
