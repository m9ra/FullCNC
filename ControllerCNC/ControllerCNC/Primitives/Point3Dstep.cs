using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class Point3Dstep
    {
        /// <summary>
        /// X dimension.
        /// </summary>
        public readonly int X;

        /// <summary>
        /// Y dimension.
        /// </summary>
        public readonly int Y;

        /// <summary>
        /// V dimension.
        /// </summary>
        public readonly int Z;

        public Point3Dstep(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var o = obj as Point3Dstep;
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
