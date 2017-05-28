using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    [Serializable]
    class ItemJoin
    {
        /// <summary>
        /// Index of point where the join will be connected on Item2 if flexible entrance is allowed.
        /// </summary>
        private readonly int _joinPointIndex2;

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
        internal int JoinPointIndex2
        {
            get
            {
                return Item2.AllowFlexibleEntrance ? _joinPointIndex2 : 0;
            }
        }

        /// <summary>
        /// Shape2 of the connection.
        /// </summary>
        internal readonly PointProviderItem Item2;

        internal ItemJoin(PointProviderItem item1, int joinPointIndex1, PointProviderItem item2, int joinPointIndex2)
        {
            JoinPointIndex1 = joinPointIndex1;
            Item1 = item1;

            _joinPointIndex2 = joinPointIndex2;
            Item2 = item2;
        }

        /// <summary>
        /// Builds path to <see cref="Item2"/> and the item recursively. Assumes it starts from <see cref="Item1"/> outgoing point.
        /// Path ends at the same point it started.
        /// </summary>
        internal void Build(WorkspacePanel workspace, List<Speed4Dstep> speedPoints)
        {
            var outgoingPoint = Item1.CutPoints.Skip(JoinPointIndex1).First();
            var incommingPoint = Item2.CutPoints.Skip(JoinPointIndex2).First();
            var cuttingSpeed = workspace.CuttingSpeed;

            speedPoints.Add(incommingPoint.With(cuttingSpeed));
            Item2.Build(workspace, speedPoints, this);

            if (Item2 is NativeControlItem)
                // Native controls are handled in special way.
                return;

            speedPoints.Add(outgoingPoint.With(cuttingSpeed));
        }
    }
}
