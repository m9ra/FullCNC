using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    internal static class Extensions
    {
        public static IEnumerable<Point4Df> DuplicateTo4D(this IEnumerable<Point2Df> points)
        {
            return points.Select(p => new Point4Df(p.C1, p.C2, p.C1, p.C2));
        }

        public static IEnumerable<Point4Df> As4Df(this IEnumerable<Point4D> points)
        {
            return points.Select(p => new Point4Df(p.U, p.V, p.X, p.Y));
        }

        public static IEnumerable<Point2Df> ToUV(this IEnumerable<Point4Df> points)
        {
            return points.Select(p => new Point2Df(p.U, p.V));
        }

        public static IEnumerable<Point2D> ToUV(this IEnumerable<Point4D> points)
        {
            return points.Select(p => new Point2D(p.U, p.V));
        }

        public static IEnumerable<Point2Df> ToXY(this IEnumerable<Point4Df> points)
        {
            return points.Select(p => new Point2Df(p.X, p.Y));
        }

        public static IEnumerable<Point2D> ToXY(this IEnumerable<Point4D> points)
        {
            return points.Select(p => new Point2D(p.X, p.Y));
        }
    }
}
