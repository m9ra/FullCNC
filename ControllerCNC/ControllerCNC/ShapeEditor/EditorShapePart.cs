using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;
using System.Windows;

namespace ControllerCNC.ShapeEditor
{
    class EditorShapePart
    {
        private readonly Point2Dmm[] _definitionPoints;

        private readonly double _minC1, _maxC1;

        private readonly double _diffC1;

        private readonly double _minC2, _maxC2;

        private readonly double _diffC2;

        private Size _metricSize;

        internal IEnumerable<Point2Dmm> Points
        {
            get
            {
                var factorC1 = _metricSize.Width / _diffC1;
                var factorC2 = _metricSize.Height / _diffC2;

                //c1 will be aligned to zero by default
                //c2 is not changed
                //THIS FOLLOWS AEROFOIL COMMON PRACTICES
                foreach (var point in _definitionPoints)
                {
                    yield return new Point2Dmm((point.C1 - _minC1) * factorC1 + MetricOffset.C1, point.C2 * factorC2 + MetricOffset.C2);
                }
            }
        }

        internal double MetricWidth
        {
            get
            {
                return _metricSize.Width;
            }
            set
            {
                _metricSize = new Size(value, value * (_diffC2) / (_diffC1));
            }
        }

        internal double MetricHeight
        {
            get
            {
                return _metricSize.Height;
            }
            set
            {
                _metricSize = new Size(value * (_diffC1) / (_diffC2), value);
            }
        }

        internal Point2Dmm MetricOffset { get; set; }

        internal EditorShapePart(IEnumerable<Point2Dmm> points)
        {
            _definitionPoints = points.ToArray();

            _minC1 = points.Min(p => p.C1);
            _maxC1 = points.Max(p => p.C1);
            _diffC1 = _maxC1 - _minC1;

            _minC2 = points.Min(p => p.C2);
            _maxC2 = points.Max(p => p.C2);
            _diffC2 = _maxC2 - _minC2;

            MetricOffset = new Point2Dmm(0, 0);

            //keep size on its defaults
            _metricSize = new Size(_diffC1, _diffC2);
        }
    }
}
