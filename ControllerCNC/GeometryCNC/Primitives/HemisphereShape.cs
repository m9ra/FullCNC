using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Primitives
{
    class HemisphereShape : Shape3D
    {
        internal readonly Point Center;

        internal readonly double Radius;

        internal HemisphereShape(Point center, double radius)
        {
            Center = center;
            Radius = radius;
        }

        internal override double GetVerticalHeight(Point p)
        {
            var distance = (Center - p).Length;
            var sqrLen = Radius * Radius - distance * distance;
            
            if (sqrLen > 0)
                return Math.Sqrt(sqrLen);

            return 0.0;
        }
    }
}
