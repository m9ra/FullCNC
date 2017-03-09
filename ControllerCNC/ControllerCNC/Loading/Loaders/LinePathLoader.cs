using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using ControllerCNC.GUI;
using ControllerCNC.Primitives;
using ControllerCNC.Machine;

namespace ControllerCNC.Loading.Loaders
{
    class LinePathLoader : LoaderBase
    {
        internal override ShapeItem Load(string path, ReadableIdentifier identifier)
        {
            var lines = File.ReadAllLines(path);

            var segmentSpeeds = new List<Speed>();
            var coordinates = new List<Point2Dmm>();
            coordinates.Add(new Point2Dmm(0, 0));

            foreach (var line in lines)
            {
                if (line.Trim() == "")
                    //empty line
                    continue;

                var coords = line.Split(' ');
                if (coords.Length != 3)
                    throw new FormatException("Invalid format on line: " + line);

                var coord1 = double.Parse(coords[0]);
                var coord2 = double.Parse(coords[1]);
                var speed = double.Parse(coords[2]);

                coordinates.Add(new Point2Dmm(coord1, coord2));
                var deltaT = Constants.MilimetersPerStep * Constants.TimerFrequency / speed;
                segmentSpeeds.Add(Speed.FromDeltaT((int)Math.Round(deltaT)));
            }


            var item = new NativeControlItem(identifier, coordinates, segmentSpeeds);
            item.SetOriginalSize();
            return item;
        }
    }
}
