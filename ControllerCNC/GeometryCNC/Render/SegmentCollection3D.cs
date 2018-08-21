using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace GeometryCNC.Render
{
    public class SegmentCollection3D
    {
        private readonly List<Tuple<Point3D, Point3D>> _segments = new List<Tuple<Point3D, Point3D>>();

        public void AddSegment(Point3D start, Point3D end)
        {
            _segments.Add(Tuple.Create(start, end));
        }

        public MeshGeometry3D CreateMesh(double lineThickness)
        {
            var mesh = new MeshGeometry3D();
            for (var i = 0; i < _segments.Count; ++i)
            {
                var p1 = _segments[i].Item1;
                var p2 = _segments[i].Item2;
                addSegment(mesh, p1, p2, lineThickness);
            }
            return mesh;
        }

        private static void addSegment(MeshGeometry3D mesh,
      Point3D point1, Point3D point2, double thickness)
        {
            // Find an up vector that is not colinear with the segment.
            var up = new Vector3D(0, 1, 0);
            var segment = point2 - point1;
            segment.Normalize();
            if (Math.Abs(Vector3D.DotProduct(up, segment)) > 0.9)
                up = new Vector3D(1, 0, 0);

            // Get the segment's vector.
            var v = point2 - point1;

            // Get the scaled up vector.
            var n1 = scaleVector(up, thickness / 2.0);

            // Get another scaled perpendicular vector.
            var n2 = Vector3D.CrossProduct(v, n1);
            n2 = scaleVector(n2, thickness / 2.0);

            // Make a skinny box.
            // p1pm means point1 PLUS n1 MINUS n2.
            var p1pp = point1 + n1 + n2;
            var p1mp = point1 - n1 + n2;
            var p1pm = point1 + n1 - n2;
            var p1mm = point1 - n1 - n2;
            var p2pp = point2 + n1 + n2;
            var p2mp = point2 - n1 + n2;
            var p2pm = point2 + n1 - n2;
            var p2mm = point2 - n1 - n2;

            // Sides.
            addTriangle(mesh, p1pp, p1mp, p2mp);
            addTriangle(mesh, p1pp, p2mp, p2pp);

            addTriangle(mesh, p1pp, p2pp, p2pm);
            addTriangle(mesh, p1pp, p2pm, p1pm);

            addTriangle(mesh, p1pm, p2pm, p2mm);
            addTriangle(mesh, p1pm, p2mm, p1mm);

            addTriangle(mesh, p1mm, p2mm, p2mp);
            addTriangle(mesh, p1mm, p2mp, p1mp);

            // Ends.
            addTriangle(mesh, p1pp, p1pm, p1mm);
            addTriangle(mesh, p1pp, p1mm, p1mp);

            addTriangle(mesh, p2pp, p2mp, p2mm);
            addTriangle(mesh, p2pp, p2mm, p2pm);
        }

        private static Vector3D scaleVector(Vector3D v, double targetLength)
        {
            v.Normalize();
            return v * targetLength;
        }

        private static void addTriangle(MeshGeometry3D mesh, Point3D p1, Point3D p2, Point3D p3)
        {
            var p1i = mesh.Positions.Count;
            var p2i = p1i + 1;
            var p3i = p2i + 1;

            mesh.TriangleIndices.Add(p1i);
            mesh.TriangleIndices.Add(p2i);
            mesh.TriangleIndices.Add(p3i);

            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);
        }
    }
}
