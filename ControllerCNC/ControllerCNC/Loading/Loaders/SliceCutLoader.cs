using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using ControllerCNC.GUI;
using ControllerCNC.Primitives;

namespace ControllerCNC.Loading.Loaders
{
    class SliceCutLoader : LoaderBase
    {
        /// <inheritdoc/>
        internal override ShapeItem Load(string path, ReadableIdentifier identifier)
        {
            var lines = File.ReadAllLines(path);
            var dimensions = new List<double>();
            foreach (var line in lines)
            {
                var parts = line.Split(' ');
                if (parts.Length == 0)
                    continue;

                var dimension = double.Parse(parts[0]);
                dimensions.Add(dimension);
            }

            var sliceThickness = dimensions[0];
            var sliceLength = dimensions[1];
            var sliceCount = (int)Math.Round(dimensions[2]);

            //TODO even counts are now not supported
            sliceCount = ((sliceCount + 1) / 2) * 2;

            var slicePoints = new List<Point2Dmm>();
            for (var i = 0; i < sliceCount; ++i)
            {
                var totalHeight = i * sliceThickness;
                if (i % 2 == 0)
                {
                    slicePoints.Add(new Point2Dmm(0, totalHeight));
                    slicePoints.Add(new Point2Dmm(sliceLength, totalHeight));
                }
                else
                {
                    slicePoints.Add(new Point2Dmm(sliceLength, totalHeight));
                    slicePoints.Add(new Point2Dmm(0, totalHeight));
                }
            }

            //this we can do only for odd slice counts
            slicePoints.Add(new Point2Dmm(0, 0));
            slicePoints.Reverse();

            var item = new ShapeItem2D(identifier, slicePoints);
            item.SetOriginalSize();
            return item;
        }
    }
}
