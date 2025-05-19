using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpOsci
{
    internal class XYAudioRander_SDL3 : Control
    {
        private IntPtr _window;      // SDL窗口
        public sdlRander _renderer;
        private IntPtr _texture;     // 纹理（用于快速像素操作）
        private uint[] _pixels;      // 像素缓冲区

        public bool isplayer;


        private List<float> _xData = new List<float>();
        private List<float> _yData = new List<float>();


        private int _width, _height;
        private float _scaleX, _scaleY;
        private float _origin;

        private float[] _data;
        private long currentPlayerPosition; // 当前播放位置（0~_xData.length）
        private long lasrPlayerPosition;// 上次播放位置（0~_xData.length）

        public XYAudioRander_SDL3()
        {
            _renderer = new sdlRander(this);
        }
        public void SetData(List<float> xdata, List<float> ydata)
        {
            _xData.AddRange(xdata);
            _yData.AddRange(ydata);
        }
        public void SetPlayerPosition(long position)
        {
            lasrPlayerPosition = currentPlayerPosition;
            currentPlayerPosition = position;
        }


        protected override void  OnSizeChanged(EventArgs e)
        {
            _width = this.Width;
            _height = this.Height;
            _scaleX = (float)_width / (_xData.Count);
            _scaleY = (float)_height / (_yData.Count);
            _origin = _height / 2;
            if(this._renderer != null)
            {
                _renderer.Resize(_width, _height);
            }
        }

        public void DrawGrid(Graphics g, int width, int height)
        {
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            return;
        }
    }
}
