using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.ShapeEditor
{

    public class Triangulation2D
    {
        // From Wikipedia:
        // One way to triangulate a simple polygon is by using the assertion that any simple polygon
        // without holes has at least two so called 'ears'. An ear is a triangle with two sides on the edge
        // of the polygon and the other one completely inside it. The algorithm then consists of finding
        // such an ear, removing it from the polygon (which results in a new polygon that still meets
        // the conditions) and repeating until there is only one triangle left.

        // the algorithm here aims for simplicity over performance. there are other, more performant
        // algorithms that are much more complex.

        // convert a triangle to a list of triangles. each triangle is represented by a Point2Df array of length 3.
        public static IEnumerable<Point2Df[]> Triangulate(IEnumerable<Point2Df> points)
        {
            var poly = new Polygon(points);

            List<Point2Df[]> triangles = new List<Point2Df[]>();  // accumulate the triangles here
            // keep clipping ears off of poly until only one triangle remains
            while (poly.PtListOpen.Count > 3)  // if only 3 points are left, we have the final triangle
            {
                int midvertex = FindEar(poly);  // find the middle vertex of the next "ear"
                triangles.Add(new Point2Df[] { poly.PtList[midvertex - 1], poly.PtList[midvertex], poly.PtList[midvertex + 1] });
                // create a new polygon that clips off the ear; i.e., all vertices but midvertex
                List<Point2Df> newPts = new List<Point2Df>(poly.PtList);
                newPts.RemoveAt(midvertex);  // clip off the ear
                poly = new Polygon(newPts);  // poly now has one less point
            }
            // only a single triangle remains, so add it to the triangle list
            triangles.Add(poly.PtListOpen.ToArray());
            return triangles;
        }

        // find an ear (always a triangle) of the polygon and return the index of the middle (second) vertex in the ear
        public static int FindEar(Polygon poly)
        {
            for (int i = 0; i < poly.PtList.Count - 2; i++)
            {
                if (poly.VertexType(i + 1) == PolygonType.Convex)
                {
                    // get the three points of the triangle we are about to test
                    Point2Df a = poly.PtList[i];
                    Point2Df b = poly.PtList[i + 1];
                    Point2Df c = poly.PtList[i + 2];
                    bool foundAPointInTheTriangle = false;  // see if any of the other points in the polygon are in this triangle
                    for (int j = 0; j < poly.PtListOpen.Count; j++)  // don't check the last point, which is a duplicate of the first
                    {
                        if (j != i && j != i + 1 && j != i + 2 && PointInTriangle(poly.PtList[j], a, b, c)) foundAPointInTheTriangle = true;
                    }
                    if (!foundAPointInTheTriangle)  // the middle point of this triangle is convex and none of the other points in the polygon are in this triangle, so it is an ear
                        return i + 1;  // EXITING HERE!
                }
            }
            throw new ApplicationException("Improperly formed polygon");
        }

        // return true if point p is inside the triangle a,b,c
        public static bool PointInTriangle(Point2Df p, Point2Df a, Point2Df b, Point2Df c)
        {
            // three tests are required.
            // if p and c are both on the same side of the line a,b
            // and p and b are both on the same side of the line a,c
            // and p and a are both on the same side of the line b,c
            // then p is inside the triangle, o.w., not
            return PointsOnSameSide(p, a, b, c) && PointsOnSameSide(p, b, a, c) && PointsOnSameSide(p, c, a, b);
        }

        // if the two points p1 and p2 are both on the same side of the line a,b, return true
        private static bool PointsOnSameSide(Point2Df p1, Point2Df p2, Point2Df a, Point2Df b)
        {
            // these are probably the most interesting three lines of code in the algorithm (probably because I don't fully understand them)
            // the concept is nicely described at http://www.blackpawn.com/texts/pointinpoly/default.html
            double cp1 = CrossProduct(VSub(b, a), VSub(p1, a));
            double cp2 = CrossProduct(VSub(b, a), VSub(p2, a));
            return (cp1 * cp2) >= 0;  // they have the same sign if on the same side of the line
        }

        // subtract the vector (point) b from the vector (point) a
        private static Point2Df VSub(Point2Df a, Point2Df b)
        {
            return new Point2Df(a.C1 - b.C1, a.C2 - b.C2);
        }

        // find the cross product of two x,y vectors, which is always a single value, z, representing the three dimensional vector (0,0,z)
        private static double CrossProduct(Point2Df p1, Point2Df p2)
        {
            return (p1.C1 * p2.C2) - (p1.C2 * p2.C1);
        }
    }

    //Useful definititions. Polygons may be characterized by their degree of convexity:
    //Convex: any line drawn through the polygon (and not tangent to an edge or corner) meets its boundary exactly twice. 
    //Non-convex: a line may be found which meets its boundary more than twice. 
    //Simple: the boundary of the polygon does not cross itself. All convex polygons are simple. 
    //Concave: Non-convex and simple. 
    //Star-shaped: the whole interior is visible from a single point, without crossing any edge. The polygon must be simple, and may be convex or concave. 
    //Self-intersecting: the boundary of the polygon crosses itself. 

    //This class deals with Simple polygons only, either Concave or Convex.

    public enum PolygonType
    {
        Convex,
        Concave
    }

    public class Polygon
    {
        public readonly List<Point2Df> PtList;  // the points making up the Polygon; guaranteed to be Closed, such that the last point is the same as the first
        public readonly List<Point2Df> PtListOpen;  // the same PtList, but with the last point removed, i.e., an Open polygon
        public readonly double Area;
        public readonly PolygonType Type;

        // create a new polygon with a list of points (which won't change)
        public Polygon(IEnumerable<Point2Df> pts)
        {
            var ptlist = new List<Point2Df>(pts);
            PolyClose(ptlist);  // make sure the polygon is closed by duplicating the first point to the end, if necessary
            PtList = ptlist;
            PtListOpen = new List<Point2Df>(PtList);
            PtListOpen.RemoveAt(PtList.Count - 1);  // remove the last point, which is a duplicate of the first
            Area = PolyArea(PtList);
            Type = PolyType(PtList, Area);
        }

        // create a new pointlist that closes the polygon by adding the first point at the end
        private static void PolyClose(List<Point2Df> pts)
        {
            if (!IsPolyClosed(pts)) pts.Add(pts[0]);  // add a point at the end if it is not already closed
        }

        // find the area of a polygon. if the vertices are ordered clockwise, the area is negative, o.w. positive, but
        // the absolute value is the same in either case. (Remember that, in System.Drawing, Y is positive down.
        private static double PolyArea(List<Point2Df> ptlist)
        {
            double area = 0;
            for (int i = 0; i < ptlist.Count() - 1; i++) area += ptlist[i].C1 * ptlist[i + 1].C2 - ptlist[i + 1].C1 * ptlist[i].C2;
            return area / 2;
        }

        // find the type, Concave or Convex, of a Simple polygon
        private static PolygonType PolyType(List<Point2Df> ptlist, double area)
        {
            int polysign = Math.Sign(area);
            for (int i = 0; i < ptlist.Count() - 2; i++)
            {
                if (Math.Sign((double)PolyArea(new List<Point2Df> { ptlist[i], ptlist[i + 1], ptlist[i + 2] })) != polysign) return PolygonType.Concave;
            }
            return PolygonType.Convex;
        }

        // find the type of a specific vertex in a polygon, either Concave or Convex.
        public PolygonType VertexType(int vertexNo)
        {
            Polygon triangle;
            if (vertexNo == 0)
            {
                triangle = new Polygon(new List<Point2Df> { PtList[PtList.Count - 2], PtList[0], PtList[1] });  // the polygon is always closed so the last point is the same as the first
            }
            else
            {
                triangle = new Polygon(new List<Point2Df> { PtList[vertexNo - 1], PtList[vertexNo], PtList[vertexNo + 1] });
            }

            if (Math.Sign(triangle.Area) == Math.Sign(this.Area))
                return PolygonType.Convex;
            else
                return PolygonType.Concave;
        }

        private static bool IsPolyClosed(List<Point2Df> pts)
        {
            return IsSamePoint(pts[0], pts[pts.Count - 1]);
        }

        private static bool IsSamePoint(Point2Df pt1, Point2Df pt2)
        {
            return pt1.C1 == pt2.C1 && pt1.C2 == pt2.C2;
        }
    }
}
