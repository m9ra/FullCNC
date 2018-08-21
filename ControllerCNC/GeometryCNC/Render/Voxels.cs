using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using GeometryCNC.Volumetric;

namespace GeometryCNC.Render
{
    public static class Voxels
    {
        public static MeshGeometry3D CreateMeshFrom(HeightMap map)
        {
            //collect points
            var originalPoints = new Point3D[map.VoxelCountX, map.VoxelCountY];

            var centerX = (map.GetPoint(map.VoxelCountX - 1, 0).X - map.GetPoint(0, 0).X) / 2;
            var centerY = (map.GetPoint(0, map.VoxelCountY - 1).Y - map.GetPoint(0, 0).Y) / 2;

            for (var xi = 0; xi < map.VoxelCountX; ++xi)
            {
                for (var yi = 0; yi < map.VoxelCountY; ++yi)
                {
                    var point = map.GetPoint(xi, yi);
                    var height = map.GetHeight(xi, yi);
                    originalPoints[xi, yi] = new Point3D(point.X - centerX, point.Y - centerY, height);
                }
            }

            //fill the mesh                    
            var pointMap = new Dictionary<Point3D, int>(3 * originalPoints.Length);

            var points = new Point3DCollection(2 * originalPoints.Length);
            var triangles = new Int32Collection(2 * originalPoints.Length);
            for (var xi = 0; xi < map.VoxelCountX - 1; ++xi)
            {
                for (var yi = 0; yi < map.VoxelCountY - 1; ++yi)
                {
                    var p1 = originalPoints[xi, yi];
                    var p2 = originalPoints[xi + 1, yi];
                    var p3 = originalPoints[xi + 1, yi + 1];
                    var p4 = originalPoints[xi, yi + 1];

                    var zp1 = new Point3D(p1.X, p1.Y, -1);
                    var zp2 = new Point3D(p2.X, p2.Y, -1);
                    var zp3 = new Point3D(p3.X, p3.Y, -1);
                    var zp4 = new Point3D(p4.X, p4.Y, -1);

                    addSquare(p1, p2, p3, p4, triangles, points, pointMap);
                    addSquare(zp1, zp2, zp3, zp4, triangles, points, pointMap);

                    /* var np2 = new Point3D(p2.X, p2.Y, p1.Z);
                    var np3 = new Point3D(p3.X, p3.Y, p1.Z);
                    var np4 = new Point3D(p4.X, p4.Y, p1.Z);

                 

                    addSquare(p1, np2, np3, np4, triangles, points, pointMap);
                    addSquare(p1, np2, zp1, zp2, triangles, points, pointMap);
                    addSquare(np2, np3, zp2, zp3, triangles, points, pointMap);
                    addSquare(np3, np4, zp3, zp4, triangles, points, pointMap);
                    addSquare(np4, p1, zp4, zp1, triangles, points, pointMap);
                    addSquare(zp1, zp2, zp3, zp4, triangles, points, pointMap); */
                    //addSquare(p2Index, p3Index, getIndex(p3, points, pointMap), getIndex(p4, points, pointMap), triangles);
                }
            }

            var geometry = new MeshGeometry3D();
            geometry.Positions = points;
            geometry.TriangleIndices = triangles;

            return geometry;
        }

        private static void addSquare(Point3D p1, Point3D p2, Point3D p3, Point3D p4, Int32Collection triangles, Point3DCollection points, Dictionary<Point3D, int> pointMap)
        {
            var p1i = getIndex(p1, points, pointMap);
            var p2i = getIndex(p2, points, pointMap);
            var p3i = getIndex(p3, points, pointMap);
            var p4i = getIndex(p4, points, pointMap);

            triangles.Add(p3i);
            triangles.Add(p2i);
            triangles.Add(p1i);

            triangles.Add(p3i);
            triangles.Add(p1i);
            triangles.Add(p4i);
        }

        private static int getIndex(Point3D point, Point3DCollection collection, Dictionary<Point3D, int> pointMap)
        {
            if (!pointMap.TryGetValue(point, out var index))
            {
                pointMap[point] = index = collection.Count;
                collection.Add(point);
            }

            return index;
        }
    }
}
