using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class Point4D
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


        /// <summary>
        /// Squared eclidian distance to given point
        /// </summary>
        public double DistanceSquaredTo(Point4D point)
        {
            var diffU = U - point.U;
            var diffV = V - point.V;
            var diffX = X - point.X;
            var diffY = Y - point.Y;

            return 1.0 * diffU * diffU + 1.0 * diffV * diffV + 1.0 * diffX * diffX + 1.0 * diffY * diffY;
        }

        /// <summary>
        /// Selects UV plane.
        /// </summary>
        internal Point2D ToUV()
        {
            return new Point2D(U, V);
        }

        /// <summary>
        /// Selects XY.
        /// </summary>
        internal Point2D ToXY()
        {
            return new Point2D(X, Y);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var o = obj as Point4D;
            if (o == null)
                return false;


            return U == o.U && V == o.V && X == o.X && Y == o.Y;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return U.GetHashCode() + V.GetHashCode() + X.GetHashCode() + Y.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("U:{0}, V:{1}, X:{2}, Y:{3}", U, V, X, Y);
        }
    }
}
