using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    [Serializable]
    abstract class PointProviderItem : WorkspaceItem
    {
        /// <summary>
        /// Points for cutting of the item.
        /// </summary>
        internal abstract IEnumerable<Point4Dstep> CutPoints { get; }

        /// <summary>
        /// Builds cutting plan for the item and all joined items recursively.
        /// Build assumes we are at item join point. Closed shapes has to return back to that point.
        /// </summary>
        /// <param name="workspace">Workspace where joins are defined.</param>
        /// <param name="speedPoints">Output of the build.</param>
        /// <param name="incommingJoin">Join which was used to get into the item.</param>
        internal abstract void Build(WorkspacePanel workspace, List<Speed4Dstep> speedPoints, ItemJoin incommingJoin);

        internal PointProviderItem(ReadableIdentifier name)
            : base(name)
        {
        }

        protected PointProviderItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
