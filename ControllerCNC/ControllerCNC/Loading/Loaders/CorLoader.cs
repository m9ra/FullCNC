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
    class CorLoader : LoaderBase
    {
        /// <inheritdoc/>
        internal override ShapeItem Load(string path, ReadableIdentifier identifier)
        {
            var lines = File.ReadAllLines(path);
            var points = new List<Point2Dmm>();
            var pointsScale = 100.0;
            foreach (var line in lines)
            {
                if (line.Trim() == "")
                    continue;

                var pointParts = line.Trim().Split('\t');
                var x = double.Parse(pointParts[0]) * pointsScale;
                var y = double.Parse(pointParts[1]) * pointsScale;

                var point = new Point2Dmm(-x, -y);
                points.Add(point);
            }

            var shape = new ShapeItem2D(identifier, points);
            shape.MetricWidth = pointsScale;
            return shape;
        }
    }
}
