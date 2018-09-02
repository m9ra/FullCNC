using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;
using GeometryCNC.Primitives;

namespace MillingRouter3D.GUI
{
    [Serializable]
    class MillingShapeItemGCode : MillingItem
    {
        private readonly ToolPath _toolPath;

        internal MillingShapeItemGCode(string gcode, ReadableIdentifier identifier) :
            base(identifier)
        {
            var parser = new GeometryCNC.GCode.Parser(gcode);
            _toolPath = parser.GetToolPath();
        }

        internal MillingShapeItemGCode(SerializationInfo info, StreamingContext context)
    : base(info, context)
        {
            _toolPath = (ToolPath)info.GetValue("_toolPath", typeof(ToolPath));
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("_toolPath", _toolPath);
        }

        protected override Point2Dmm getEntryPoint()
        {
            return new Point2Dmm(PositionX, PositionY);
        }

        internal override void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace)
        {
            var maxZ = _toolPath.Targets.Select(t => t.Point.Z).Max();
            var minZ = _toolPath.Targets.Select(t => t.Point.Z).Min();
            foreach (var target in _toolPath.Targets)
            {
                var p = target.Point;
                var p3Dmm = new Point3Dmm(p.X + PositionX, -p.Y + PositionY, -p.Z + builder.ZeroLevel);
                if (target.MotionMode == MotionMode.IsLinearRapid)
                {
                    builder.AddRampedLine(p3Dmm);
                    //builder.AddCuttingLine(p3Dmm);
                }
                else
                {
                    builder.AddCuttingLine(p3Dmm);
                }
            }
            builder.AddRampedLine(getEntryPoint());
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            var itemPoints = new[] { new Point2Dmm(PositionX, PositionY) }.Concat(_toolPath.Targets.Select(t => new Point2Dmm(t.Point.X + PositionX, -t.Point.Y + PositionY)));

            var geometry = CreatePathFigure(new[] { itemPoints.ToArray() });
            drawingContext.DrawGeometry(null, new Pen(Brushes.Blue, 1.0), geometry);
        }
    }
}
