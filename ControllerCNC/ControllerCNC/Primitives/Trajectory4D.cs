using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    class Trajectory4D
    {
        /// <summary>
        /// Points defined by the trajectory.
        /// </summary>
        public readonly IEnumerable<Point4D> Points;
    }
}
