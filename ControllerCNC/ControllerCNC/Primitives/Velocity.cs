using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    /// <summary>
    /// Velocity is defined as number of steps that has to be made in specified time.
    /// </summary>
    class Velocity
    {
        /// <summary>
        /// Velocity corresponding to no movement.
        /// </summary>
        public static Velocity Zero = new Velocity();

        /// <summary>
        /// Number of steps that should be done in given time.
        /// </summary>
        public readonly uint StepCount;

        /// <summary>
        /// Time available for doing steps.
        /// </summary>
        public readonly uint Time;


    }
}
