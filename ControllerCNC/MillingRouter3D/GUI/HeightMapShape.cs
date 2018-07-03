using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MillingRouter3D.GUI
{
    class HeightMapShape
    {
        private readonly double[,] _heightMap;

        internal HeightMapShape(double[,] heightMap)
        {
            _heightMap = (double[,])heightMap.Clone();
        }

        internal double GetHeight(double xRatio, double yRatio)
        {
            var xIndex = (int)Math.Round(xRatio * _heightMap.GetLength(0));
            var yIndex = (int)Math.Round(yRatio * _heightMap.GetLength(1));

            return Math.Min(1.0, Math.Max(1.0 - Math.Sin(yRatio * Math.PI * 5), 0));

            return _heightMap[xIndex, yIndex];
        }
    }
}
