using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    public class Speed4Dstep
    {
        public readonly Point4Dstep Point;

        public readonly Speed SpeedUV;

        public readonly Speed SpeedXY;

        public Speed4Dstep(Point4Dstep point, Speed speedUV, Speed speedXY)
        {
            Point = point;
            SpeedUV = speedUV;
            SpeedXY = speedXY;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("{0} [{1},{2}]", Point, SpeedUV, SpeedXY);
        }
    }
}
