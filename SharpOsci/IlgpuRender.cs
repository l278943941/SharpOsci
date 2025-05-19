using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Util;
using ILGPU.Algorithms;
using Microsoft.VisualBasic;
using System.IO;
using ILGPU.Runtime.CPU;
using System.Windows.Forms;
using System.Runtime;
using ILGPU.Backends;
using System.Reflection;
using ILGPU.Backends.EntryPoints;
using System.Data;
using System.Numerics;
using static ILGPU.IR.MethodCollections;
using ILGPU.Algorithms.Vectors;

namespace SharpOsci
{
    internal class IlgpuRender
    {
        Context context;
        Accelerator accelerator;

        MemoryBuffer1D<float, Stride1D.Dense> osciScreen;
        MemoryBuffer1D<byte, Stride1D.Dense> osScreenImg;
        MemoryBuffer1D<int, Stride1D.Dense> Setings;
        MemoryBuffer1D<byte, Stride1D.Dense> pointdata;

        Action<Index2D, ArrayView<int>, ArrayView<float>> Kernel_Decayi;

        //Action<AcceleratorStream,KernelConfig, ArrayView<int>, ArrayView<float>> Kernel_Decayi_Launcher;
        Action<Index1D, ArrayView<byte>, ArrayView<int>, ArrayView<float>,int> Kernel_osci;
        Action<Index2D, ArrayView<float>, ArrayView<byte>, ArrayView<int>> Kernel_img;

        byte[] bytes;
        int imagewidth = 800;
        int imageheight = 600;
        bool isirending= false;
        bool isseting= false;
        int currentframe = 0;
        int currentlen = 0;
        int[] darwSet;
        byte[] image;
        float[] osScreenImg_cpu;
        Random Random = new Random((int)DateTime.Now.Ticks);
        public XYWaveformRenderer xyRenderer;

        public IlgpuRender()
        {
            context = Context.Create(builder =>
            {
                builder.Default(); // 使用默认配置
                builder.EnableAlgorithms(); // 关键：启用算法库
            });
            accelerator = context.CreateCudaAccelerator(0);
            //CreateCudaAccelerator(0);

            Debug.WriteLine(accelerator);

            osciScreen =accelerator.Allocate1D<float>(1920 * 1920 * 4);
            osScreenImg = accelerator.Allocate1D<byte>(800*600*4);
            pointdata = accelerator.Allocate1D<byte>(6400*2*3*10);
            Setings = accelerator.Allocate1D<int>(11);

            //var backend = accelerator.GetBackend();
            //var method = typeof(IlgpuRender).GetMethod(nameof(Kernel_osc_Decayi), BindingFlags.Public | BindingFlags.Static);
            //var entryPointDesc = EntryPointDescription.FromExplicitlyGroupedKernel(method);
            //var compiledKernel = backend.Compile(entryPointDesc, default);
            //using var kernel = accelerator.LoadKernel(compiledKernel);

            //Kernel_Decayi_Launcher = kernel.CreateLauncherDelegate<Action<AcceleratorStream, KernelConfig, ArrayView<int>, ArrayView<float>>>();

            Kernel_Decayi= accelerator.LoadAutoGroupedStreamKernel<
                Index2D,
                ArrayView<int>,
                ArrayView<float>
                >(Kernel_osc_Decayi);

            Kernel_osci = accelerator.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView<byte>,
                ArrayView<int>,
                ArrayView<float>,
                int>(Kernel_emu_osci);

             Kernel_img = accelerator.LoadAutoGroupedStreamKernel<
                Index2D,
                ArrayView<float>,
                ArrayView<byte>,
                ArrayView<int>>(Kernel_full_img);


            currentlen = 3200; currentframe = 0;

            bytes=new byte[6400*2*3*10];

            image = new byte[800 * 600 * 4];

            osScreenImg_cpu = new float[1920 * 1920 * 4];

        }

