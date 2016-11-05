using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    class JoinLine : WorkspaceItem
    {
        /// <summary>
        /// Size of displayed entry point.
        /// </summary>
        internal readonly static double EntryPointSize = 20;


        /// <summary>
        /// First shape where the join starts.
        /// Can be null for plan starting point.
        /// </summary>
        private readonly TrajectoryShapeItem _shape1;

        /// <summary>
        /// Second shape where the join ends.
        /// </summary>
        private readonly TrajectoryShapeItem _shape2;


        internal bool IsEntryPoint { get { return _shape1 == null; } }

        internal JoinLine(TrajectoryShapeItem shape1, TrajectoryShapeItem shape2)
        {
            if (shape2 == null)
                throw new ArgumentNullException("shape2");

            _shape1 = shape1;
            _shape2 = shape2;

            initialize();
        }

        internal void FillBuilder(PlanBuilder builder)
        {
            if (!IsEntryPoint)
                throw new NotImplementedException();

            //TODO all the joined shapes has to be traversed.
            var planner = new StraightLinePlanner(Constants.ReverseSafeSpeed);
            var plan = planner.CreateConstantPlan(new Trajectory4D(_shape2.TrajectoryPoints));

            builder.Add(plan.Build());
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            if (!IsEntryPoint)
                throw new NotImplementedException();

            var entryPoint = new Ellipse();
            entryPoint.Width = EntryPointSize;
            entryPoint.Height = EntryPointSize;
            entryPoint.RenderTransform = new TranslateTransform(-EntryPointSize / 2, -EntryPointSize / 2);

            var brush = new SolidColorBrush(Colors.Green);
            brush.Opacity = 0.3;
            entryPoint.Fill = brush;

            return entryPoint;
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size constraint)
        {
            var entryPoint = _shape2.TrajectoryPoints.First();
            PositionX = entryPoint.X;
            PositionY = entryPoint.Y;
            return base.MeasureOverride(constraint);
        }

    }
}
