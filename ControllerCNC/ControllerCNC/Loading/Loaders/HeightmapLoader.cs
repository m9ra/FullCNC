using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Loading.Loaders
{
    class HeightmapLoader
    {
        private readonly int[,] _map;

        private readonly int _mapMax;

        private readonly int _mapMin;

        private readonly int _width;

        private readonly int _height;

        private readonly int _mapRange;

        internal HeightmapLoader(string filename)
        {
            var image = new Bitmap(Image.FromFile(filename));
            var bitmapData = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            var ptr = bitmapData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            var dataSize = Math.Abs(bitmapData.Stride) * image.Height;
            var data = new byte[dataSize];
            var stride = bitmapData.Stride;
            var width = bitmapData.Width;
            var height = bitmapData.Height;
            System.Runtime.InteropServices.Marshal.Copy(ptr, data, 0, dataSize);
            image.UnlockBits(bitmapData);

            _mapMax = int.MinValue;
            _mapMin = int.MaxValue;

            _map = new int[width, height];
            var bytesPerPixel = stride / width;
            for (var x = 0; x < width; ++x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var startB = (stride * y) + x * bytesPerPixel;

                    var r = data[startB];
                    var g = data[startB + 1];
                    var b = data[startB + 2];

                    var value = r + g + b;
                    _map[x, y] = value;

                    _mapMax = Math.Max(_mapMax, value);
                    _mapMin = Math.Min(_mapMin, value);
                }
            }

            _width = width;
            _height = height;
            _mapRange = _mapMax - _mapMin;
        }

        private double getHeight(int x, int y)
        {
            var value = _map[x, y];
            return 1.0 * (value - _mapMin) / _mapRange;
        }

        internal double[,] GetPoints()
        {
            var points = new double[_width, _height];
            for (var x = 0; x < _width; ++x)
            {
                for (var y = 0; y < _height; ++y)
                {
                    var z = getHeight(x, y);
                    points[x, y] = z;
                }
            }

            return points;
        }
    }
}
