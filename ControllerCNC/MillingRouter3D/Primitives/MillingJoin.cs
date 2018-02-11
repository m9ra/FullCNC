using ControllerCNC.Planning;
using MillingRouter3D.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MillingRouter3D.Primitives
{
    [Serializable]
    class MillingJoin
    {

        /// <summary>
        /// Shape1 of the connection.
        /// </summary>
        internal readonly MillingItem Item1;

        /// <summary>
        /// Shape2 of the connection.
        /// </summary>
        internal readonly MillingItem Item2;

        internal MillingJoin(MillingItem item1, MillingItem item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        internal void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace)
        {
            var initialPoint = builder.CurrentPoint;

            var entryPoint = Item2.EntryPoint;
            builder.GotoTransitionLevel();
            builder.AddRampedLine(entryPoint);

            Item2.BuildPlan(builder, workspace);
            builder.GotoTransitionLevel();

            foreach (var outgoingJoin in workspace.FindOutgoingJoins(Item2))
            {
                outgoingJoin.BuildPlan(builder, workspace);
            }

            builder.GotoTransitionLevel();
            builder.AddRampedLine(initialPoint);
        }
    }
}
