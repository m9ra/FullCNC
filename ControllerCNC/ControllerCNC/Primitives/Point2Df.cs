using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class Point2Df
    {
        /// <summary>
        /// First coordinate in 2D plane.
        /// </summary>
        public readonly double C1;

        /// <summary>
        /// Second coordinate in 2D plane.
        /// </summary>
        public readonly double C2;

        internal Point2Df(double c1, double c2)
        {
            C1 = c1;
            C2 = c2;
        }
    }
}
