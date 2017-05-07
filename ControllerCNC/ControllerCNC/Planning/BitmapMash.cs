using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    class BitmapMash
    {
        private readonly int _width;

        private readonly int _height;

        private readonly int _stride;

        private readonly byte[] _data;

        private readonly byte _background;

        private readonly MeshPoint[,] _mesh;

        internal IEnumerable<MeshPoint> MeshPoints
        {
            get
            {
                foreach (var point in _mesh)
                    if (point != null)
                        yield return point;
            }
        }

        internal IEnumerable<MeshPoint> ActiveMeshPoints
        {
            get
            {
                return MeshPoints.Where(p => p.IsActive);
            }
        }

        internal IEnumerable<MeshComponent> Components
        {
            get
            {
                return MeshPoints.Where(p => p.Component != null).Select(p => p.Component).Distinct().ToArray();
            }
        }

        internal BitmapMash(int width, int height, byte[] data, int stride)
        {
            _width = width;
            _height = height;
            _data = data;
            _stride = stride;
            _mesh = new MeshPoint[_width + 1, _height + 1];
            _background = getColor(0, 0);

            initializeMesh();
        }

        internal IEnumerable<Point2Dmm[]> GetShapeParts()
        {
            clusterComponents();
            relaxDiagonals();
            orientEdges();

            var contours = new List<MeshPoint[]>();
            foreach (var component in Components)
            {
                var componentContours = getContours(component);
                contours.AddRange(componentContours);
            }

            var parts = new List<Point2Dmm[]>();
            foreach (var contour in contours)
            {
                var part = new List<Point2Dmm>();
                foreach (var point in contour)
                {
                    part.Add(new Point2Dmm(point.X, point.Y));
                }

                parts.Add(part.ToArray());
            }
            return parts;
        }

        private IEnumerable<MeshPoint[]> getContours(MeshComponent component)
        {
            var circles = new List<MeshPoint[]>();

            var processedPoints = new HashSet<MeshPoint>();
            foreach (var point in component.Points)
            {
                if (!processedPoints.Add(point))
                    continue;

                var circle = new List<MeshPoint>();
                circle.Add(point);

                var currentPoint = point.Neighbours.First();
                while (currentPoint != point)
                {
                    //loop through the whole circle
                    var incomingPoint = circle[circle.Count - 1];
                    circle.Add(currentPoint);
                    processedPoints.Add(currentPoint);

                    currentPoint = currentPoint.GetOtherNeighbour(incomingPoint);
                }

                circles.Add(circle.ToArray());
            }

            return circles;
        }

        private void clusterComponents()
        {
            var processedPoints = new HashSet<MeshPoint>();
            var workList = new Queue<MeshPoint>();

            foreach (var meshPoint in ActiveMeshPoints)
            {
                if (processedPoints.Add(meshPoint))
                    workList.Enqueue(meshPoint);

                var component = new MeshComponent();
                while (workList.Count > 0)
                {
                    var point = workList.Dequeue();
                    component.Add(point);

                    foreach (var neighbour in point.Neighbours)
                    {
                        if (processedPoints.Add(neighbour))
                            workList.Enqueue(neighbour);
                    }
                }
            }
        }

        private void relaxDiagonals()
        {
            foreach (var meshPoint in MeshPoints)
            {
                meshPoint.TryRelaxDiagonal();
            }
        }

        private void orientEdges()
        {
            foreach (var component in Components)
            {
                orientComponentEdges(component);
            }
        }

        private void orientComponentEdges(MeshComponent component)
        {
            foreach (var nonOrientedPoint in component.NonOrientedPoints)
            {
                var root = nonOrientedPoint;
                while (true)
                {
                    var nextTarget = root.NextNonOrientedTarget();
                    if (nextTarget == null)
                        //all the circle is oriented
                        break;

                    root.OrientEdgeTo(nextTarget);
                    root = nextTarget;
                }
            }
        }

        private void initializeMesh()
        {
            for (var x = 0; x < _width + 1; ++x)
            {
                for (var y = 0; y < _height + 1; ++y)
                {
                    var a1 = isActive(x, y);
                    var a2 = isActive(x - 1, y);
                    var a3 = isActive(x + 1, y);
                    var a4 = isActive(x, y - 1);
                    var a5 = isActive(x, y + 1);

                    if (a2 == a1 && a3 == a1 && a4 == a1 && a5 == a1)
                    {
                        var b2 = isActive(x - 1, y - 1);
                        var b3 = isActive(x + 1, y + 1);
                        var b4 = isActive(x - 1, y - 1);
                        var b5 = isActive(x + 1, y + 1);

                        if (b2 == a1 && b3 == a1 && b4 == a1 && b5 == a1)
                            //no more testing needed - edges cannot be here
                            continue;
                    }

                    _mesh[x, y] = new MeshPoint(x, y);
                }
            }

            for (var x = 0; x < _width + 1; ++x)
            {
                for (var y = 0; y < _height + 1; ++y)
                {
                    var point = _mesh[x, y];
                    if (point == null)
                        continue;

                    setEdges(_mesh[x, y]);
                }
            }
        }

        private void setEdges(MeshPoint point)
        {
            trySetEdge(point, 1, 0);
            trySetEdge(point, -1, 0);
            trySetEdge(point, 0, 1);
            trySetEdge(point, 0, -1);
        }

        private void trySetEdge(MeshPoint point, int xOffs, int yOffs)
        {
            var targetPoint = getPoint(point.X + xOffs, point.Y + yOffs);
            if (targetPoint == null)
                return;

            var minX = Math.Min(point.X, targetPoint.X);
            var minY = Math.Min(point.Y, targetPoint.Y);

            /*
             * A--D
             * |  |
             * B--C
             */
            var firstPixelActivity = isActive(minX, minY);

            //second pixel is orthogonaly shifted
            var secondPixelActivity = isActive(minX - Math.Abs(yOffs), minY - Math.Abs(xOffs));

            var hasEdge = firstPixelActivity != secondPixelActivity;
            if (hasEdge)
                point.SetEdge(targetPoint);
        }

        private byte getColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
                return _background;

            return _data[y * _stride + x];
        }

        private MeshPoint getPoint(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width + 1 || y >= _height + 1)
                return null;

            return _mesh[x, y];
        }

        private bool isActive(int x, int y)
        {
            return getColor(x, y) != _background;
        }
    }

    class MeshPoint
    {
        public readonly int X;

        public readonly int Y;

        public MeshComponent Component { get; set; }

        public IEnumerable<MeshPoint> Neighbours { get { return getEdges().Where(e => e != null).Select(e => e.Target); } }

        public bool IsActive { get { return Neighbours.Any(); } }

        public bool NeedsOrientation { get { return getEdges().Where(e => e != null && e.NeedsOrientation).Any(); } }

        private MeshEdge _top;

        private MeshEdge _bottom;

        private MeshEdge _left;

        private MeshEdge _right;

        internal MeshPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal void SetEdge(MeshPoint otherPoint)
        {
            var selfEdge = new MeshEdge();
            selfEdge.Target = this;

            var edge = new MeshEdge();
            edge.Target = otherPoint;

            if (otherPoint.Y == Y + 1)
            {
                _top = edge;
                otherPoint._bottom = selfEdge;
                return;
            }

            if (otherPoint.Y == Y - 1)
            {
                _bottom = edge;
                otherPoint._top = selfEdge;
                return;
            }

            if (otherPoint.X == X + 1)
            {
                _right = edge;
                otherPoint._left = selfEdge;
                return;
            }

            if (otherPoint.X == X - 1)
            {
                _left = edge;
                otherPoint._right = selfEdge;
                return;
            }

            throw new NotImplementedException();
        }

        internal void TryRelaxDiagonal()
        {
            var isDiagonal = _top != null && _bottom != null && _left != null && _right != null;
            if (!isDiagonal)
                return;

            // there is no other way for a node to have 4 edges, than to be a diagonal 
            // NOTICE: top target has to have exactly one left or right node
            var isLeftDiagonal = _top.Target._left != null;
            if (isLeftDiagonal)
            {
                _bottom.Target._top.Target = _left.Target;
                _left.Target._right.Target = _bottom.Target;

                _top.Target._bottom.Target = _right.Target;
                _right.Target._left.Target = _top.Target;
            }
            else
            {
                _bottom.Target._top.Target = _right.Target;
                _right.Target._left.Target = _bottom.Target;

                _top.Target._bottom.Target = _left.Target;
                _left.Target._right.Target = _top.Target;
            }

            _top = null;
            _bottom = null;
            _left = null;
            _right = null;
            Component.Remove(this);
            Component = null;
        }

        internal MeshPoint NextNonOrientedTarget()
        {
            foreach (var edge in getEdges())
            {
                if (edge != null && edge.NeedsOrientation)
                    return edge.Target;
            }

            return null;
        }

        internal MeshPoint GetOtherNeighbour(MeshPoint incomingPoint)
        {
            foreach (var edge in getEdges())
            {
                if (edge != null && edge.Target != incomingPoint)
                    return edge.Target;
            }

            throw new NotImplementedException();
        }

        internal void OrientEdgeTo(MeshPoint target)
        {
            this.setEdgeValue(target, false);
            target.setEdgeValue(this, true);
        }

        private void setEdgeValue(MeshPoint target, bool isIncomming)
        {
            foreach (var edge in getEdges())
            {
                if (edge != null && edge.Target == target)
                {
                    edge.IsIncomming = isIncomming;
                    return;
                }
            }

            throw new KeyNotFoundException("target has to be present among edges");
        }

        private MeshEdge[] getEdges()
        {
            return new[] { _top, _bottom, _right, _left };
        }
    }

    class MeshEdge
    {
        internal MeshPoint Target;

        internal bool? IsIncomming;

        internal bool NeedsOrientation { get { return !IsIncomming.HasValue; } }
    }

    class MeshComponent
    {
        private readonly HashSet<MeshPoint> _points = new HashSet<MeshPoint>();

        public IEnumerable<MeshPoint> NonOrientedPoints { get { return _points.Where(p => p.NeedsOrientation); } }

        public IEnumerable<MeshPoint> Points { get { return _points; } }

        internal int Size { get { return _points.Count; } }

        internal void Accept(MeshComponent component)
        {
            foreach (var point in component._points)
            {
                Add(point);
            }

            component._points.Clear();
        }

        internal void Remove(MeshPoint point)
        {
            if (!_points.Remove(point))
                throw new NotImplementedException();
        }

        internal void Add(MeshPoint point)
        {
            if (!_points.Add(point))
                throw new NotImplementedException("point is already present");

            point.Component = this;
        }
    }
}
