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
    class DatLoader : LoaderBase
    {
        /// <inheritdoc/>
        internal override ShapeItem Load(string path, ReadableIdentifier identifier)
        {
            var lines = File.ReadAllLines(path);
            var points = new List<Point2Dmm>();
            foreach (var line in lines)
            {
                var pointParts = sanitizeDatLine(line).Split(' ');
                if (pointParts.Length != 2)
                    //invalid line
                    continue;

                double x, y;
                if (!double.TryParse(pointParts[0], out x) || !double.TryParse(pointParts[1], out y))
                    continue;

                var point = new Point2Dmm(-x, -y);
                points.Add(point);
            }
            points.Add(points.First());
            var shape = new ShapeItem2D(identifier, points);
            shape.MetricWidth = 50;
            return shape;
        }

        private string sanitizeDatLine(string line)
        {
            var currentLine = line.Trim();
            var lastLine = "";

            while (currentLine != lastLine)
            {
                lastLine = currentLine;
                currentLine = currentLine.Replace("\t", " ").Replace("  ", " ");
            }

            return currentLine;
        }
    }
}
