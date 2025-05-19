using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpOsci
{
    internal class WaveFormRenderer:Control
    {
        private float[] _data;
        private double _highlightPosition; // 高亮位置（0~1）



       

        private void DrawGrid(Graphics g, int width, int height)
        {
            using (var gridPen = new Pen(Color.LightGray, 0.5f))
            {
                // 水平网格
                for (int y = 0; y < height; y += height / 8)
                    g.DrawLine(gridPen, 0, y, width, y);

                // 垂直网格
                for (int x = 0; x < width; x += width / 10)
                    g.DrawLine(gridPen, x, 0, x, height);
            }
        }
    }
}
