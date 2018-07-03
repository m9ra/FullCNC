using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ControllerCNC.Planning
{
    public static class GeometryUtils
    {
        public static readonly double Epsilon = 1e-2;

        public static bool IsZero(this Vector vector)
        {
            return vector.Length < Epsilon;
        }

        public static bool IsZero(double value)
        {
            return Math.Abs(value) < Epsilon;
        }

        public static double NormalizedAngleBetween(Vector v1, Vector v2)
        {
            return (Vector.AngleBetween(v1, v2) + 360) % (360);
        }

        public static bool ArePointsClockwise(IEnumerable<Point2Dmm> definition)
        {
            var points = definition.ToArray();

            var wSum = 0.0;
            for (var i = 1; i < points.Length; ++i)
            {
                var x1 = points[i - 1];
                var x2 = points[i];

                wSum += (x2.C1 - x1.C1) * (x2.C2 + x1.C2);
            }
            var isClockwise = wSum < 0;
            return isClockwise;
        }

        /// <summary>
        /// Test whether two line segments intersect. If so, calculate the intersection point.
        /// <see cref="http://stackoverflow.com/a/14143738/292237"/>
        /// </summary>
        /// <param name="p">Vector to the start point of p.</param>
        /// <param name="p2">Vector to the end point of p.</param>
        /// <param name="q">Vector to the start point of q.</param>
        /// <param name="q2">Vector to the end point of q.</param>
        /// <param name="intersection">The point of intersection, if any.</param>
        /// <param name="considerOverlapAsIntersect">Do we consider overlapping lines as intersecting?
        /// </param>
        /// <returns>True if an intersection point was found.</returns>
        public static bool LineSegementsIntersect(Point p, Point p2, Point q, Point q2,
            out Point intersection, bool considerCollinearOverlapAsIntersect = false)
        {
            intersection = new Point();

            var r = p2 - p;
            var s = q2 - q;
            var rxs = Vector.CrossProduct(r, s);
            var qpxr = Vector.CrossProduct((q - p), r);

            // If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
            if (IsZero(rxs) && IsZero(qpxr))
            {
                // 1. If either  0 <= (q - p) * r <= r * r or 0 <= (p - q) * s <= * s
                // then the two lines are overlapping,
                if (considerCollinearOverlapAsIntersect)
                    if ((0 <= (q - p) * r && (q - p) * r <= r * r) || (0 <= (p - q) * s && (p - q) * s <= s * s))
                        return true;

                // 2. If neither 0 <= (q - p) * r = r * r nor 0 <= (p - q) * s <= s * s
                // then the two lines are collinear but disjoint.
                // No need to implement this expression, as it follows from the expression above.
                return false;
            }

            // 3. If r x s = 0 and (q - p) x r != 0, then the two lines are parallel and non-intersecting.
            if (IsZero(rxs) && !IsZero(qpxr))
                return false;

            // t = (q - p) x s / (r x s)
            var t = Vector.CrossProduct((q - p), s) / rxs;

            // u = (q - p) x r / (r x s)

            var u = Vector.CrossProduct((q - p), r) / rxs;

            // 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
            // the two line segments meet at the point p + t r = q + u s.
            if (!IsZero(rxs) && (0 <= t && t <= 1) && (0 <= u && u <= 1))
            {
                // We can calculate the intersection point using either t or u.
                intersection = p + t * r;

                // An intersection was found.
                return true;
            }

            // 5. Otherwise, the two line segments are not parallel but do not intersect.
            return false;
        }

        public static bool IsPointInPolygon(Point point, Point[] polygon)
        {
            int polygonLength = polygon.Length, i = 0;
            bool inside = false;
            // x, y for tested point.
            var pointX = point.X;
            var pointY = point.Y;
            // start / end point for the current polygon segment.
            double startX, startY, endX, endY;
            var endPoint = polygon[polygonLength - 1];
            endX = endPoint.X;
            endY = endPoint.Y;
            while (i < polygonLength)
            {
                startX = endX; startY = endY;
                endPoint = polygon[i++];
                endX = endPoint.X; endY = endPoint.Y;
                //
                inside ^= (endY > pointY ^ startY > pointY) /* ? pointY inside [startY;endY] segment ? */
                          && /* if so, test if it is under the segment */
                          ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
            }
            return inside;
        }
    }
}
