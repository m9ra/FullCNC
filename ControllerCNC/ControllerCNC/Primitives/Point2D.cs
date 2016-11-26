using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class Point2D
    {
        /// <summary>
        /// First coordinate in 2D plane.
        /// </summary>
        public readonly int C1;

        /// <summary>
        /// Second coordinate in 2D plane.
        /// </summary>
        public readonly int C2;

        internal Point2D(int c1, int c2)
        {
            C1 = c1;
            C2 = c2;
        }
    }
}
