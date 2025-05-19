
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Runtime.InteropServices;
using System;
using System.Numerics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Net.Mime.MediaTypeNames;

namespace SharpOsci
{

    [StructLayout(LayoutKind.Sequential)]
    public struct darwSetings
    {
        public uint ImageWidth;//图像宽度
        public uint ImageHeight;//图像高度
        public uint _bufferStride;//图像数据步幅
        public byte PEN_COLOR_R;//画笔颜色R
        public byte PEN_COLOR_G;//画笔颜色G
        public byte PEN_COLOR_B;//画笔颜色B
        public byte PEN_WIDTH;//画笔宽度
        public byte TAU;//余晖衰减系数
        public double sigma;//高斯衰减系数sigma
        public byte miuscBitDepth;//音乐位深度
        public uint pointArrayLength;//点数组长度
    }


    internal class XYWaveformRenderer : Control
    {
        private List<float> _xData = new List<float>(); // X轴数据（左声道）
        private List<float> _yData = new List<float>(); // Y轴数据（右声道）
        byte[] Data; // 数据（左右声道）
        private long _highlightProgress; // 播放进度（0~1）
        private long _lastProgress; // 上一阵的播放进度（0~1）
        public bool isplayer = false;


        private long lastbitbitTime=0; // 上一帧的时间

        public IlgpuRender ilgpuRender;

        // 成员变量
        private BITMAPINFO _bmi;
        private IntPtr _hdcControl;    // 控件的HDC
        private IntPtr _hdcMem;        // 内存HDC

        private IntPtr _hDib;
        private IntPtr _pixelsPtr;

        bool islock;

        int _bufferStride;


        private float _scaleX, _scaleY; // 每单位对应的像素
        private int[] _origin; // 原点坐标

        public darwSetings darwsetings;



        Color pointcolor = Color.White; // 点的颜色

