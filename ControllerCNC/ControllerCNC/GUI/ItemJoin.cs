using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.GUI
{
    [Serializable]
    class ItemJoin
    {
        /// <summary>
        /// Index of point where the join will be connected on Item1.
        /// </summary>
        internal readonly int JoinPointIndex1;

        /// <summary>
        /// Shape1 of the connection.
        /// </summary>
        internal readonly PointProviderItem Item1;

        /// <summary>
        /// Index of point where the join will be connected on Item2.
        /// </summary>
        internal readonly int JoinPointIndex2;

        /// <summary>
        /// Shape2 of the connection.
        /// </summary>
        internal readonly PointProviderItem Item2;

        internal ItemJoin(PointProviderItem item1, int joinPointIndex1, PointProviderItem item2, int joinPointIndex2)
        {
            JoinPointIndex1 = joinPointIndex1;
            Item1 = item1;

            JoinPointIndex2 = joinPointIndex2;
            Item2 = item2;
        }
    }
}