        public void Render(int len=3200)
        {
            while (isseting)
            {
                Thread.Sleep(1);
            }
            //开始计时
            Stopwatch sw = Stopwatch.StartNew();

            //传送数据到GPU
            //pointdata.View.SubView(0, len).CopyFromCPU(bytes);
            //传送设置到GPU
            //Setings.View.CopyFromCPU(darwSet);
            isirending = true;

            // 启动内核


            //Kernel_Decayi_Launcher(accelerator.DefaultStream, config, Setings.View, osciScreen.View);
            Setings.View.CopyFromCPU(darwSet);
            pointdata.View.SubView(0, darwSet[10]).CopyFromCPU(bytes);

            Kernel_Decayi((256, 64), Setings.View, osciScreen.View);
            accelerator.Synchronize();
             Kernel_osci(6400, pointdata.View, Setings.View, osciScreen.View, Random.Next());
            accelerator.Synchronize();
            Kernel_img((imagewidth, imageheight), osciScreen.View, osScreenImg.View, Setings.View);
            accelerator.Synchronize();
            //计算时间
            sw.Stop();

            Debug.WriteLine("gpu消耗时间:" + sw.ElapsedMilliseconds.ToString());

            osScreenImg.CopyToCPU(image);

            isirending = false;

            xyRenderer.Invoke(() =>
            {
                xyRenderer.DrawALLPoints3(image);
            });
            currentframe++;
        }



        public void Dispose()
        {
            osciScreen.Dispose();
            osScreenImg.Dispose();
            pointdata.Dispose();
            Setings.Dispose();
            accelerator.Dispose();
            context.Dispose();
        }



        public void run()
        {
            Thread thread = new Thread(()=>{
                currentframe = 0;
                while (true)
                {
                    //执行渲染
                    Render(currentlen);
                    //等待16ms
                    Thread.Sleep(16);
                }
            });
            thread.Start();
        }


        public void SetData(byte[] data,int len)
        {
            bytes = data;
            currentlen = data.Length;
            darwSet[10]= data.Length;
        }

        public void SetImageSize(int width, int height)
        {
            while (isirending)
            {
                Thread.Sleep(1);
            }
            isseting = true;
            imagewidth = width;
            imageheight = height;
            osScreenImg.Dispose();
            darwSet[0] = imagewidth; 
            darwSet[1] = imageheight;
            image= new byte[imagewidth * imageheight * 4];
            osScreenImg = accelerator.Allocate1D<byte>(imagewidth * imageheight * 4);
            isseting = false;
        }
        public void setSetings(darwSetings setings)
        {
            while (isirending)
            {
                Thread.Sleep(0);
            }
            isseting = true;
           darwSet = new int[11] {
                (int)setings.ImageHeight,
                (int)setings.ImageWidth,
                (int)setings._bufferStride,
                (int) setings.PEN_COLOR_R,
                (int)setings.PEN_COLOR_G,
                (int)setings.PEN_COLOR_B,
                (int)setings.PEN_WIDTH,
                (int)setings.TAU,
                (int)(setings.sigma*1000),
                (int)setings.miuscBitDepth,
                (int) currentlen
            };
            Setings.View.CopyFromCPU(darwSet);
            isseting = false;
        }
        public struct XorShift128
        {
            private uint _state0, _state1, _state2, _state3;

            public XorShift128(uint seed)
            {
                uint hash = 2166136261u;
                hash = (hash ^ (seed >> 0)) * 16777619u;
                hash = (hash ^ (seed >> 8)) * 16777619u;
                hash = (hash ^ (seed >> 16)) * 16777619u;
                hash = (hash ^ (seed >> 24)) * 16777619u;

                _state0 = hash + 0x6C078967u;
                _state1 = hash + 0x7F4A7C15u;
                _state2 = hash + 0x9638806Du;
                _state3 = hash + 0x3831F20Cu;

                // 确保状态非零
                if (_state0 == 0) _state0 = 1;
                if (_state1 == 0) _state1 = 1;
                if (_state2 == 0) _state2 = 1;
                if (_state3 == 0) _state3 = 1;
            }