        // Win32 API声明
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSrc, int xSrc, int ySrc, uint rasterOp
        );

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BITMAPINFO pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset
    );

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RGBQUAD
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)] // 至少一个颜色表条目
            public RGBQUAD[] bmiColors;
        }

        // 调用约定要与 C++ 一致（默认是 __cdecl | __stdcall 需显式指明）
        [DllImport("ampDraw.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int PPDarw_XYScreen(ulong imgPtr, ulong pointsPtr, ulong paramsPtr);
        [DllImport("ampDraw.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int AmpDarw_XYline(ulong imgPtr, ulong pointsPtr, ulong paramsPtr);


        public XYWaveformRenderer()
        {
            
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer| ControlStyles.Opaque, true);
            DoubleBuffered = true;
            ResizeRedraw = true;

        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // 获取控件原生HDC
            InitializeGdiResources();

            darwsetings = new darwSetings()
            {
                ImageHeight = (uint)Height,
                ImageWidth = (uint)Width,
                PEN_COLOR_R = 15,
                PEN_COLOR_G = 250,
                PEN_COLOR_B = 40,
                TAU = 10,
                sigma = 0.3,
                PEN_WIDTH = 3,
                miuscBitDepth = (byte)3,
            };



            //如果使用cppdll的话需要注释掉下面4行
            //ilgpuRender = new IlgpuRender();
            //ilgpuRender.xyRenderer = this;
            //ilgpuRender.setSetings(darwsetings);
            //ilgpuRender.run();

        }



        // 缓冲初始化/尺寸变化时调用
        private void ResizeBuffer()
        {
            if (Width <= 0 || Height <= 0) return;
            // 释放旧资源
            if (_hDib != IntPtr.Zero)
            {
                DeleteObject(_hDib);
                _hDib = IntPtr.Zero;
            }

            // 创建新的位图
            SelectObject(_hdcMem, _hDib);

            // 释放旧资源
            _bufferStride = Width * 4; // 每行字节数（32位色深）
            _origin =new int[] { (int)(Width / 2f),(int) (Height / 2f)};
            _scaleX = Math.Min(Width / 2f, Height / 2f) * 0.99f;
            _scaleY = Math.Min(Width / 2f, Height / 2f) * 0.99f;

            darwsetings.ImageWidth =(uint) Width;
            darwsetings.ImageHeight = (uint)Height;
            darwsetings._bufferStride = (uint)_bufferStride;

            if (ilgpuRender != null) 
            ilgpuRender.SetImageSize(Width, Height);

            // 初始化所有像素为透明黑色
            ClearBuffer();


        }



        // 清空缓冲
        private unsafe void ClearBuffer()
        {
            byte* p = (byte*)_pixelsPtr;
            int totalBytes = Width*4 * Height;
            for (int i = 0; i < totalBytes; i += 4)
            {
                p[i]  = (byte)(p[i]/1.6);   // B
                p[i + 1]  = (byte)(p[i + 1] / 1.6); // G
                p[i + 2]  = (byte)(p[i + 2] / 1.6); // R
                p[i + 3] = 255; // A (透明)
            }
        }



        public void SetData(List<float> xData, List<float> yData)
        {
            _xData = xData;
            _yData = yData;
            Invalidate();
        }

        public bool SetData2(byte[] p, int len)
        {
            darwsetings.pointArrayLength = (uint)len;
            Data = p;
            DrawALLPoints2(Data,(int) darwsetings.pointArrayLength);
            return true;
        }


        public void UpdateDisplay()
        {
            DrawALLPoints();
            Invalidate(); // 触发 OnPaint
        }


        public unsafe void DrawALLPoints3(byte[] p)
        {
            islock = true;

            byte* pp = (byte*)_pixelsPtr;
            fixed (byte* v = p)
            {
                Marshal.Copy(p,0, _pixelsPtr,Height*Width*4);
            }

            islock = true;
            FlushToScreen();

        }


        public int posdis(int x,int y) {
            return (int)Math.Sqrt(x* x +y * y);
        }
        public unsafe void DrawALLPoints2(byte[] p, int len) {
            islock = true;
            darwsetings.pointArrayLength = (uint)len;
            GCHandle seting = GCHandle.Alloc(darwsetings, GCHandleType.Pinned);
            nint setingPtr = seting.AddrOfPinnedObject();

            fixed (byte* v = p) {
                PPDarw_XYScreen((ulong)_pixelsPtr, (ulong)v, (ulong)setingPtr);
            }


            
            seting.Free();

            islock = true;
            FlushToScreen();

        }




        public unsafe void DrawALLPoints()
        {
            islock = true;



            ClearBuffer();


            DrawGrid();


            if (isplayer && _highlightProgress > 0)
            {
                int actionIndex = (int)_lastProgress ;
                int endIndex = (int)_highlightProgress;
                float[] lastpos = new float[2];
                lastpos[0]= _origin[0];
                lastpos[1] = _origin[1];

                int ox = _origin[0];
                int oy = _origin[1];

                int offset = oy * (Width*4) + ox * 4;
                byte* p = (byte*)_pixelsPtr;

                for (int i = 0; i < 2; i++)
                {
                    for(int j = 0;j < 2; j++)
                    {
                        offset = (oy+i) * (Width * 4) + (ox+j) * 4;
                        // 直接写入像素内存（覆盖原有颜色）
                        float xishu = Math.Abs((i-2)+(j-2)-3)/4.0f;
                        p[offset] = (byte)((255 - p[offset]) * 0.1f*0.5);     // Blue
                        p[offset + 1] = (byte)((255 - p[offset + 1]) * 0.9f * 0.5);// Green
                        p[offset + 2] = (byte)((255 - p[offset + 2]) * 0.1f * 0.5); // Red
                        p[offset + 3] = (byte)(255 * xishu); // Alpha
                    }
                }

                float timeDecayNumber = 0.5f;
                int fanalen = (endIndex- actionIndex);
                float timeDecayNumberadd = (0.5f * (1.0f / fanalen));
                for (int i = actionIndex; i < endIndex; i ++)
                {
                    if (i >= _xData.Count || i >= _yData.Count) break;

                    int x = (int)(_origin[0] + _xData[(int)i] * _scaleX);
                    int y = (int)(_origin[1] + -(_yData[(int)i] * _scaleY));

                    offset = y * _bufferStride + x * 4;

                    timeDecayNumber += timeDecayNumberadd;
                    //Debug.WriteLine($"fanalen: {fanalen}");
                    //Debug.WriteLine($"i: {i}");
                    //Debug.WriteLine($"actionIndex: {actionIndex}");
                    //Debug.WriteLine($"timeDecayNumber: {timeDecayNumber}");


                    // 直接写入像素内存（覆盖原有颜色）
                    p[offset] = (byte)((255 - p[offset])*(0.2* timeDecayNumber));     // Blue
                    p[offset + 1] = (byte)((255 - p[offset+1]) * (0.9* timeDecayNumber));// Green
                    p[offset + 2] = (byte)((255 - p[offset+2]) * (0.1* timeDecayNumber)); // Red
                    p[offset + 3] = 255; // Alpha

                    int[] possub = new int[2] { (int)lastpos[0] - x, (int)lastpos[1] - y };
                    int dis= posdis(possub[0], possub[1]);

                    for (int j = 0; j < dis; j++)
                    {
                        int x1 = (int)(x + (possub[0] * ((float)j / dis)));
                        int y1= (int)(y + (possub[1] * ((float)j / dis)));
                        offset = y1 * _bufferStride + x1 * 4;
                        float xishu = (0.1f + (float)(j / 2) / dis);
                        p[offset] = (byte)Math.Min(255, ((p[offset]+3)* 1.4)* timeDecayNumber);     // Blue
                        p[offset + 1] = (byte)Math.Min(255, ((p[offset]+18)* 1.6)* timeDecayNumber);// Green
                        p[offset + 2] = (byte)Math.Min(255, ((p[offset] +0.15)* 1.1)* timeDecayNumber); // Red
                p[offset + 3] = 255; // Alpha
                    }
                    lastpos[0] = x;
                    lastpos[1] = y;
                }
            }
            islock = false;
            // 手动触发重绘（或由外部定时器控制）
            FlushToScreen();
            //this.Invalidate();
            //this.Invalidate();
        }

        public void FlushToScreen()
        {

            if (_hdcControl == IntPtr.Zero || _hdcMem == IntPtr.Zero)
            {
                Debug.WriteLine("错误：HDC 未初始化！");
                return;
            }

            if (Width <= 0 || Height <= 0)
            {
                Debug.WriteLine("错误：控件尺寸无效！");
                return;
            }
            _hdcControl = GetDC(this.Handle);
            // 将内存HDC复制到控件HDC
            bool success = BitBlt(
                _hdcControl, 0, 0, this.Width, this.Height,
                _hdcMem, 0, 0, 0x00CC0020 // SRCCOPY
            );
            if (!success)
            {
                Debug.WriteLine("BitBlt 失败！错误代码：" + Marshal.GetLastWin32Error());
            }
            ReleaseDC(this.Handle, _hdcControl);
        }

        public bool SetPlayerPosition(long progress)
        {
            _lastProgress = _highlightProgress;
            _highlightProgress = progress;
            DrawALLPoints();
            return true;
        }

        public bool SetPlayerPosition2(long progress)
        {
            DrawALLPoints2(Data, (int)(progress*2*darwsetings.miuscBitDepth  - _lastProgress));
            _lastProgress = progress * 2 * darwsetings.miuscBitDepth;
            return true;
        }




        public bool SetSetings( darwSetings set)
        {
            darwsetings.PEN_COLOR_R = set.PEN_COLOR_R;
            darwsetings.PEN_COLOR_G = set.PEN_COLOR_G;
            darwsetings.PEN_COLOR_B = set.PEN_COLOR_B;
            darwsetings.PEN_WIDTH = set.PEN_WIDTH;
            darwsetings.TAU = set.TAU;
            darwsetings.sigma = set.sigma;

            darwsetings.miuscBitDepth = set.miuscBitDepth;

            return true;
        }

        //protected override void OnPaint(PaintEventArgs e)
        //{
        //    if (_xData.Count == 0 || _yData.Count == 0|| islock) 
        //        return;
        //}

        // 绘制XY网格（极坐标风格）
        private unsafe void DrawGrid()
        {
            using (var gridPen = new Pen(Color.Green, 0.5f))
            {
                int x = (int)(Width / 3f);
                int y = (int)(Height / 3f);

                int currentx = x;
                int currenty = y;
                Color color = Color.Gray;
                //绘制竖线
                for(int j=0;j<2; j++)
                {
                    for (int i = 0; i < Height; i++)
                    {
                        int offset = i * _bufferStride + currentx * 4;
                        byte* p = (byte*)_pixelsPtr;
                        p[offset] = (byte)(color.B/3);
                        p[offset + 1] = (byte)(color.G/3);
                        p[offset + 2] = (byte)(color.R/3);
                        p[offset + 3] = (byte)(color.A/3);
                    }
                    currentx += x ;
                }
                //绘制横线
                for (int j = 0; j < 2; j++)
                {
                    for (int i = 0; i < Width; i++)
                    {
                        int offset = currenty * _bufferStride + i * 4;
                        byte* p = (byte*)_pixelsPtr;
                        p[offset] = (byte)(color.B / 3);
                        p[offset + 1] = (byte)(color.G / 3);
                        p[offset + 2] = (byte)(color.R / 3);
                        p[offset + 3] = (byte)(color.A / 3);
                    }
                    currenty += y;
                }


            }
        }

        private void InitializeGdiResources()
        {
            // 释放旧的控件 HDC
            if (_hdcControl != IntPtr.Zero)
            {
                ReleaseDC(this.Handle, _hdcControl);
                _hdcControl = IntPtr.Zero;
            }

            // 获取当前控件的 HDC
            _hdcControl = GetDC(this.Handle);

            // 创建内存 HDC 和兼容位图
            _hdcMem = CreateCompatibleDC(_hdcControl);

            _bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                    biWidth = Width,
                    biHeight = -Height, // 负数表示自上而下位图
                    biPlanes = 1,
                    biBitCount = 32,    // 32位色深 (ARGB)
                    biCompression = 0,  // BI_RGB
                    biSizeImage = 0,
                    biClrUsed = 0,
                    biClrImportant = 0
                },
                bmiColors = new RGBQUAD[1] // 颜色表占位（即使不需要）
            };



            _hDib = CreateDIBSection(_hdcControl, ref _bmi, 0, out _pixelsPtr, 0 , 0);

            int a = Marshal.GetLastWin32Error();


            // 绑定新位图到内存 HDC，并释放旧位图
            IntPtr hOldBitmap = SelectObject(_hdcMem, _hDib);
            if (hOldBitmap != IntPtr.Zero)
            {
                DeleteObject(hOldBitmap);
            }
            ReleaseDC(this.Handle,_hdcControl);
        }

        private void ResizeGdiResources()
        {
            // 释放旧资源
            if (_hDib != IntPtr.Zero)
            {
                DeleteObject(_hDib);
                _hDib = IntPtr.Zero;
            }

            _bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                    biWidth = Width,
                    biHeight = -Height, // 负数表示自上而下位图
                    biPlanes = 1,
                    biBitCount = 32,    // 32位色深 (ARGB)
                    biCompression = 0,  // BI_RGB
                    biSizeImage = 0,
                    biClrUsed = 0,
                    biClrImportant = 0
                },
                bmiColors = new RGBQUAD[1] // 颜色表占位（即使不需要）
            };


            // 创建新尺寸的兼容位图
            _hDib = CreateDIBSection(_hdcControl, ref _bmi, 0, out _pixelsPtr, 0, 0);
            SelectObject(_hdcMem, _hDib);

            ResizeBuffer();
        }

        private void ReleaseGdiResources()
        {


            // 释放内存设备上下文（_hdcMem）
            if (_hdcMem != IntPtr.Zero)
            {
                // 1. 先移除位图对象，避免资源绑定
                IntPtr hOldBitmap = SelectObject(_hdcMem, _hDib);
                if (hOldBitmap != IntPtr.Zero)
                {
                    DeleteObject(hOldBitmap); // 删除旧位图（如果存在）
                }

                // 2. 删除当前 DIB 位图
                if (_hDib != IntPtr.Zero)
                {
                    DeleteObject(_hDib);
                    _hDib = IntPtr.Zero;
                }

                // 3. 删除内存设备上下文
                DeleteDC(_hdcMem);
                _hdcMem = IntPtr.Zero;

            }
        }


        protected override void Dispose(bool disposing)
        {

            base.Dispose(disposing);
            ReleaseGdiResources();
        }

        protected override void OnSizeChanged(EventArgs e)
        {

            base.OnSizeChanged(e);
            ReleaseGdiResources();
            InitializeGdiResources();
            ResizeBuffer(); // 尺寸变化
        }
      

    }
}