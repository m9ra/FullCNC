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

        public readonly Speed Speed;

        public Speed4Dstep(Point4Dstep point, Speed speed)
        {
            Point = point;
            Speed = speed;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("{0}[{1}]", Point, Speed);
        }
    }
}
