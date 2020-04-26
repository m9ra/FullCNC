using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ControllerCNC.GUI;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

namespace MillingRouter3D.GUI
{
    [Serializable]
    class LevelMillingItem : MillingShapeItem2D
    {
        protected override bool useDenseCalculation => false;

        internal override double MetricHeight
        {
            get { return base.MetricHeight; }
            set
            {
                _shapeMetricSize.Height = value;
                _currentOffsetLines = null;
                fireOnSettingsChanged();
            }
        }

        internal override double MetricWidth
        {
            get { return base.MetricWidth; }
            set
            {
                _shapeMetricSize.Width = value;
                _currentOffsetLines = null;
                fireOnSettingsChanged();
            }
        }

        internal override IEnumerable<Point2Dmm[]> ShapeDefinition
        {
            get
            {
                return preparePoints(new[]
                {
                    new [] {
                        new Point2Dmm(0,0),
                        new Point2Dmm(MetricWidth, 0),
                        new Point2Dmm(MetricWidth, MetricHeight),
                        new Point2Dmm(0, MetricHeight),
                    }
                });
            }
        }

        internal override IEnumerable<PlaneShape> Shapes
        {
            get
            {
                return new[]
                {
                    new PlaneShape(ShapeDefinition.First().Take(4))
                };
            }
        }

        public LevelMillingItem(ReadableIdentifier name) : base(name, new[] { new[] { new Point2Dmm(0, 0), new Point2Dmm(1, 0), new Point2Dmm(1, 1), new Point2Dmm(0, 1) } })
        {
            MillingDepth = 0; // default to z level milling
        }

        internal LevelMillingItem(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        protected override void refreshOfffsetLines()
        {
            var panel = Parent as MillingWorkspacePanel;
            var toolWidth = panel.CuttingKerf;
            if (toolWidth <= 0)
            {
                _currentOffsetLines = null;
                return;
            }

            var points = new List<Point2Dmm>();
            var startY = toolWidth / 2;
            var endY = MetricHeight - toolWidth / 2;
            for (var i = 0; i < MetricWidth / toolWidth; ++i)
            {
                if (startY >= endY)
                    break;

                var currentX = i * toolWidth + toolWidth / 2;
                currentX = Math.Min(currentX, MetricWidth - toolWidth / 2);

                if (i % 2 == 0)
                {
                    points.Add(new Point2Dmm(currentX, startY));
                    points.Add(new Point2Dmm(currentX, endY));
                }
                else
                {
                    points.Add(new Point2Dmm(currentX, endY));
                    points.Add(new Point2Dmm(currentX, startY));
                }
            }

            _currentOffsetLines = new[] { points.ToArray() };
        }

        internal override void BuildPlan(PlanBuilder3D builder, MillingWorkspacePanel workspace)
        {
            var offsetLines = afterScaleTransformation(_currentOffsetLines);
            if(!offsetLines.Any() || !offsetLines.First().Any())
            {
                return;
            }

            builder.AddRampedLine(offsetLines.First().First());
            var currentDepth = 0.0;
            while (currentDepth <= MillingDepth)
            {
                builder.GotoZ(currentDepth);

                foreach (var cluster in offsetLines)
                {
                    foreach (var point in cluster)
                    {
                        builder.AddCuttingSpeedTransition(point);
                    }
                }

                var depthIncrement = Math.Min(workspace.MaxLayerCut, MillingDepth - currentDepth);
                if (depthIncrement <= 0)
                {
                    break;
                }
                currentDepth += depthIncrement;
            }
            builder.GotoTransitionLevel();
            builder.AddRampedLine(EntryPoint);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            refreshOfffsetLines();
            var _itemBrush = new SolidColorBrush(Color.FromArgb(128, 255, 64, 64));
            var _itemPen = new Pen(Brushes.DarkRed, 1.0);
            var _cutPen = new Pen(Brushes.Blue, 2.0);

            var itemPoints = TransformedShapeDefinition.ToArray();

            var geometry = CreatePathFigure(itemPoints);
            drawingContext.DrawGeometry(_itemBrush, _itemPen, geometry);

            var offsetLines = OffsetLines.ToArray();
            var offsetGeometry = CreatePathFigure(offsetLines);
            drawingContext.DrawGeometry(null, _cutPen, offsetGeometry);
        }
    }
}
