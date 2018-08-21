using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Primitives
{
    public abstract class Shape3D
    {
        /// <summary>
        /// Gets height under given point.
        /// </summary>
        internal abstract double GetVerticalHeight(Point p);
    }
}
