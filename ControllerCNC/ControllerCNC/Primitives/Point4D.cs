using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    class Point4D
    {
        /// <summary>
        /// U dimension.
        /// </summary>
        public readonly int U;

        /// <summary>
        /// V dimension.
        /// </summary>
        public readonly int V;

        /// <summary>
        /// X dimension.
        /// </summary>
        public readonly int X;

        /// <summary>
        /// Y dimension.
        /// </summary>
        public readonly int Y;

        public Point4D(int u, int v, int x, int y)
        {
            U = u;
            V = v;
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return string.Format("U:{0}, V:{1}, X:{2}, Y:{3}", U, V, X, Y);
        }
    }
}
