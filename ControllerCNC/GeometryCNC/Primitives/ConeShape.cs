using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Primitives
{
    public class ConeShape : Shape3D
    {
        internal readonly Point Center;

        internal readonly double Radius;

        public ConeShape(Point center, double radius)
        {
            Center = center;
            Radius = radius;
        }

        internal override double GetVerticalHeight(Point p)
        {
            var distance = (Center - p).Length;
            return Math.Max(0.0, Radius - distance);
        }
    }
}
