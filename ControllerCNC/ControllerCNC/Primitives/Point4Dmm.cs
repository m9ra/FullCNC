using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class Point4Dmm
    {
        /// <summary>
        /// U dimension.
        /// </summary>
        public readonly double U;

        /// <summary>
        /// V dimension.
        /// </summary>
        public readonly double V;

        /// <summary>
        /// X dimension.
        /// </summary>
        public readonly double X;

        /// <summary>
        /// Y dimension.
        /// </summary>
        public readonly double Y;

        public Point4Dmm(double u, double v, double x, double y)
        {
            U = u;
            V = v;
            X = x;
            Y = y;
        }

        /// <summary>
        /// Squared eclidian distance to given point
        /// </summary>
        public double DistanceSquaredTo(Point4Dmm point)
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
        internal Point2Dmm ToUV()
        {
            return new Point2Dmm(U, V);
        }

        /// <summary>
        /// Selects XY.
        /// </summary>
        internal Point2Dmm ToXY()
        {
            return new Point2Dmm(X, Y);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var o = obj as Point4Dmm;
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
