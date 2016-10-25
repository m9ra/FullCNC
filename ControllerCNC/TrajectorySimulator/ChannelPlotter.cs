using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace TrajectorySimulator
{
    class ChannelPlotter
    {
        public IEnumerable<Point4D> Plot2D(ChannelTrace channelX, ChannelTrace channelY)
        {
            var qX = new Queue<int>(channelX.Times);
            var qY = new Queue<int>(channelY.Times);

            var timeX = 0;
            var timeY = 0;

            var xCoord = 0;
            var yCoord = 0;
            var result = new List<Point4D>();

            while (qX.Count > 0 || qY.Count > 0)
            {
                var xPeek =qX.Count==0?int.MaxValue: qX.Peek() + timeX;
                var yPeek =qY.Count==0?int.MaxValue: qY.Peek() + timeY;

                if (xPeek == yPeek)
                {
                    timeX += qX.Dequeue();
                    timeY += qY.Dequeue();

                    xCoord += 1;
                    yCoord += 1;
                }
                else if (xPeek > yPeek)
                {
                    timeY += qY.Dequeue();
                    yCoord += 1;
                }
                else if (yPeek > xPeek)
                {
                    timeX += qX.Dequeue();
                    xCoord += 1;
                }

                result.Add(point(xCoord, yCoord));
            }

            return result;
        }

        private Point4D point(int x, int y)
        {
            return new Point4D(0, 0, x, y);
        }
    }
}
