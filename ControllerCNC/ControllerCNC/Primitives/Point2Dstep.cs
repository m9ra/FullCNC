using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class Point2Dstep
    {
        /// <summary>
        /// First coordinate in 2D plane.
        /// </summary>
        public readonly int C1;

        /// <summary>
        /// Second coordinate in 2D plane.
        /// </summary>
        public readonly int C2;

        internal Point2Dstep(int c1, int c2)
        {
            C1 = c1;
            C2 = c2;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var o = obj as Point2Dstep;
            if (o == null)
                return false;

            return C1 == o.C1 && C2 == o.C2;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return C1.GetHashCode() + C2.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("C1:{0}, C2:{1}", C1, C2);
        }
    }
}
