using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using ControllerCNC.Primitives;

namespace ControllerCNC.ShapeEditor
{
    class FacetPanel : Panel
    {
        /// <summary>
        /// For now, only one part for facet is supported.
        /// </summary>
        private EditorShapePart _part;

        private Brush _itemBrush;

        private Pen _itemPen;

        private Pen _borderPen;

        private double _visualScale = 10.0;

        internal double MetricWidth
        {
            get
            {
                if (_part == null)
                    return 0;

                return _part.MetricWidth;
            }

            set
            {
                if (_part == null || value == _part.MetricWidth)
                    return;

                _part.MetricWidth = value;
                InvalidateVisual();
            }
        }

        internal double MetricHeight
        {
            get
            {
                if (_part == null)
                    return 0;

                return _part.MetricHeight;
            }

            set
            {
                if (_part == null || value == _part.MetricHeight)
                    return;

                _part.MetricHeight = value;
                InvalidateVisual();
            }
        }

        internal double MetricShiftX
        {
            get
            {
                if (_part == null)
                    return 0;

                return _part.MetricOffset.C1;
            }

            set
            {
                if (_part == null || value == _part.MetricOffset.C1)
                    return;

                _part.MetricOffset = new Point2Dmm(value, _part.MetricOffset.C2);
                InvalidateVisual();
            }
        }

        internal double MetricShiftY
        {
            get
            {
                if (_part == null)
                    return 0;

                return _part.MetricOffset.C2;
            }

            set
            {
                if (_part == null || value == _part.MetricOffset.C2)
                    return;

                _part.MetricOffset = new Point2Dmm(_part.MetricOffset.C1, value);
                InvalidateVisual();
            }
        }

        internal double VisualScale
        {
            get { return _visualScale; }
            set
            {
                if (_visualScale == value)
                    return;

                _visualScale = value;
                InvalidateVisual();
            }
        }

        internal double VisualOffsetX { get; private set; }

        internal double VisualOffsetY { get; private set; }

        public FacetPanel()
        {
            _itemBrush = Brushes.Gray;
            _itemPen = new Pen(Brushes.Black, 2.0);
            _borderPen = new Pen(Brushes.DarkGray, 10.0);
        }

        public void AddPart(EditorShapePart part)
        {
            if (_part != null)
                throw new NotImplementedException("Concat part dimensions");

            _part = part;

            InvalidateVisual();
        }

        public void ClearParts()
        {
            _part = null;
            InvalidateVisual();
        }

        public FacetShape CreateFacetShape()
        {
            return new FacetShape(_part.Points);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_part != null)
            {
                var figure = createFigure(_part.Points);
                var geometry = new PathGeometry(new[] { figure }, FillRule.EvenOdd, Transform.Identity);
                dc.DrawGeometry(_itemBrush, _itemPen, geometry);
            }

            dc.DrawRectangle(null, _borderPen, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        private PathFigure createFigure(IEnumerable<Point2Dmm> geometryPoints)
        {
            var pathSegments = new PathSegmentCollection();
            var isFirst = true;
            var firstPoint = new Point(0, 0);

            var xOffsetVisual = VisualOffsetX;
            var yOffsetVisual = ActualHeight / 2 + VisualOffsetY;

            foreach (var point in geometryPoints)
            {
                var planePoint = new Point(point.C1, point.C2);
                planePoint.X = planePoint.X * _visualScale + xOffsetVisual;
                planePoint.Y = planePoint.Y * _visualScale + yOffsetVisual;

                pathSegments.Add(new LineSegment(planePoint, !isFirst));
                if (isFirst)
                    firstPoint = planePoint;
                isFirst = false;
            }

            var figure = new PathFigure(firstPoint, pathSegments, false);
            return figure;
        }

        internal void FitSize(FacetPanel bindedPanel)
        {
            var maxOffsetX = Math.Max(Math.Abs(MetricShiftX), Math.Abs(bindedPanel.MetricShiftX));
            var maxOffsetY = Math.Max(Math.Abs(MetricShiftY), Math.Abs(bindedPanel.MetricShiftY));

            var maxWidth = Math.Max(MetricWidth + maxOffsetX, bindedPanel.MetricWidth + maxOffsetX);
            var maxHeight = Math.Max(MetricHeight + maxOffsetY, bindedPanel.MetricHeight + maxOffsetY);

            var availableWidth = ActualWidth - 20;
            var availableHeight = ActualHeight - 20;
            var sizeFactor = Math.Min(availableWidth / maxWidth, availableHeight / maxHeight);

            VisualScale = sizeFactor;

            VisualOffsetX = 10 + Math.Max(0, -Math.Min(MetricShiftX, bindedPanel.MetricShiftX)) * VisualScale;
            //VisualOffsetX = -Math.Min(_shapeMinC1, bindedPanel._shapeMinC1) * getMetricScale() * VisualScale - availableWidth / 2;
            //VisualOffsetY = -Math.Min(_shapeMinC2, bindedPanel._shapeMinC2) * getMetricScale() * VisualScale - availableHeight / 2;
            bindedPanel.VisualOffsetX = VisualOffsetX;
            bindedPanel.VisualOffsetY = VisualOffsetY;
            bindedPanel.VisualScale = sizeFactor;
        }
    }
}
