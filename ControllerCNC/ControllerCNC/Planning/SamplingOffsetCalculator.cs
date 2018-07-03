using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ControllerCNC.Planning
{
    public class SamplingOffsetCalculator
    {
        private readonly List<ShapeNode> _shapeNodes;

        /// <summary>
        /// contains closest distances to shape from inside
        /// </summary>
        private readonly List<double[,]> _meshes = new List<double[,]>();

        private readonly Dictionary<ShapeNode, double[,]> _clusterToMesh = new Dictionary<ShapeNode, double[,]>();

        private readonly List<Point[]> _clusters;

        private readonly double meshStep = 0.5;

        public SamplingOffsetCalculator(IEnumerable<Point2Dmm[]> clusters)
        {
            _clusters = clusters.Select(c => makeDense(c.Select(OffsetCalculator.AsPoint)).ToArray()).ToList();
            _shapeNodes = ShapeNode.From(_clusters).ToList();
            /**/
            foreach (var node in _shapeNodes)
            {
                var mesh = createMesh(node);
                _meshes.Add(mesh);
            }/**/
        }

        private IEnumerable<Point> makeDense(IEnumerable<Point> enumerable)
        {
            return enumerable;
            var isClosed = enumerable.First().Equals(enumerable.Last());
            if (!isClosed)
                throw new InvalidOperationException();

            var points = enumerable.ToArray();

            var result = new List<Point>();

            var denseFactor = 5;
            for (var pointIndex = 0; pointIndex < points.Length - 1; ++pointIndex)
            {
                var p1 = points[pointIndex];
                var p2 = points[pointIndex + 1];

                var v = (p2 - p1);
                var length = v.Length;
                var stepCount = (int)(length / denseFactor);

                result.Add(p1);
                for (var i = 0; i < stepCount; ++i)
                {
                    var newP = p1 + v * i / stepCount;
                    result.Add(newP);
                }
            }
            result.Add(result.First());
            return result.ToArray();
        }

        private double[,] createMesh(ShapeNode node)
        {
            var maxX = node.Points.Select(p => p.X).Max();
            var maxY = node.Points.Select(p => p.Y).Max();
            var xSteps = (int)(maxX / meshStep);
            var ySteps = (int)(maxY / meshStep);

            var result = new double[xSteps, ySteps];

            for (var xi = 0; xi < xSteps; ++xi)
            {
                var currentX = xi * meshStep;
                for (var yi = 0; yi < ySteps; ++yi)
                {
                    var currentY = yi * meshStep;
                    var distance = getDistance(currentX, currentY, node);
                    result[xi, yi] = distance;
                }
            }

            _clusterToMesh[node] = result;
            foreach (var child in node.OrderedChildren)
            {
                _clusterToMesh[child] = result;
            }

            return result;
        }

        private double getDistance(double currentX, double currentY, ShapeNode cluster)
        {
            var p = new Point(currentX, currentY);
            return cluster.InShapeDistance(p);
        }

        public IEnumerable<Point2Dmm[]> WithOffset(double offset)
        {
            var resultClusters = new List<Point[]>();
            var allNodes = _shapeNodes.Concat(_shapeNodes.SelectMany(n => n.OrderedChildren));

            foreach (var node in allNodes)
            {
                var resultCluster = new List<Point>();
                var mesh = getClusterMesh(node);
                foreach (var point in node.Points)
                {
                    var closestPoint = getClosestPointAbove(point, offset, node, mesh);
                    if (double.IsNaN(closestPoint.X))
                        continue;

                    var distance = node.InShapeDistance(closestPoint);
                   /* if (distance < offset)
                        throw new InvalidOperationException();*/

                    /*if (!GeometryUtils.IsPointInPolygon(closestPoint, node.Points))
                        throw new InvalidOperationException();*/

                    resultCluster.Add(closestPoint);
                }

                if (resultCluster.Count > 0)
                    resultClusters.Add(resultCluster.ToArray());
            }

            return resultClusters.Select(c => c.Select(OffsetCalculator.AsPoint2D).ToArray());
        }

        private double[,] getClusterMesh(ShapeNode node)
        {
            _clusterToMesh.TryGetValue(node, out var result);
            return result;
        }

        private Point getClosestPointAbove(Point point, double offset, ShapeNode node, double[,] mesh)
        {
            //search nearest point above offset - circular iteration should work here
            var currentOffset = offset;
            var bestPoint = new Point();

            while (true)
            {
                var maxDistance = double.NegativeInfinity;

                var spiralPoints = GetValidSpiralPoints(point, currentOffset);

                foreach (var spiralPoint in spiralPoints)
                {
                    var distance = GetBorderDistance(spiralPoint, mesh);
                    //var distance = getDistance(spiralPoint.X, spiralPoint.Y, node);
                    if (distance > maxDistance)
                    {
                        if (node.IntersectsWith(point, spiralPoint))
                            continue;

                        bestPoint = spiralPoint;
                        maxDistance = distance;
                    }
                }

                if (maxDistance < 0)
                    return new Point(double.NaN, double.NaN);

                if (maxDistance >= offset)
                    break;

                currentOffset += meshStep;
            }
            return bestPoint;
        }

        private double GetBorderDistance(Point spiralPoint, double[,] mesh)
        {
            var x = (int)Math.Round(spiralPoint.X / meshStep);
            var y = (int)Math.Round(spiralPoint.Y / meshStep);
            if (
                x < 0 || y < 0 ||
                x >= mesh.GetLength(0) || y >= mesh.GetLength(0)
                )
                return double.NegativeInfinity;


            return mesh[x, y];
        }

        private IEnumerable<Point> GetValidSpiralPoints(Point point, double offset)
        {
            var fullCircle = Math.PI * offset * 2;
            var stepCount = (int)(fullCircle / meshStep);

            var result = new Point[stepCount + 1];

            for (var i = 0; i <= stepCount; ++i)
            {
                var angle = i * 2 * Math.PI / stepCount;

                result[i].X = offset * Math.Cos(angle) + point.X;
                result[i].Y = offset * Math.Sin(angle) + point.Y;
            }

            return result;
        }
    }
}