            public uint NextUInt()
            {
                uint t = _state3;
                t ^= t << 11;
                t ^= t >> 8;
                _state3 = _state2;
                _state2 = _state1;
                _state1 = _state0;
                t ^= _state0;
                t ^= _state0 >> 19;
                _state0 = t;
                return t;
            }

            public float NextFloat()
            {
                return NextUInt() / (float)uint.MaxValue; // [0,1)
            }
        }




        public static void Kernel_osc_Decayi(Index2D index,ArrayView<int> setings,ArrayView<float> screen) {

            int TAU = setings[7];
            ////setings[7];
            float brightness = (float)FastExp(-16.0f / TAU);
            int screenindex = ((index.Y* 256 + index.X)*900 );
            for (int i = 0; i < (225 * 4); i += 4)
            {

                screen[screenindex + i] = screen[screenindex + i] < 1 ? 0 : screen[screenindex + i] * 0.3f;
                screen[screenindex + i + 1] = screen[screenindex + i + 1] < 1 ? 0 : screen[screenindex + i + 1] * 0.3f;
                screen[screenindex + i + 2] = screen[screenindex + i + 2] < 1 ? 0 : screen[screenindex + i + 2] * 0.3f;
                screen[screenindex + i + 3] = 255f;


            }

        }

        public static float FastExp(float x)
        {
            // 1. 处理极端值（防止溢出）
            const float MAX_INPUT = 88.0f;  // e^88 ≈ 1e38 (接近 float.MaxValue)
            const float MIN_INPUT = -88.0f; // e^-88 ≈ 1e-38

            // 手动钳制输入范围（不使用 Math.Clamp）
            if (x > MAX_INPUT) return float.MaxValue;
            if (x < MIN_INPUT) return 0.0f;

            // 2. 分解整数部分 n 和小数部分 r（不使用 Math.Floor）
            int n = (int)x;  // 直接转换为整数（等效于向零取整）
            float r = x - n; // 计算小数部分（可能为负数）

            // 3. 多项式近似计算 e^r
            float er = 1.0f + r * (1.0f + r * (0.5f + r * (0.1666667f + r * 0.041666667f)));

            // 4. 计算 e^n（通过位操作快速幂）
            float en = 1.0f;
            if (n != 0)
            {
                uint exponent = (uint)(n > 0 ? n : -n);
                float baseE = 2.718281828459045f; // e ≈ 2.71828
                float result = 1.0f;

                // 快速幂算法（循环展开优化）
                while (exponent > 0)
                {
                    if ((exponent & 1) == 1)
                        result *= baseE;
                    baseE *= baseE;
                    exponent >>= 1;
                }

                en = (n > 0) ? result : 1.0f / result;
            }

            // 5. 合并结果
            return en * er;
        }


        public static void darwPoint(int x,int y, ArrayView<float> screen, ArrayView<int> setings, float atten,float noiseidecay, int[] planxs,
        int[] planxy,
        float[] planatten, int planlen, int seed) {
            for (int plan_i = 0; plan_i < planlen; plan_i++)
            {
                int darwx = x + planxs[plan_i];
                int darwy = y + planxy[plan_i];
                float darwatten = planatten[plan_i];
                if (darwx < 0 || darwx >= 1920 || darwy < 0 || darwy >= 1920)
                {
                    continue;
                }
                int screenindex = ((darwy * (1920 * 4)) + (darwx * 4));

                float temp = screen[screenindex] + setings[5] * darwatten * atten;
                Atomic.Exchange(ref screen[screenindex], temp > 255f ? 255 : temp);
                temp = screen[screenindex + 1] + setings[4] * darwatten * atten;
                Atomic.Exchange(ref screen[screenindex + 1], temp > 255f ? 255 : temp);
                temp = screen[screenindex + 2] + setings[3] * darwatten * atten;
                Atomic.Exchange(ref screen[screenindex + 2], temp > 255f ? 255 : temp);

            }
            float u_base_noise = 0.09f;
            uint uniqueSeed = (uint)(seed );
            XorShift128 rng = new XorShift128(uniqueSeed);
            float noiseiDecay = noiseidecay;
            for (int noisei = 0; noisei < 5; noisei++)
            {
                float theta = (float)(rng.NextFloat() * Math.PI * 2.0f);
                float u = rng.NextFloat();
                float v = rng.NextFloat();

                // 生成半径（基于指数分布）
                float radiusx = (float)-(Math.Log(1 - u) / u_base_noise);

                // 极坐标转笛卡尔坐标
                int noisex = (int)(x + radiusx * Math.Cos(theta));
                int noisey = (int)(y + radiusx * Math.Sin(theta));  // 关键修正点

                // 边界检查
                if (noisex < 0 || noisex >= 1920 || noisey < 0 || noisey >= 1920)
                    continue;

                // 更新像素值
                int screenindex = noisey * (1920 * 4) + noisex * 4;

                float temp = screen[screenindex] + setings[5] * noiseiDecay;
                screen[screenindex] = temp > 255f ? 255 : temp;
                temp = screen[screenindex + 1] + setings[4] * noiseiDecay;
                screen[screenindex + 1] = temp > 255f ? 255 : temp;
                temp = screen[screenindex + 2] + setings[3] * noiseiDecay;
                screen[screenindex + 2] = temp > 255f ? 255 : temp;
            }
        }

