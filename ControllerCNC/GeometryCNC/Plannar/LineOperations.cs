using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Plannar
{
    public static class LineOperations
    {
        public static bool HasIntersection(Point l1s, Point l1e, Point l2s, Point l2e)
        {
            throw new NotImplementedException();
        }

        public static double DistanceToSegment(Point p, Point ls, Point le)
        {
            return DistanceToSegment(p, ls, le, out var _);
        }

        public static double DistanceToSegment(Point pt, Point ls, Point le, out Point closest)
        {
            // Calculate the distance between
            // point pt and the segment p1 --> p2.

            var dx = le.X - ls.X;
            var dy = le.Y - ls.Y;
            if ((dx == 0) && (dy == 0))
            {
                // It's a point not a line segment.
                closest = ls;
                dx = pt.X - ls.X;
                dy = pt.Y - ls.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            // Calculate the t that minimizes the distance.
            var t = ((pt.X - ls.X) * dx + (pt.Y - ls.Y) * dy) /
                (dx * dx + dy * dy);

            // See if this represents one of the segment's
            // end points or a point in the middle.
            if (t < 0)
            {
                closest = new Point(ls.X, ls.Y);
                dx = pt.X - ls.X;
                dy = pt.Y - ls.Y;
            }
            else if (t > 1)
            {
                closest = new Point(le.X, le.Y);
                dx = pt.X - le.X;
                dy = pt.Y - le.Y;
            }
            else
            {
                closest = new Point(ls.X + t * dx, ls.Y + t * dy);
                dx = pt.X - closest.X;
                dy = pt.Y - closest.Y;
            }

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
