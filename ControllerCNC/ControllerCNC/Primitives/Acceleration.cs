using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    /// <summary>
    /// Determines how many steps per second will be accelerated per second
    /// </summary>
    class Acceleration
    {
        public readonly int Numerator;

        public readonly int Denominator;
    }
}