        public static void Kernel_emu_osci(Index1D index, ArrayView<byte> miuscData, ArrayView<int> setings, ArrayView<float> screen,int seed=278943941)
        {
            int miuscBitDepth = setings[9];
            int len = (setings[10])*setings[9]/2;

            if (index >= len)
                return;

            int PEN_WIDTH = setings[6];
            int TAU = setings[7];
            float sigma = setings[8]/1000.0f;
            float pathLiting = 5.0f;
            //float pointliting = 0.04f;

            float ftameTime = 16.0f;

            float pointTilme = (float)ftameTime / len;//pointTilme无效是因为len无效


            float decay_time = (index * pointTilme); //这里index无效 pointTilme也是无效的


            //Interop.WriteLine("{0}", decay_time);



            //(float) Math.Exp(-decay_time / TAU);
            float decay = FastExp((-decay_time / TAU));


            //Interop.WriteLine("{0} ", index);



            int originX = 1920 / 2;
            int originY = 1920 / 2;

            float scalex = (int)originX * 0.95f;
            float scaley = (int)originY * 0.95f;

            float lastx = 0;
            float lasty = 0;

            int i = index * miuscBitDepth * 2;


            float x = 0;
            float y=0;
            switch (miuscBitDepth)
            {
                case 3:
                    x = ((miuscData[i] << 8 | miuscData[i + 1] << 16 | miuscData[i + 2] << 24) >> 8) / 8388608f;
                    y = -(((miuscData[i + 3] << 8 | miuscData[i + 4] << 16 | miuscData[i + 5] << 24) >> 8) / 8388608f);

                    lastx = i == 0 ? 0 : ((miuscData[i - 6] << 8 | miuscData[i - 5] << 16 | miuscData[i - 4] << 24) >> 8) / 8388608f;
                    lasty = -(i == 0 ? 0 : ((miuscData[i - 3] << 8 | miuscData[i - 2] << 16 | miuscData[i - 1] << 24) >> 8) / 8388608f);
                    break;
                default:
                    break;
            }

            //Interop.WriteLine("{0}", decay_time);

            int[] planxs=new int[81];
            int[] planxy = new int[81];
            float[] planatten = new float[81];
            int planlen = 0;

            for (int i1 = -(PEN_WIDTH); i1 < PEN_WIDTH+1; i1++)
            {
                for (int j = -(PEN_WIDTH); j < PEN_WIDTH+1 ; j++)
                {
                    float normeist = (float)Math.Sqrt(i1 * i1 + j * j);
                    //Interop.WriteLine("{0} {1} {2}", j, i1, normeist);
                    if (normeist <= PEN_WIDTH)
                    {
                        planxs[planlen] = j;
                        planxy[planlen] = i1;
                        planatten[planlen] = (float)FastExp(-(normeist * normeist) / (2.0f * sigma * sigma));
                        planlen++;
                    }
                }
            }


            x = x * scalex + originX;
            y = y * scaley + originY;


            float lastposx = lastx * scalex + originX;
            float lastposy = lasty * scaley + originY;


            float pathlen = (float)Math.Sqrt((x - lastposx) * (x - lastposx) + (y - lastposy) * (y - lastposy));

            float lenadd = 1.0f / pathlen;
            //if(pathlen!=0)
            //    Interop.WriteLine("{0} ", pathlen);

            float atten = (float)(decay * pathLiting * lenadd);

            //画出当前点
            darwPoint((int)x,(int)y,screen,setings, decay*0.004f,decay*0.3f, planxs, planxy, planatten, planlen,seed+index);
            int lastdarwx = (int)x;
            int lastdarwy = (int)y;
            //从上一个点画到当前点
            for (float path_i = 0; path_i < pathlen; path_i++)
            {
                int posix = (int)(lastposx + (x - lastposx) * path_i * lenadd);
                int posiy = (int)(lastposy + (y - lastposy) * path_i * lenadd);
                if (posix == lastdarwx && posiy == lastdarwy)
                {
                    continue;
                }
                else
                {
                    lastdarwx = posix;
                    lastdarwy = posiy;
                }

                int screenindex = ((posiy * (1920 * 4)) + (posix * 4));

                float temp = screen[screenindex ] + setings[5] * atten;
                Atomic.Exchange(ref screen[screenindex], temp > 255f ? 255 : temp);

                temp = screen[screenindex + 1] + setings[4] * atten;
                Atomic.Exchange(ref screen[screenindex + 1], temp > 255f ? 255 : temp);

                temp = screen[screenindex + 2] + setings[3] * atten;
                Atomic.Exchange(ref screen[screenindex + 2], temp > 255f ? 255 : temp);

            }

        }

