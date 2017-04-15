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
        private List<EditorShapePart> _parts = new List<EditorShapePart>();

        private Brush _itemBrush;

        private Pen _itemPen;

        private Pen _borderPen;

        private double _visualScale = 10.0;

        /// <summary>
        /// Determine size of the shape in milimeters.
        /// </summary>
        private Size _shapeMetricSize;

        private double _metricShiftX;

        private double _metricShiftY;

        protected double _shapeMaxC1 { get { return safeMax(_parts.SelectMany(part => part.Points.Select(point => point.C1))); } }

        protected double _shapeMaxC2 { get { return safeMax(_parts.SelectMany(part => part.Points.Select(point => point.C2))); } }

        protected double _shapeMinC1 { get { return safeMin(_parts.SelectMany(part => part.Points.Select(point => point.C1))); } }

        protected double _shapeMinC2 { get { return safeMin(_parts.SelectMany(part => part.Points.Select(point => point.C2))); } }

        internal double MetricWidth
        {
            get
            {
                return _shapeMetricSize.Width;
            }

            set
            {
                if (value == _shapeMetricSize.Width)
                    return;
                _shapeMetricSize = new Size(value, value * (_shapeMaxC2 - _shapeMinC2) / (_shapeMaxC1 - _shapeMinC1));
                InvalidateVisual();
            }
        }

        internal double MetricHeight
        {
            get
            {
                return _shapeMetricSize.Height;
            }

            set
            {
                if (value == _shapeMetricSize.Width)
                    return;
                _shapeMetricSize = new Size(value * (_shapeMaxC1 - _shapeMinC1) / (_shapeMaxC2 - _shapeMinC2), value);
                InvalidateVisual();
            }
        }

        internal double MetricShiftX
        {
            get
            {
                return _metricShiftX;
            }

            set
            {
                if (value == _metricShiftX)
                    return;

                _metricShiftX = value;
                InvalidateVisual();
            }
        }

        internal double MetricShiftY
        {
            get
            {
                return _metricShiftY;
            }

            set
            {
                if (value == _metricShiftY)
                    return;

                _metricShiftY = value;
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
            if (_parts.Count == 0)
            {
                //initialize dimensions
                var c1s = part.Points.Select(p => p.C1);
                var c2s = part.Points.Select(p => p.C2);
                var minC1 = c1s.Min();
                var maxC1 = c1s.Max();

                var minC2 = c2s.Min();
                var maxC2 = c2s.Max();

                var width = maxC1 - minC1;
                var height = maxC2 - minC2;

                _shapeMetricSize = new Size(width, height);
            }
            else
            {
                throw new NotImplementedException("Concat part dimensions");
            }

            _parts.Add(part);


            InvalidateVisual();
        }

        public void ClearParts()
        {
            _parts.Clear();
            InvalidateVisual();
        }

        public FacetShape CreateFacetShape()
        {
            if (_parts.Count != 1)
                throw new NotImplementedException();

            var metricScale = getMetricScale();
            return new FacetShape(_parts[0].Points.Select(p => new Point2Dmm(p.C1 * metricScale + MetricShiftX, p.C2 * metricScale + MetricShiftY)));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            foreach (var part in _parts)
            {
                var figure = createFigure(part.Points);
                var geometry = new PathGeometry(new[] { figure }, FillRule.EvenOdd, Transform.Identity);
                dc.DrawGeometry(_itemBrush, _itemPen, geometry);
            }

            dc.DrawRectangle(null, _borderPen, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        private double safeMin(IEnumerable<double> values)
        {
            if (values.Any())
                return values.Min();

            return double.PositiveInfinity;
        }

        private double safeMax(IEnumerable<double> values)
        {
            if (values.Any())
                return values.Max();

            return double.NegativeInfinity;
        }

        private PathFigure createFigure(IEnumerable<Point2Dmm> geometryPoints)
        {
            var pathSegments = new PathSegmentCollection();
            var isFirst = true;
            var firstPoint = new Point(0, 0);

            var xOffsetVisual = ActualWidth / 2 + VisualOffsetX;
            var yOffsetVisual = ActualHeight / 2 + VisualOffsetY;

            var metricScale = getMetricScale();

            foreach (var point in geometryPoints)
            {
                var planePoint = new Point(point.C1, point.C2);
                planePoint.X = (planePoint.X * metricScale + MetricShiftX) * _visualScale + xOffsetVisual;
                planePoint.Y = (planePoint.Y * metricScale + MetricShiftY) * _visualScale + yOffsetVisual;

                pathSegments.Add(new LineSegment(planePoint, !isFirst));
                if (isFirst)
                    firstPoint = planePoint;
                isFirst = false;
            }

            var figure = new PathFigure(firstPoint, pathSegments, false);
            return figure;
        }

        private double getMetricScale()
        {
            var xDiff = _shapeMaxC1 - _shapeMinC1;
            var yDiff = _shapeMaxC2 - _shapeMinC2;
            var metricScale = Math.Max(MetricWidth, MetricHeight) / Math.Max(xDiff, yDiff);
            return metricScale;
        }

        internal void FitSize(FacetPanel bindedPanel)
        {
            var maxWidth = Math.Max(MetricWidth, bindedPanel.MetricWidth);
            var maxHeight = Math.Max(MetricHeight, bindedPanel.MetricHeight);

            var availableWidth = ActualWidth - 20;
            var availableHeight = ActualHeight - 20;
            var sizeFactor = Math.Min(availableWidth / maxWidth, availableHeight / maxHeight);

            VisualScale = sizeFactor;

            VisualOffsetX = -Math.Min(_shapeMinC1, bindedPanel._shapeMinC1) * getMetricScale() * VisualScale - availableWidth / 2;
            //VisualOffsetY = -Math.Min(_shapeMinC2, bindedPanel._shapeMinC2) * getMetricScale() * VisualScale - availableHeight / 2;
            bindedPanel.VisualOffsetX = VisualOffsetX;
            bindedPanel.VisualOffsetY = VisualOffsetY;
            bindedPanel.VisualScale = sizeFactor;
        }
    }
}
