using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class Point3Dmm
    {
        /// <summary>
        /// X dimension.
        /// </summary>
        public readonly double X;

        /// <summary>
        /// Y dimension.
        /// </summary>
        public readonly double Y;

        /// <summary>
        /// Z dimension.
        /// </summary>
        public readonly double Z;

        public Point3Dmm(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }


        /// <summary>
        /// Squared euclidian distance to given point
        /// </summary>
        public double DistanceSquaredTo(Point3Dmm point)
        {
            var diffZ = Z - point.Z;
            var diffX = X - point.X;
            var diffY = Y - point.Y;

            return 1.0 * diffZ * diffZ + 1.0 * diffX * diffX + 1.0 * diffY * diffY;
        }

        /// <summary>
        /// Euclidian distance to given point.
        /// </summary>
        public double DistanceTo(Point3Dmm point)
        {
            return Math.Sqrt(DistanceSquaredTo(point));
        }

        internal Point3Dmm ShiftTo(Point3Dmm target, double percentage)
        {
            var dx = target.X - X;
            var dy = target.Y - Y;
            var dz = target.Z - Z;

            return new Point3Dmm(X + dx * percentage, Y + dy * percentage, Z + dz * percentage);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var o = obj as Point3Dmm;
            if (o == null)
                return false;

            return X == o.X && Y == o.Y && Z == o.Z;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("X:{0}, Y:{1}, Z:{2}", X, Y, Z);
        }
    }
}