        public static void Kernel_full_img(Index2D index, ArrayView<float> screen, ArrayView<byte> retimg, ArrayView<int> setings)
        {
            // 大图的宽度和高度
            int bigWidth = 1920; // 根据实际情况调整
            int bigHeight = 1920; // 应根据大图真实高度调整，例如从参数获取

            int smallWidth = setings[0];
            int smallHeight = setings[1];

            // 计算浮点缩放比例
            float scaleX = (float)bigWidth / smallWidth;
            float scaleY = (float)bigHeight / smallHeight;

            // 计算当前小图像素对应的大图区域
            int startX = (int)(index.X * scaleX);
            int endX = (int)((index.X + 1) * scaleX);
            // 处理最后一个像素的右边界
            if (index.X == smallWidth - 1)
                endX = bigWidth;

            int startY = (int)(index.Y * scaleY);
            int endY = (int)((index.Y + 1) * scaleY);
            // 处理最后一个像素的下边界
            if (index.Y == smallHeight - 1)
                endY = bigHeight;

            int r = 0, g = 0, b = 0, addnum = 0;
            int imgIndex = (index.Y * smallWidth + index.X) * 4;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int screenIndex = (y * bigWidth + x) * 4;
                    if (screenIndex < 0 || screenIndex + 2 >= screen.Length ||
                        imgIndex < 0 || imgIndex + 3 >= retimg.Length)
                        continue;

                    b += (int)screen[screenIndex];
                    g += (int)screen[screenIndex + 1];
                    r += (int)screen[screenIndex + 2];
                    addnum++;
                }
            }

            if (addnum > 0)
            {
                retimg[imgIndex] = (byte)(b / addnum);
                retimg[imgIndex + 1] = (byte)(g / addnum);
                retimg[imgIndex + 2] = (byte)(r / addnum);
            }
            else
            {
                // 处理无像素的情况，设为黑色
                retimg[imgIndex] = 0;
                retimg[imgIndex + 1] = 0;
                retimg[imgIndex + 2] = 0;
            }
            retimg[imgIndex + 3] = 255; // Alpha通道固定为255

        }
    }


}
