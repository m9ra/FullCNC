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

        private double _scale = 10.0;

        internal double Scale
        {
            get { return _scale; }
            set
            {
                if (_scale == value)
                    return;

                _scale = value;
                InvalidateVisual();
            }
        }

        public FacetPanel()
        {
            _itemBrush = Brushes.Gray;
            _itemPen = new Pen(Brushes.Black, 2.0);
            _borderPen = new Pen(Brushes.DarkGray, 10.0);
        }

        public void AddPart(EditorShapePart part)
        {
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

            return new FacetShape(_parts[0].Points);
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

        private PathFigure createFigure(IEnumerable<Point2Dmm> geometryPoints)
        {
            var pathSegments = new PathSegmentCollection();
            var isFirst = true;
            var firstPoint = new Point(0, 0);

            var xOffset = ActualWidth / 2;
            var yOffset = ActualHeight / 2;
            foreach (var point in geometryPoints)
            {
                var planePoint = new Point(point.C1, point.C2);
                planePoint.X = planePoint.X * _scale + xOffset;
                planePoint.Y = planePoint.Y * _scale + yOffset;

                pathSegments.Add(new LineSegment(planePoint, !isFirst));
                if (isFirst)
                    firstPoint = planePoint;
                isFirst = false;
            }

            var figure = new PathFigure(firstPoint, pathSegments, false);
            return figure;
        }
    }
}
