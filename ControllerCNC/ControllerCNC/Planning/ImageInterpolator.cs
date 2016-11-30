using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;
using System.Drawing.Imaging;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    /// <summary>
    /// Finds coordinate interpolation of image against background. (specified by (0,0) point)
    /// </summary>
    public class ImageInterpolator
    {
        /// <summary>
        /// Color which is treated as a background.
        /// </summary>
        private readonly byte _backgroundColor;

        /// <summary>
        /// Data of the image.
        /// </summary>
        private readonly byte[] _data;

        /// <summary>
        /// Stride of data.
        /// </summary>
        private readonly int _stride;

        /// <summary>
        /// Width of the image.
        /// </summary>
        private readonly int _width;

        /// <summary>
        /// Height of the image.
        /// </summary>
        private readonly int _height;

        /// <summary>
        /// Registered contour points.
        /// </summary>
        private Dictionary<Tuple<int, int>, ContourPoint> _contourPoints = null;

        private HashSet<ContourPoint> _coloredPoints = null;

        public ImageInterpolator(string filename)
        {
            var image = new Bitmap(Image.FromFile(filename));
            var bitmapData = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);

            var ptr = bitmapData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            var dataSize = Math.Abs(bitmapData.Stride) * image.Height;
            _data = new byte[dataSize];
            _stride = bitmapData.Stride;
            _width = bitmapData.Width;
            _height = bitmapData.Height;
            System.Runtime.InteropServices.Marshal.Copy(ptr, _data, 0, dataSize);
            image.UnlockBits(bitmapData);
            _backgroundColor = _data[0];
        }

        public IEnumerable<Point2Dmm> InterpolateCoordinates()
        {
            //initialize datastructures
            _contourPoints = new Dictionary<Tuple<int, int>, ContourPoint>();
            _coloredPoints = new HashSet<ContourPoint>();

            //first pixel will be treated as mask
            var background = _data[0];

            var points = new List<Point>();
            for (var y = 1; y < _height - 1; ++y)
            {
                for (var x = 1; x < _width - 1; ++x)
                {
                    var currentColor = getColor(x, y);
                    if (currentColor != background)
                        continue;

                    if (!touchesSurface(x, y))
                        continue;

                    registerContourPoint(x, y);
                }
            }

            var blindPaths = new List<ContourPoint[]>();
            var circlePaths = new List<ContourPoint[]>();
            while (hasNonColoredPoints())
            {
                //first collect all blind paths (Theorem: coloring blind paths cannot destroy any circle)
                while (hasBlindPoint())
                {
                    var blindPoint = getBlindPoint();
                    var blindPath = getBlindPath(blindPoint);
                    blindPaths.Add(blindPath);
                    _coloredPoints.UnionWith(blindPath); //color the path
                }

                //then there has to be circle or end
                var circlePoint = _contourPoints.Values.Except(_coloredPoints).FirstOrDefault();
                if (circlePoint == null)
                    break;

                //ideally we would like to get largest circle, however, it is NP hard (TODO Solve it :D)
                var circlePath = getCirclePath(circlePoint);
                circlePaths.Add(circlePath);
                //coloring circle can create another blind paths
                _coloredPoints.UnionWith(circlePath);
            }

            var shrinkIterationCount = 5;
            var shrinkedPaths = circlePaths.ToList();
            for (var i = 0; i < shrinkedPaths.Count; ++i)
            {
                for (var j = 0; j < shrinkIterationCount; ++j)
                    shrinkedPaths[i] = shrinkLines(shrinkedPaths[i]).ToArray();
            }

            var orderedPoints = joinShapes(shrinkedPaths);
            //make the shape closed
            orderedPoints = orderedPoints.Concat(new[] { orderedPoints.First() }).ToArray();

            return orderedPoints.Select(p => new Point2Dmm(p.X, p.Y));
        }


        internal IEnumerable<Point4Dstep> InterpolateCoordinates(double scale)
        {
            var points = InterpolateCoordinates();

            var result = new List<Point4Dstep>();
            foreach (var point in points)
            {
                result.Add(point2D(point.C1, point.C2, scale));
            }
            return result;
        }

        private bool hasBlindPoint()
        {
            return getBlindPoint() != null;
        }

        private ContourPoint getBlindPoint()
        {
            //first try to search outer points
            foreach (var point in _contourPoints.Values)
            {
                if (_coloredPoints.Contains(point))
                    continue;

                if (point.Neighbours.Count() <= 1)
                    //it does not matter whether the neighbour is colored
                    return point;
            }

            //try to search leftovers
            foreach (var point in _contourPoints.Values)
            {
                if (_coloredPoints.Contains(point))
                    continue;

                if (point.Neighbours.Except(_coloredPoints).Count() <= 1)
                    return point;
            }

            return null;
        }

        private ContourPoint[] getBlindPath(ContourPoint point)
        {
            var path = new List<ContourPoint>();
            var tmpColoring = new HashSet<ContourPoint>();
            path.Add(point);
            tmpColoring.Add(point);

            while (point != null)
            {
                var nonColoredNeighbours = point.Neighbours.Except(tmpColoring).Except(_coloredPoints).ToArray();
                if (nonColoredNeighbours.Length == 1)
                {
                    if (tmpColoring.Add(point))
                        path.Add(point);
                    point = nonColoredNeighbours.First();
                }
                else
                    point = null;
            }

            return path.ToArray();
        }

        private ContourPoint[] getCirclePath(ContourPoint point)
        {
            //this method expect no blind paths - therefore there has to be circle 
            //containing given point
            var unavailablePoints = new HashSet<ContourPoint>(_coloredPoints);
            unavailablePoints.Add(point);
            var nonColoredNeighbours = point.Neighbours.Except(unavailablePoints).ToArray();
            //we are trying to find path between start/end without point (it has to exist)
            var start = nonColoredNeighbours[0];
            var end = nonColoredNeighbours[1];
            if (start.Neighbours.Contains(end))
                throw new NotImplementedException();

            //we will use DFS (BFS would give the worst circle, DFS can do better)
            var pathStack = new Stack<Stack<ContourPoint>>();
            pathStack.Push(new Stack<ContourPoint>(new[] { start }));
            while (pathStack.Count > 0)
            {
                var currentLayer = pathStack.Peek();
                if (currentLayer.Count == 0)
                {
                    //layer is empty 
                    pathStack.Pop();
                    //parent of the layer didn't succeed
                    pathStack.Peek().Pop();
                    continue;
                }

                //keep the point in the stack (for reconstruction)
                var currentPoint = currentLayer.Peek();
                var nextLayer = new Stack<ContourPoint>();
                foreach (var neighbour in currentPoint.Neighbours)
                {
                    if (unavailablePoints.Add(neighbour))
                        nextLayer.Push(neighbour);
                }

                pathStack.Push(nextLayer);
                if (nextLayer.Contains(end))
                    //we have found a circle
                    break;
            }

            var path = new List<ContourPoint>();
            path.Add(point);
            path.AddRange(pathStack.Select(s => s.Peek()));
            return path.ToArray();
        }

        private IEnumerable<ContourPoint> joinShapes(IEnumerable<ContourPoint[]> shapes)
        {
            var closedShapes = new List<ContourPoint[]>();
            foreach (var shape in shapes)
            {
                if (shape.First().Neighbours.Contains(shape.Last()))
                {
                    closedShapes.Add(shape);
                }
            }

            //fill table of join length between shapes
            var joinLengthSqr = new double[closedShapes.Count, closedShapes.Count];
            var joinStart = new ContourPoint[closedShapes.Count, closedShapes.Count];
            var joinEnd = new ContourPoint[closedShapes.Count, closedShapes.Count];

            for (var shape1Index = 0; shape1Index < closedShapes.Count - 1; ++shape1Index)
            {
                joinLengthSqr[shape1Index, shape1Index] = double.PositiveInfinity;
                var shape1 = closedShapes[shape1Index];
                for (var shape2Index = shape1Index + 1; shape2Index < closedShapes.Count; ++shape2Index)
                {
                    var shape2 = closedShapes[shape2Index];

                    joinLengthSqr[shape2Index, shape1Index] = double.PositiveInfinity;
                    joinLengthSqr[shape1Index, shape2Index] = double.PositiveInfinity;
                    for (var shape1PointIndex = 0; shape1PointIndex < shape1.Length; ++shape1PointIndex)
                    {
                        for (var shape2PointIndex = 0; shape2PointIndex < shape2.Length; ++shape2PointIndex)
                        {
                            var shape1Point = shape1[shape1PointIndex];
                            var shape2Point = shape2[shape2PointIndex];

                            var diffX = shape1Point.X - shape2Point.X;
                            var diffY = shape1Point.Y - shape2Point.Y;
                            var lengthSqr = 1.0 * diffX * diffX + diffY * diffY;
                            if (lengthSqr < joinLengthSqr[shape1Index, shape2Index])
                            {
                                joinLengthSqr[shape1Index, shape2Index] = lengthSqr;
                                joinStart[shape1Index, shape2Index] = shape1Point;
                                joinEnd[shape1Index, shape2Index] = shape2Point;
                            }
                        }
                    }
                }
            }

            //join shapes by the shortest joins

            for (var i = 0; i < closedShapes.Count - 1; ++i)
            {
                //n shapes will have n-1 joins
                var bestShape1Index = 0;
                var bestShape2Index = 0;
                for (var shape1Index = 0; shape1Index < closedShapes.Count - 1; ++shape1Index)
                {
                    for (var shape2Index = shape1Index + 1; shape2Index < closedShapes.Count; ++shape2Index)
                    {
                        var shape1 = closedShapes[shape1Index];
                        var shape2 = closedShapes[shape2Index];
                        if (shape1 == shape2)
                            //shapes are already joined
                            continue;

                        if (joinLengthSqr[shape1Index, shape2Index] < joinLengthSqr[bestShape1Index, bestShape2Index])
                        {
                            bestShape1Index = shape1Index;
                            bestShape2Index = shape2Index;
                        }
                    }
                }

                var shape1Point = joinStart[bestShape1Index, bestShape2Index];
                var shape2Point = joinEnd[bestShape1Index, bestShape2Index];
                joinShapes(closedShapes, bestShape1Index, shape1Point, bestShape2Index, shape2Point);
                joinLengthSqr[bestShape1Index, bestShape2Index] = double.NaN;
            }

            //all slots should contain same shape
            return closedShapes[0];
        }

        private void joinShapes(List<ContourPoint[]> shapes, int shape1Index, ContourPoint shape1Point, int shape2Index, ContourPoint shape2Point)
        {
            var shape1 = shapes[shape1Index];
            var shape2 = shapes[shape2Index];
            for (var i = 0; i < shape1.Length; ++i)
            {
                var point = shape1[i];
                if (point != shape1Point)
                    continue;

                var reorderedShape2 = reorderClosedShapeTo(shape2, shape2Point);


                var newShape = shape1.Take(i + 1).Concat(reorderedShape2).Concat(new[] { reorderedShape2.First() }).Concat(shape1.Skip(i)).ToArray();
                var targetShape1 = shapes[shape1Index];
                var targetShape2 = shapes[shape2Index];
                for (var j = 0; j < shapes.Count; ++j)
                {
                    if (shapes[j] == targetShape1 || shapes[j] == targetShape2)
                        shapes[j] = newShape;
                }
                return;
            }

            throw new InvalidOperationException("Join was not successful");
        }

        private ContourPoint[] reorderClosedShapeTo(ContourPoint[] shape, ContourPoint startPoint)
        {
            var startIndex = -1;
            for (var i = 0; i < shape.Length; ++i)
            {
                if (shape[i] == startPoint)
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex < 0)
                throw new NotSupportedException("Startpoint not found");

            var result = new List<ContourPoint>();
            for (var i = startIndex; i < shape.Length + startIndex + 1; ++i)
            {
                result.Add(shape[i % shape.Length]);
            }

            return result.ToArray();
        }

        private IEnumerable<ContourPoint> shrinkLines(IEnumerable<ContourPoint> points)
        {
            var result = new List<ContourPoint>();
            result.Add(points.First());

            var lastPointIndex = 0;
            var pointsArray = points.ToArray();
            for (var i = 1; i < pointsArray.Length - 1; ++i)
            {
                var lastPoint = pointsArray[lastPointIndex];
                var point = pointsArray[i];
                var nextPoint = pointsArray[i + 1];
                var totalDiffX = nextPoint.X - lastPoint.X;
                var totalDiffY = nextPoint.Y - lastPoint.Y;

                var isLineGoodAproximation = true;

                var isXaproximation = Math.Abs(totalDiffX) < Math.Abs(totalDiffY);
                var lineLength = i + 1 - lastPointIndex;
                var ratioX = 1.0 * totalDiffX / lineLength;
                var ratioY = 1.0 * totalDiffY / lineLength;

                for (var j = lastPointIndex; j < i + 1; ++j)
                {
                    //check whether line is good aproximator
                    var approximatedPoint = pointsArray[j];
                    var currentLineLength = j - lastPointIndex;
                    var approxX = lastPoint.X + ratioX * currentLineLength;
                    var approxY = lastPoint.Y + ratioY * currentLineLength;

                    var threshold = 0.85;
                    if (Math.Abs(approxX - approximatedPoint.X) > threshold || Math.Abs(approxY - approximatedPoint.Y) > threshold)
                    {
                        isLineGoodAproximation = false;
                        break;
                    }
                }

                if (isLineGoodAproximation)
                    //point can be skipped (it will be approximated by the line)
                    continue;

                lastPointIndex = i;
                result.Add(point);
            }

            result.Add(points.Last());

            return result;
        }



        /// <summary>
        /// Gets point which is not colored yet, if available.
        /// </summary>
        private ContourPoint getNonColoredPoint()
        {
            //first search for root non colored points
            foreach (var point in _contourPoints.Values)
            {
                if (point.IsRoot && !_coloredPoints.Contains(point))
                    return point;
            }

            //otherwise search for any non colored points
            foreach (var point in _contourPoints.Values)
            {
                if (!_coloredPoints.Contains(point))
                    return point;
            }

            //there is no such a point
            return null;
        }

        /// <summary>
        /// Determine whether there are points that are not colored yet.
        /// </summary>
        private bool hasNonColoredPoints()
        {
            return getNonColoredPoint() != null;
        }

        /// <summary>
        /// Registers new contour point.
        /// </summary>
        private void registerContourPoint(int x, int y)
        {
            var key = Tuple.Create(x, y);
            var point = new ContourPoint(x, y);
            _contourPoints.Add(key, point);

            tryAddNeighbour(point, x + 1, y);
            tryAddNeighbour(point, x - 1, y);
            tryAddNeighbour(point, x, y + 1);
            tryAddNeighbour(point, x, y - 1);
            /*
            tryAddNeighbour(point, x + 1, y + 1);
            tryAddNeighbour(point, x + 1, y - 1);
            tryAddNeighbour(point, x - 1, y + 1);
            tryAddNeighbour(point, x - 1, y - 1);*/
        }

        private void tryAddNeighbour(ContourPoint point, int x, int y)
        {
            ContourPoint neighbour;
            if (!_contourPoints.TryGetValue(Tuple.Create(x, y), out neighbour))
                return;

            if (hasCommonSurfacePoint(point, neighbour))
                point.AddNeighbour(neighbour);
        }

        private bool hasCommonSurfacePoint(ContourPoint point1, ContourPoint point2)
        {
            var x1 = point1.X;
            var y1 = point1.Y;

            return
                isCommonSurface(x1 + 1, y1, point2) ||
                isCommonSurface(x1 - 1, y1, point2) ||
                isCommonSurface(x1, y1 + 1, point2) ||
                isCommonSurface(x1, y1 - 1, point2) ||

                isCommonSurface(x1 + 1, y1 + 1, point2) ||
                isCommonSurface(x1 + 1, y1 - 1, point2) ||
                isCommonSurface(x1 - 1, y1 + 1, point2) ||
                isCommonSurface(x1 - 1, y1 - 1, point2);
        }

        private bool isCommonSurface(int x, int y, ContourPoint point)
        {
            if (!isSurface(x, y))
                return false;

            return Math.Abs(x - point.X) <= 1 && Math.Abs(y - point.Y) <= 1;
        }

        /// <summary>
        /// Determine whether there is surface on x, y
        /// </summary>
        private bool isSurface(int x, int y)
        {
            return getColor(x, y) != _backgroundColor;
        }

        /// <summary>
        /// Determine whether background on x, y touches surface.
        /// </summary>
        private bool touchesSurface(int x, int y)
        {
            return
                isSurface(x + 1, y) ||
                isSurface(x - 1, y) ||
                isSurface(x, y + 1) ||
                isSurface(x, y - 1) ||
                isSurface(x + 1, y + 1) ||
                isSurface(x + 1, y - 1) ||
                isSurface(x - 1, y + 1) ||
                isSurface(x - 1, y - 1);

        }

        private byte getColor(int x, int y)
        {
            return _data[y * _stride + x];
        }

        /// <summary>
        /// Converts 2D coordinates into Point4D.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>The <see cref="Point4Dstep"/>.</returns>
        private Point4Dstep point2D(int x, int y)
        {
            return new Point4Dstep(0, 0, x, y);
        }

        /// <summary>
        /// Converts and scales 2D coordinates into Point4D.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="scale">Scale of the coordinates.</param>
        /// <returns>The <see cref="Point4Dstep"/>.</returns>
        private Point4Dstep point2D(double x, double y, double scale)
        {
            return point2D((int)Math.Round(x * scale), (int)Math.Round(y * scale));
        }
    }


    /// <summary>
    /// Class for handling contour graph.
    /// </summary>
    class ContourPoint
    {
        private readonly HashSet<ContourPoint> _neighbours = new HashSet<ContourPoint>();

        internal IEnumerable<ContourPoint> Neighbours { get { return _neighbours.ToArray(); } }

        internal bool IsRoot { get { return _neighbours.Count <= 1; } }

        internal readonly int X;

        internal readonly int Y;

        internal ContourPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal void AddNeighbour(ContourPoint point)
        {
            if (point == this)
                throw new NotSupportedException("Cannot be self neighbour");

            this._neighbours.Add(point);
            point._neighbours.Add(this);
        }

        public override string ToString()
        {
            return string.Format("{0}, {1} ({2})", X, Y, _neighbours.Count);
        }
    }
}
