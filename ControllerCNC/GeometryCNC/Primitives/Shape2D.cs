using GeometryCNC.Plannar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Primitives
{
    public class Shape2D
    {
        private readonly Point[] _points;

        private double[] _involve_constant;

        private double[] _involve_multiple;

        internal bool IsClosed => _points.First() == _points.Last();

        public IEnumerable<Point> Points => _points;

        public Shape2D(Point[] points)
        {
            _points = points.ToArray();
        }

        public Shape2D AsClosed()
        {
            if (IsClosed)
                return this;

            return new Shape2D(_points.Concat(new[] { _points.First() }).ToArray());
        }

        public double Distance(Point p)
        {
            var minDistance = double.PositiveInfinity;
            for (var i = 0; i < _points.Length - 1; ++i)
            {
                var p1 = _points[i];
                var p2 = _points[i + 1];

                var distance = LineOperations.DistanceToSegment(p, p1, p2);
                minDistance = Math.Min(minDistance, distance);
            }

            return minDistance;
        }

        public bool Involve(Point p)
        {
            if (!IsClosed)
                throw new InvalidOperationException("Cannot test values for an open shape");

            if (_involve_constant == null)
                precalculateInvolveValues();

            var x = p.X;
            var y = p.Y;

            var oddNodes = false;
            var j = _points.Length - 1 - 1; //skip last point
            for (var i = 0; i < _points.Length - 1; i++)
            {
                var polyXi = _points[i].X;
                var polyYi = _points[i].Y;
                var polyXj = _points[j].X;
                var polyYj = _points[j].Y;

                if (polyYi < y && polyYj >= y || polyYj < y && polyYi >= y)
                {
                    oddNodes ^= (y * _involve_multiple[i] + _involve_constant[i] < x);
                }
                j = i;
            }

            return oddNodes;
        }

        private void precalculateInvolveValues()
        {
            _involve_constant = new double[_points.Length - 1];
            _involve_multiple = new double[_involve_constant.Length];

            var j = _points.Length - 1 - 1; //skip last point
            for (var i = 0; i < _points.Length - 1; i++)
            {
                var polyXi = _points[i].X;
                var polyYi = _points[i].Y;
                var polyXj = _points[j].X;
                var polyYj = _points[j].Y;

                if (polyYj == polyYi)
                {
                    _involve_constant[i] = polyXi;
                    _involve_multiple[i] = 0;
                }
                else
                {
                    _involve_constant[i] = polyXi - (polyYi * polyXj) / (polyYj - polyYi) + (polyYi * polyXi) / (polyYj - polyYi);
                    _involve_multiple[i] = (polyXj - polyXi) / (polyYj - polyYi);
                }
                j = i;
            }
        }
    }
}
