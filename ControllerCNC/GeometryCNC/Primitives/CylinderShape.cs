using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Primitives
{
    class CylinderShape : Shape3D
    {
        internal readonly Point Center;

        internal readonly double Diameter;

        internal readonly double Height;

        internal CylinderShape(Point center, double height, double diameter)
        {
            Center = center;
            Height = height;
            Diameter = diameter;
        }

        internal override double GetVerticalHeight(Point p)
        {
            var distance = (Center - p).Length;
            if (distance <= Diameter / 2)
                return Height;

            return 0.0;
        }
    }
}