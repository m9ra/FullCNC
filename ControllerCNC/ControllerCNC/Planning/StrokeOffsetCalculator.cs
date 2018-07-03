using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ControllerCNC.Planning
{
    public class StrokeOffsetCalculator
    {
        private readonly List<Point[]> _clusters;

        private readonly List<ShapeNode> _shapeNodes;

        internal readonly double Precision = 0.1;

        internal readonly double Tolerance = 0.01;

        public StrokeOffsetCalculator(IEnumerable<Point2Dmm[]> clusters)
        {
            _clusters = clusters.Select(c => filter(c.Select(OffsetCalculator.AsPoint)).ToArray()).ToList();

            _shapeNodes = ShapeNode.From(_clusters).ToList();
        }

        private IEnumerable<Point> filter(IEnumerable<Point> enumerable)
        {
            var points = enumerable.ToArray();
            var result = new List<Point>();
            for (var i = 0; i < points.Length - 1; ++i)
            {
                var p1 = points[i];
                var p2 = points[i + 1];

                if (p1 == p2)
                    continue;

                result.Add(p1);
            }

            result.Add(result.First());
            return result;
        }

        public IEnumerable<Point2Dmm[]> WithOffset(double offset)
        {
            var allNodes = _shapeNodes.Concat(_shapeNodes.SelectMany(n => n.OrderedChildren));

            var resultClusters = new List<Point[]>();
            foreach (var node in allNodes)
            {
                var strokes = new List<StrokeLine>();

                for (var i = 0; i < node.Points.Length - 1; ++i)
                {
                    var p1 = node.Points[i];
                    var p2 = node.Points[i + 1];

                    var lineStrokes = getStrokes(p1, p2, node, offset);
                    strokes.AddRange(lineStrokes);
                }

                resultClusters.AddRange(getClusters(strokes, node, offset));
            }

            return resultClusters.Select(c => c.Select(OffsetCalculator.AsPoint2D).ToArray());
        }

        private IEnumerable<Point[]> getClusters(IEnumerable<StrokeLine> strokes, ShapeNode node, double offset)
        {
            var remainingStrokes = new List<StrokeLine>(strokes);

            var strokeComponents = new List<StrokeLine[]>();
            var currentStrokeComponent = new List<StrokeLine>();

            while (remainingStrokes.Any())
            {
                if (currentStrokeComponent.Count == 0)
                {
                    //initialize
                    currentStrokeComponent.Add(remainingStrokes[0]);
                    remainingStrokes.RemoveAt(0);
                }

                //remainingStrokes.Sort((a, b) => strokeDistanceComparer(a, b, currentStrokeComponent));

                var hasChange = false;
                for (var i = 0; i < remainingStrokes.Count; ++i)
                {
                    if (tryAddComponent(currentStrokeComponent, remainingStrokes[i], node, offset))
                    {
                        remainingStrokes.RemoveAt(i);
                        hasChange = true;
                        break;
                    }
                }

                if (!hasChange || !remainingStrokes.Any())
                {
                    strokeComponents.Add(currentStrokeComponent.ToArray());
                    currentStrokeComponent.Clear();
                }
            }


            return strokeComponents.Select(strokeToPoints);
        }

        private bool tryAddComponent(List<StrokeLine> currentStrokeComponent, StrokeLine strokeLine, ShapeNode node, double offset)
        {
            var s = currentStrokeComponent.First().S;
            var e = currentStrokeComponent.Last().E;
            
            if (d(strokeLine.E, s) <= d(e, strokeLine.S))
            {
                if (getStrokes(strokeLine.E, s, node, offset - Tolerance, false).Count() != 1)
                    return false;

                currentStrokeComponent.Insert(0, strokeLine);
            }
            else
            {
                if (getStrokes(e, strokeLine.S, node, offset - Tolerance, false).Count() != 1)
                    return false;

                currentStrokeComponent.Add(strokeLine);
            }

            return true;
        }

        private int strokeDistanceComparer(StrokeLine a, StrokeLine b, List<StrokeLine> currentStrokeComponent)
        {
            var aDistance = strokeDistance(a, currentStrokeComponent);
            var bDistance = strokeDistance(b, currentStrokeComponent);

            if (aDistance == bDistance)
                return 0;

            return aDistance > bDistance ? 1 : -1;
        }

        private double strokeDistance(StrokeLine a, List<StrokeLine> component)
        {
            var s = component.First().S;
            var e = component.Last().E;

            return Math.Min(d(a.E, s), d(e, a.S));
        }

        private double d(StrokeLine a, Point b)
        {
            return Math.Min(d(a.S, b), d(a.E, b));
        }

        private double d(Point a, Point b)
        {
            return (a - b).Length;
        }

        private Point[] strokeToPoints(StrokeLine[] strokes)
        {
            var result = new List<Point>();
            foreach (var stroke in strokes)
            {
                result.Add(stroke.S);
                result.Add(stroke.E);
            }

            result.Add(strokes[0].S);

            return result.ToArray();
        }

        private IEnumerable<StrokeLine> getStrokes(Point p1, Point p2, ShapeNode node, double offset, bool elevate = true)
        {
            var v = p2 - p1;
            var n = new Vector(v.Y, -v.X);
            n.Normalize();
            var elevation = elevate ? offset : 0;
            var np1 = p1 - n * elevation;
            var np2 = p2 - n * elevation;

            var result = new List<StrokeLine>();
            if (v.Length < Tolerance)
            {
                result.Add(new StrokeLine(p1, p2));
                return result;
            }

            var stepCount = v.Length / Precision;
            bool hasStart = false;
            Point start = new Point();


            for (var i = 0; i < stepCount; ++i)
            {
                var tp = np1 + v * i / stepCount;
                var isValid = isPointValid(tp, node, offset);

                if (isValid && !hasStart)
                {
                    hasStart = true;
                    start = tp;
                }

                if (!isValid && hasStart)
                {
                    hasStart = false;
                    var lastValid = np1 + v * (i - 1) / stepCount;
                    result.Add(new StrokeLine(start, lastValid));
                }
            }

            if (hasStart)
            {
                result.Add(new StrokeLine(start, np2));
            }

            return result;
        }

        private bool isPointValid(Point p, ShapeNode node, double offset)
        {
            var distance = node.InShapeDistance(p);
            return distance > 0 && distance >= offset;
        }
    }

    class StrokeLine
    {
        public readonly Point S;

        public readonly Point E;

        internal StrokeLine(Point s, Point e)
        {
            S = s;
            E = e;
        }

        public override string ToString()
        {
            return $"{S.X:0.000},{S.Y:0.000} {E.X:0.000}, {E.Y:0.000}";
        }
    }
}
