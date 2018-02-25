using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ControllerCNC.Planning
{
    public class OffsetCalculator
    {
        private readonly Point[] _points;

        private readonly bool _isClosedShape;

        private readonly bool _createVisualLog;

        private readonly List<DrawEvent[]> _updateSnapshots = new List<DrawEvent[]>();

        private readonly Pen _redPen = new Pen(Brushes.Red, 2);

        private readonly Pen _aboveLinePen = new Pen(Brushes.DarkCyan, 2);

        private readonly Pen _aboveLineHighlightPen = new Pen(Brushes.Cyan, 2);

        private readonly Pen _originalPen = new Pen(Brushes.Black, 2);

        private readonly Pen _bodyPen = new Pen(Brushes.Gray, 2);

        private readonly Pen _resultPen = new Pen(Brushes.Blue, 2);

        private readonly Pen _basicRulePen = new Pen(Brushes.Red, 1);

        private readonly Pen _appendRulePen = new Pen(Brushes.Orange, 2);

        public OffsetCalculator(IEnumerable<Point2Dmm> points, bool createVisualLog = false)
        {
            _points = points.Select(asPoint).ToArray();
            _isClosedShape = _points.First().Equals(_points.Last());

            if (!_isClosedShape)
                throw new NotImplementedException();

            _points = _points.Distinct().ToArray();
            _createVisualLog = createVisualLog;
        }

        public IEnumerable<Point2Dmm[]> WithOffset(double offset)
        {
            var points = new List<Point>(_points);
            var bisectors = new List<Point>(_points);

            logVisualSnapshot(points, null);
            updateAllBisectors(points, bisectors, offset);
            logVisualSnapshot(points, bisectors);
            resolveLocalProblems(points, bisectors, offset);
            logVisualSnapshot(points, bisectors);
            closeShape(points, bisectors);
            logVisualSnapshot(points, bisectors);
            var result = new List<Point2Dmm[]>();

            /*/
            result.Add(bisectors.Select(asPoint2D).ToArray());
            /*/
            if (bisectors.Any())
            {
                var lines = getValidOffsetLines(points, bisectors, offset);
                logVisualSnapshot(lines);
                result.AddRange(lines.Select(l => l.Select(asPoint2D).ToArray()));
            }
            /**/


            return result;
        }

        public IEnumerable<DrawEvent[]> GetUpdateSnapshots()
        {
            return _updateSnapshots;
        }

        private bool canDebug()
        {
            return _createVisualLog;
        }


        private void logVisualSnapshot(IEnumerable<Point[]> lines)
        {
            if (!canDebug())
                return;

            var result = new List<DrawEvent>();
            result.AddRange(createPolyLineLog(_points.ToList(), _originalPen));
            foreach (var line in lines)
                result.AddRange(createPolyLineLog(line.ToList(), _bodyPen));

            _updateSnapshots.Add(result.ToArray());
        }

        private void logInvalidPoint(Point[] line, List<Point> points, Point invalidPoint)
        {
            if (!canDebug())
                return;

            var events = new List<DrawEvent>();
            events.AddRange(createPolyLineLog(points, _originalPen));
            events.AddRange(createPolyLineLog(line.ToList(), _redPen));
            events.Add(new CircleEvent(invalidPoint, 5, _bodyPen));

            _updateSnapshots.Add(events.ToArray());
        }

        private void logIntefereSelfIntersections(List<Point> points, List<Point> bisectors, IEnumerable<Intersection> intersections)
        {
            var events = createVisualSnapshot(points, bisectors);
            foreach (var intersection in intersections)
            {
                var sPoint1 = getPoint(intersection.S, bisectors);
                var sPoint2 = getPoint(intersection.S + 1, bisectors);
                var ePoint1 = getPoint(intersection.E, bisectors);
                var ePoint2 = getPoint(intersection.E + 1, bisectors);

                var intersectionPoint = sPoint1 + (sPoint2 - sPoint1) * intersection.RatioS;
                events.Add(new LineEvent(sPoint1, sPoint2, _aboveLineHighlightPen));
                events.Add(new LineEvent(ePoint1, ePoint2, _aboveLineHighlightPen));
                events.Add(new CircleEvent(intersectionPoint, 5, _redPen));
                events.Add(new CircleEvent(intersectionPoint, 8, _redPen));
            }
            _updateSnapshots.Add(events.ToArray());
        }

        private void logSelfIntersections(List<Point> points, List<Point> bisectors, IEnumerable<Intersection> intersections)
        {
            var events = createVisualSnapshot(points, bisectors);
            foreach (var intersection in intersections)
            {
                var sPoint1 = getPoint(intersection.S, bisectors);
                var sPoint2 = getPoint(intersection.S + 1, bisectors);
                var ePoint1 = getPoint(intersection.E, bisectors);
                var ePoint2 = getPoint(intersection.E + 1, bisectors);

                var intersectionPoint = sPoint1 + (sPoint2 - sPoint1) * intersection.RatioS;
                events.Add(new LineEvent(sPoint1, sPoint2, _aboveLineHighlightPen));
                events.Add(new LineEvent(ePoint1, ePoint2, _aboveLineHighlightPen));
                events.Add(new CircleEvent(intersectionPoint, 5, _redPen));
            }
            _updateSnapshots.Add(events.ToArray());
        }

        private void logUpdateRule(Point center, double offset, List<Point> points, List<Point> bisectors, bool isBasicUpdate)
        {
            if (!canDebug())
                return;

            var events = createVisualSnapshot(points, bisectors);
            var pen = isBasicUpdate ? _basicRulePen : _appendRulePen;
            events.Add(new CircleEvent(center, offset, pen));

            _updateSnapshots.Add(events.ToArray());
        }

        private void logAboveLine(Point testedPoint, Point[] aboveLine, Point p1, Point p2)
        {
            if (!canDebug())
                return;

            var result = new List<DrawEvent>(_updateSnapshots.Last());

            result.AddRange(createPolyLineLog(aboveLine.ToList(), _aboveLinePen));
            result.Add(createPointLog(testedPoint, _redPen));
            result.Add(new LineEvent(p1, p2, _aboveLineHighlightPen));
            _updateSnapshots.Add(result.ToArray());
        }

        private void logVisualSnapshot(List<Point> points, List<Point> bisectors)
        {
            if (!canDebug())
                return;

            var events = createVisualSnapshot(points, bisectors);

            _updateSnapshots.Add(events.ToArray());
        }

        private List<DrawEvent> createVisualSnapshot(List<Point> points, List<Point> bisectors)
        {
            var bisectorPen = new Pen(Brushes.Green, 2);
            var result = new List<DrawEvent>();

            result.AddRange(createPolyLineLog(_points.ToList(), _originalPen));

            result.AddRange(createPolyLineLog(points, _bodyPen));

            if (bisectors != null)
            {
                result.AddRange(createPolyLineLog(bisectors, _resultPen));
                for (var i = 0; i < points.Count; ++i)
                {
                    var point = points[i];
                    var bisector = bisectors[i];

                    result.Add(new LineEvent(point, bisector, bisectorPen));
                }
            }
            return result;
        }

        private IEnumerable<DrawEvent> createPolyLineLog(List<Point> points, Pen pen)
        {
            var result = new List<DrawEvent>();

            for (var i = 0; i < points.Count - 1; ++i)
            {
                result.Add(new LineEvent(points[i], points[i + 1], pen));
            }

            return result;
        }

        private DrawEvent createPointLog(Point point, Pen pen)
        {
            return new CircleEvent(point, 1, pen);
        }

        private void closeShape(List<Point> points, List<Point> bisectors)
        {
            if (points.Count > 0 && !points.First().Equals(points.Last()))
            {
                points.Add(points.First());
                bisectors.Add(bisectors.First());
            }
        }

        private void updateAllBisectors(List<Point> points, List<Point> bisectors, double offset)
        {
            for (var k = 0; k < points.Count; ++k)
            {
                updateBisector(k, bisectors, points, offset);
            }
        }

        private IEnumerable<Point[]> getValidOffsetLines(List<Point> points, List<Point> bisectors, double offset)
        {
            var result = new List<Point[]>();
            var intersections = getSelfIntersections(bisectors);
            logSelfIntersections(points, bisectors, intersections);

            if (!intersections.Any())
            {
                //the whole shape is valid or invalid
                if (isPointValid(bisectors.First(), points, offset))
                    result.Add(bisectors.ToArray());

                return result;
            }

            var interferentialIntersections = new HashSet<Intersection>();
            foreach (var i1 in intersections)
            {
                foreach (var i2 in intersections)
                {
                    if (i1.Interfere(i2))
                    {
                        interferentialIntersections.Add(i1);
                        interferentialIntersections.Add(i2);
                    }
                }
            }

            if (interferentialIntersections.Count > 0)
            {
                logSelfIntersections(points, bisectors, intersections);
                var inteferentialGroups = getGroups(interferentialIntersections);
                logIntefereSelfIntersections(points, bisectors, interferentialIntersections);
                //TODO remove carefuly
                return result;
            }

            var orderedIntersections = intersections.OrderBy(i => i.SValue).ToArray();
            var validIndex = getValidIndex(orderedIntersections.First(), points, bisectors, offset);
            if (validIndex < 0)
                return result;

            rearange(orderedIntersections, validIndex, points);
            points = rearange(points, validIndex);
            bisectors = rearange(bisectors, validIndex);

            var lines = collectLines(orderedIntersections, bisectors);

            /*/
            result.AddRange(lines);
            /*/
            foreach (var line in lines)
            {
                if (isLineValid(line, _points.ToList(), offset))
                    result.Add(line);
            }
            /**/

            return result;
        }

        private IEnumerable<Intersection[]> getGroups(HashSet<Intersection> interferentialIntersections)
        {
            var remainingIntersections = interferentialIntersections.ToList();
            var result = new List<Intersection[]>();
            while (remainingIntersections.Count > 0)
            {
                var group = new List<Intersection>();

                for (var i = 0; i < remainingIntersections.Count; ++i)
                {
                    var intersection = remainingIntersections[i];

                    if (group.Count == 0 || group.Any(gint => gint.Interfere(intersection) || intersection.Interfere(gint)))
                    {
                        group.Add(intersection);
                        remainingIntersections.RemoveAt(i);
                        i = -1;
                    }
                }

                result.Add(group.ToArray());
            }

            return result;
        }

        private bool isLineValid(Point[] line, List<Point> points, double offset)
        {
            /**/
            foreach (var point in line)
            {
                if (!isPointValid(point, points, offset))
                {
                    logInvalidPoint(line, points, point);
                    return false;
                }
            }
            return true;
            /*/
            return GeometryUtils.ArePointsClockwise(line.Select(asPoint2D));
            /**/
        }

        private IEnumerable<Point[]> collectLines(Intersection[] intersections, List<Point> bisectors)
        {
            var result = new List<Point[]>();
            var rootNode = new Intersection(0, 0, bisectors.Count - 1, 1);

            rootNode.BuildTree(intersections);

            Debug.WriteLine("START");
            collectLines2(rootNode, 0, bisectors, result);
            Debug.WriteLine("END");
            foreach (var intersection in intersections)
            {
                Debug.WriteLine(intersection);
            }

            return result;
        }

        private void collectLines2(Intersection node, int depth, List<Point> bisectors, List<Point[]> result)
        {
            var layerIntersections = new List<Intersection>();
            layerIntersections.Add(node);
            layerIntersections.AddRange(node.Children);
            layerIntersections.Add(node);

            var layerPoints = new List<Point>();
            for (var k = 0; k < layerIntersections.Count - 1; ++k)
            {
                var isFirst = k == 0;
                var isLast = k == layerIntersections.Count - 2;
                var intersection = layerIntersections[k];
                var SIk = getIntersectionPoint(intersection, bisectors);

                int startIndex, endIndex;
                if (isFirst)
                {
                    startIndex = layerIntersections[k].S;
                }
                else
                {
                    startIndex = layerIntersections[k].E;
                }

                if (isLast)
                {
                    endIndex = layerIntersections[k + 1].E;
                }
                else
                {
                    endIndex = layerIntersections[k + 1].S;
                }

                layerPoints.Add(SIk);
                for (var i = startIndex + 1; i <= endIndex; ++i)
                {
                    layerPoints.Add(bisectors[i]);
                }
            }

            layerPoints.Add(layerPoints.First());

            if (depth % 2 == 0)
                result.Add(layerPoints.ToArray());

            foreach (var child in node.Children)
            {
                collectLines2(child, depth + 1, bisectors, result);
            }
        }

        private void collectLines(Intersection node, int depth, List<Point> bisectors, List<Point[]> result)
        {
            System.Diagnostics.Debug.WriteLine(depth + " " + node);

            var children = node.Children;
            if (children.Any())
            {
                //points to all children
                foreach (var child in children)
                {
                    var points = new List<Point>();

                    //currentNode is supposed to be parent of current child (duty of caller)
                    if (!node.Contains(child))
                        throw new NotSupportedException("invalid operator");


                    points.Add(getIntersectionPoint(node, bisectors));

                    for (var i = node.S + 1; i <= child.S; ++i)
                    {
                        points.Add(bisectors[i]);
                    }

                    points.Add(getIntersectionPoint(child, bisectors));

                    for (var i = child.E + 1; i <= node.E; ++i)
                    {
                        points.Add(bisectors[i]);
                    }
                    points.Add(points.First());
                    result.Add(points.ToArray());
                }
            }
            else
            {
                // no children case
                var points = new List<Point>();
                points.Add(getIntersectionPoint(node, bisectors));
                for (var i = node.S + 1; i <= node.E; ++i)
                {
                    points.Add(bisectors[i]);
                }
                points.Add(points.First());
                result.Add(points.ToArray());
            }

            foreach (var child in children)
            {
                collectLines(child, depth + 1, bisectors, result);
            }
        }

        private void reportDepth(int currentDepth, Intersection currentNode, ref int currentIndex, Intersection[] orderedIntersections, List<Point> bisectors, List<Point[]> result)
        {
            ++currentIndex;
            System.Diagnostics.Debug.WriteLine(currentDepth + " " + currentNode);

            if (currentDepth % 2 == 0)
            {
                //collect points at every even depth
                var points = new List<Point>();

                if (currentIndex >= orderedIntersections.Length)
                {
                    points.Add(getIntersectionPoint(currentNode, bisectors));
                    for (var i = currentNode.S + 1; i <= currentNode.E; ++i)
                    {
                        points.Add(bisectors[i]);
                    }
                    points.Add(points.First());
                    result.Add(points.ToArray());
                    return;
                }
                var currentChild = orderedIntersections[currentIndex];
                //currentNode is supposed to be parent of current child (duty of caller)
                if (!currentNode.Contains(currentChild))
                    throw new NotSupportedException("invalid operator");


                points.Add(getIntersectionPoint(currentNode, bisectors));

                for (var i = currentNode.S + 1; i <= currentChild.S; ++i)
                {
                    points.Add(bisectors[i]);
                }

                points.Add(getIntersectionPoint(currentChild, bisectors));

                for (var i = currentChild.E + 1; i <= currentNode.E; ++i)
                {
                    points.Add(bisectors[i]);
                }
                points.Add(points.First());
                result.Add(points.ToArray());
            }


            ++currentIndex;
            while (currentIndex < orderedIntersections.Length - 1)
            {
                var nextNode = orderedIntersections[currentIndex];
                if (!currentNode.Contains(nextNode))
                    break;

                reportDepth(currentDepth + 1, nextNode, ref currentIndex, orderedIntersections, bisectors, result);
            }
        }

        private Point getIntersectionPoint(Intersection intersection, List<Point> bisectors)
        {
            var s1 = getPoint(intersection.S, bisectors);
            var s2 = getPoint(intersection.S + 1, bisectors);
            var v = s2 - s1;
            return v * intersection.RatioS + s1;
        }

        private List<Point> rearange(List<Point> points, int validIndex)
        {
            if (!points.First().Equals(points.Last()))
                throw new NotSupportedException("Can rearange only closed points");

            points.RemoveAt(points.Count - 1);

            var newPoints = points.Skip(validIndex).Concat(points.Take(validIndex)).ToList();
            newPoints.Add(newPoints.First()); //close shape

            return newPoints;
        }

        private void rearange(Intersection[] orderedIntersections, int validIndex, List<Point> points)
        {
            for (var i = 0; i < orderedIntersections.Length; ++i)
            {
                orderedIntersections[i] = orderedIntersections[i].Rearange(validIndex, points);
            }
        }

        private int getValidIndex(Intersection intersection, List<Point> points, List<Point> bisectors, double offset)
        {
            if (isPointValid(getPoint(intersection.S, bisectors), points, offset))
                return intersection.S;

            if (isPointValid(getPoint(intersection.S + 1, bisectors), points, offset))
                return getIndex(intersection.S + 1, points);

            if (isPointValid(getPoint(intersection.E, bisectors), points, offset))
                return intersection.E;

            if (isPointValid(getPoint(intersection.E + 1, bisectors), points, offset))
                return getIndex(intersection.E + 1, points);

            return -1;
        }


        private IEnumerable<Intersection> getSelfIntersections(List<Point> bisectors)
        {
            var result = new List<Intersection>();
            for (var i = 0; i < bisectors.Count; ++i)
            {
                var s1 = getPoint(i, bisectors);
                var s2 = getPoint(i + 1, bisectors);

                for (var j = i + 2; j < bisectors.Count - 1; ++j)
                {
                    var e1 = getPoint(j, bisectors);
                    var e2 = getPoint(j + 1, bisectors);

                    //FindIntersection(s1, s2, e1, e2, out _, out var hasIntersection, out var iP, out _, out _);
                    var hasIntersection = GeometryUtils.LineSegementsIntersect(s1, s2, e1, e2, out var iP, considerCollinearOverlapAsIntersect: true);

                    if (hasIntersection)
                    {
                        var ratioS = (s1 - iP).Length / (s1 - s2).Length;
                        var ratioE = (e1 - iP).Length / (e1 - e2).Length;
                        var intersection = new Intersection(i, ratioS, j, ratioE);
                        if (!GeometryUtils.IsZero(ratioS))
                            //previous line was touched
                            result.Add(intersection);
                    }
                }
            }

            return result;
        }

        private bool isPointValid(Point testedPoint, List<Point> points, double offset)
        {
            for (var i = 0; i < points.Count - 1; ++i)
            {
                var p1 = points[i];
                var p2 = points[i + 1];

                var segmentDistance = FindDistanceToSegment(testedPoint, p1, p2, out _);
                //TODO for some reason there is a big numerical error
                if (segmentDistance + 1e-1 < offset)
                    return false;
            }
            return PointInPolygon(testedPoint, points.ToArray());
        }

        private void updateBisectors(int i, List<Point> bisectors, List<Point> points, double offset)
        {
            updateBisector(i - 2, bisectors, points, offset);
            updateBisector(i - 1, bisectors, points, offset);
            updateBisector(i, bisectors, points, offset);
            updateBisector(i + 1, bisectors, points, offset);
            updateBisector(i + 2, bisectors, points, offset);
            updateBisector(i + 3, bisectors, points, offset);
        }

        private void updateBisector(int i, List<Point> bisectors, List<Point> points, double offset)
        {
            if (points.Count == 0)
                return;

            var p_prev = getPoint(i - 1, points);
            var p_i = getPoint(i, points);
            var p_next = getPoint(i + 1, points);
            if (calculateBisector(p_prev, p_i, p_next, offset, out var bisector))
            {
                setPoint(bisectors, i, bisector);
            }
            else
            {
                remove(i, bisectors, points);
                updateBisector(i - 1, bisectors, points, offset);
                updateBisector(i, bisectors, points, offset);
                updateBisector(i + 1, bisectors, points, offset);
            }
        }

        private Point[] resolveLocalProblems(List<Point> points, List<Point> bisectors, double offset)
        {
            for (var k = 0; k < points.Count && points.Count > 3; ++k)
            {
                var pk_m1 = getPoint(k - 1, points);
                var pk = getPoint(k, points);
                var ok = getPoint(k, bisectors);
                var pk_1 = getPoint(k + 1, points);
                var ok_1 = getPoint(k + 1, bisectors);
                var pk_2 = getPoint(k + 2, points);

                var hasIntersection = LineSegementsIntersect(pk, ok, pk_1, ok_1, out var intersection);
                if (!hasIntersection)
                    //no local problem here
                    continue;

                var center = getStuckCircleCenter(pk_m1, pk, pk_1, pk_2, offset);
                if (center.HasValue)
                {
                    //basic update rule
                    logUpdateRule(center.Value, offset, points, bisectors, true);
                    var pc = getPc(center.Value, pk_m1, pk, pk_1, pk_2, offset);
                    basicUpdateRule(k, pc, pk_m1, pk, pk_1, pk_2, offset, points, bisectors);
                    k -= 2;
                }
                else
                {
                    //append update rule
                    if (appendUpdateRule(k, pk_m1, pk, pk_1, pk_2, offset, points, bisectors))
                        k -= 2;
                }
            }

            return bisectors.ToArray();
        }

        private bool appendUpdateRule(int k, Point pk_m1, Point pk, Point pk_1, Point pk_2, double offset, List<Point> points, List<Point> bisectors)
        {
            var v1 = pk - pk_m1;
            var v2 = pk - pk_1;
            var angle1 = GeometryUtils.NormalizedAngleBetween(v1, v2);

            var v3 = pk_1 - pk;
            var v4 = pk_1 - pk_2;
            var angle2 = GeometryUtils.NormalizedAngleBetween(v3, v4);

            if (softSE(angle1, 180) && softGE(angle1, 0))
            {
                var pc = getAppendPc(pk_m1, pk, pk_1, offset, points, bisectors);
                if (setPoint(points, k, pc))
                {
                    updateBisectors(k, bisectors, points, offset);
                }
            }

            if (softSE(angle2, 180) && softGE(angle2, 0))
            {
                var pc = getAppendPc(pk_2, pk_1, pk, offset, points, bisectors);
                if (setPoint(points, k + 1, pc))
                {
                    updateBisectors(k + 1, bisectors, points, offset);
                }
            }

            return false;
        }

        private void basicUpdateRule(int k, Point pc, Point pk_m1, Point pk, Point pk_1, Point pk_2, double offset, List<Point> points, List<Point> bisectors)
        {
            outputGeogebraPoints(new Point[]
            {
                pk_m1,
                pk,
                pk_1,
                pk_2
            });
            setPoint(points, k, pc);
            var pe = getPoint(k + 1, points);
            remove(k + 1, points, bisectors);

            var extensionVector = pk_m1 - getPoint(k + 1, points);
            var ps = pk_m1 + extensionVector;

            while (points.Count > 3)
            {
                var needsRemoving = isAbovePolyline(getPoint(k + 2, points),
                    new[] { ps, getPoint(k - 1, points), getPoint(k, points), getPoint(k + 1, points), pe });

                if (!needsRemoving)
                    break;

                remove(k + 2, points, bisectors);
                logVisualSnapshot(points, bisectors);
            }

            updateBisectors(k, bisectors, points, offset);
        }

        private void outputGeogebraPoints(IEnumerable<Point> points)
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
            Debug.WriteLine("");
            Debug.WriteLine("");
            var counter = 1;
            foreach (var point in points)
            {
                Debug.WriteLine($"P{counter}=({point.X:0.000},{point.Y:0.000})");
                ++counter;
            }
            Debug.WriteLine("");
        }

        private bool isAbovePolyline(Point p, Point[] polyline)
        {
            outputGeogebraPoints(polyline);
            var shortestSegmentDistance = double.PositiveInfinity;
            var shortestPointDistance = double.PositiveInfinity;
            var nearestSegmentId = 0;
            var nearestPointId = 0;
            for (var i = 0; i < polyline.Length - 1; ++i)
            {
                var p1 = polyline[i];
                var p2 = polyline[i + 1];

                var pointDistance = (p - p1).Length;
                var segmentDistance = FindDistanceToSegment(p, p1, p2, out _);
                if (segmentDistance < shortestSegmentDistance)
                {
                    nearestSegmentId = i;
                    shortestSegmentDistance = segmentDistance;
                }

                if (pointDistance < shortestPointDistance)
                {
                    nearestPointId = i;
                    shortestPointDistance = pointDistance;
                }
            }

            var bp1 = polyline[nearestSegmentId];
            var bp2 = polyline[nearestSegmentId + 1];
            logAboveLine(p, polyline, bp1, bp2);

            if (shortestPointDistance - shortestSegmentDistance <= 1e-3)
                //when polyline vertex is closer to the point, segment calculation is imprecise
                return isAbovePolylineVertex(p, polyline, nearestPointId);

            var v1 = bp2 - bp1;
            var nv1 = new Vector(v1.Y, -v1.X);
            var v2 = p - bp1;
            //var angle = GeometryUtils.NormalizedAngleBetween(v1, v2);
            var dotProduct = Vector.Multiply(nv1, v2);
            return dotProduct < 0;
        }

        private bool isAbovePolylineVertex(Point p, Point[] polyline, int nearestPointId)
        {
            var p0 = polyline[0];
            var p1 = polyline[1];
            var preP0 = p0 - (p1 - p0);

            var pN = polyline[polyline.Length - 1];
            var pNm1 = polyline[polyline.Length - 2];
            var postN = pN - (pNm1 - pN);

            var extendedPolyline = new List<Point>();
            extendedPolyline.Add(preP0);
            extendedPolyline.AddRange(polyline);
            extendedPolyline.Add(postN);

            //due to extension, the nearestPointId is shifted with +1
            var em1 = extendedPolyline[nearestPointId];
            var e = extendedPolyline[nearestPointId + 1];
            var e1 = extendedPolyline[nearestPointId + 2];

            var v1 = em1 - e;
            var v2 = e - e1;
            var vp1 = p - e;
            var vp2 = p - e1;

            var nv1 = new Vector(v1.Y, -v1.X);
            var nv2 = new Vector(v2.Y, -v2.X);

            var isPlane1Ok = Vector.Multiply(nv1, vp1) < 0;
            var isPlane2Ok = Vector.Multiply(nv2, vp2) < 0;

            var polylineAngle = Vector.Multiply(em1 - e, e1 - e);
            if (polylineAngle >= 0)
            {
                return isPlane1Ok && isPlane2Ok;
            }
            else
            {
                return isPlane1Ok || isPlane2Ok;
            }
        }

        private Point getAppendPc(Point l0, Point l1, Point l2, double offset, List<Point> points, List<Point> bisectors)
        {
            var box1 = new SegmentBox(l0, l1, offset, p1Circle: true, p2Circle: true);
            var box2 = new SegmentBox(l1, l2, offset, p1Circle: true, p2Circle: true);

            var intersections = box1.GetOffsetIntersections(box2).OrderBy(p => (l2 - p).Length).ToArray();
            var circleCenter = intersections.First();
            var distance = FindDistanceToSegment(circleCenter, l0, l1, out var touchPoint);
            var v1 = touchPoint - circleCenter;
            var nv1 = new Vector(v1.Y, -v1.X);

            FindIntersection(touchPoint, touchPoint + nv1, l1, l2, out var _1, out var _2, out var pc, out var _3, out var _4);

            logUpdateRule(circleCenter, offset, points, bisectors, false);
            return pc;
        }

        private void remove(int k, List<Point> bisectors, List<Point> points)
        {
            while (k < 0)
                k += points.Count;
            k = k % points.Count;

            bisectors.RemoveAt(k);
            points.RemoveAt(k);
        }


        private Point getPc(Point stuckCircleCenter, Point pk_m1, Point pk, Point pk_1, Point pk_2, double offset)
        {
            //throw new NotImplementedException();
            var distance1 = FindDistanceToSegment(stuckCircleCenter, pk_m1, pk, out var s1);
            var distance2 = FindDistanceToSegment(stuckCircleCenter, pk_1, pk_2, out var s2);

            if (Math.Abs(distance1 - offset) > 1e-4)
                throw new NotImplementedException();

            if (Math.Abs(distance2 - offset) > 1e-4)
                throw new NotImplementedException();

            var v1 = stuckCircleCenter - s1;
            var v2 = stuckCircleCenter - s2;

            var nv1 = new Vector(v1.Y, -v1.X);
            var nv2 = new Vector(v2.Y, -v2.X);

            nv1.Normalize();
            nv2.Normalize();

            FindIntersection(s1, s1 + nv1, s2, s2 + nv2, out var linesIntersect, out var _, out var pc, out var _1, out var _2);
            if (!linesIntersect)
                throw new NotImplementedException();
            return pc;
        }

        private Point? getStuckCircleCenter(Point pk_m1, Point pk, Point pk_1, Point pk_2, double offset)
        {
            var box1 = new SegmentBox(pk_m1, pk, offset, p1Circle: true, p2Circle: false);
            var box2 = new SegmentBox(pk_2, pk_1, offset, p1Circle: true, p2Circle: false);

            var intersections = box1.GetOffsetIntersections(box2);
            var stuckCandidates = intersections.OrderByDescending(p => FindDistanceToSegment(p, pk, pk_1, out var _)).ToArray();

            if (stuckCandidates.Length == 0)
                return null;

            var candidate = stuckCandidates.First();

            var distance = FindDistanceToSegment(candidate, pk, pk_1, out var touchPoint);
            if (distance <= offset + 1e-4)
                //circle is not stucked
                return null;

            return candidate;
        }

        private bool testStuckCircle(Point pk_m1, Point pk, Point pk_1, Point pk_2, double offset)
        {
            var center = getStuckCircleCenter(pk_m1, pk, pk_1, pk_2, offset);
            return center.HasValue;
        }

        public bool PointInPolygon(Point p, Point[] points)
        {
            // Get the angle between the point and the
            // first and last vertices.
            var max_point = points.Length - 1;
            var total_angle = GetAngle(points[max_point], p, points[0]);

            // Add the angles from the point
            // to each other pair of vertices.
            for (var i = 0; i < max_point; i++)
            {
                total_angle += GetAngle(points[i], p, points[i + 1]);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            return (Math.Abs(total_angle) > 0.000001);
        }

        // Return the angle ABC.
        // Return a value between PI and -PI.
        // Note that the value is the opposite of what you might
        // expect because Y coordinates increase downward.
        public static double GetAngle(Point a, Point b, Point c)
        {
            // Get the dot product.
            var dot_product = DotProduct(a, b, c);

            // Get the cross product.
            var cross_product = CrossProductLength(a, b, c);

            // Calculate the angle.
            return Math.Atan2(cross_product, dot_product);
        }

        public static double CrossProductLength(Point a, Point b, Point c)
        {
            var Ax = a.X;
            var Ay = a.Y;
            var Bx = b.X;
            var By = b.Y;
            var Cx = c.X;
            var Cy = c.Y;

            // Get the vectors' coordinates.
            var BAx = Ax - Bx;
            var BAy = Ay - By;
            var BCx = Cx - Bx;
            var BCy = Cy - By;

            // Calculate the Z coordinate of the cross product.
            return (BAx * BCy - BAy * BCx);
        }

        private static double DotProduct(Point a, Point b, Point c)
        {
            var Ax = a.X;
            var Ay = a.Y;
            var Bx = b.X;
            var By = b.Y;
            var Cx = c.X;
            var Cy = c.Y;
            // Get the vectors' coordinates.
            var BAx = Ax - Bx;
            var BAy = Ay - By;
            var BCx = Cx - Bx;
            var BCy = Cy - By;

            // Calculate the dot product.
            return (BAx * BCx + BAy * BCy);
        }

        internal static void FindIntersection(
    Point p1, Point p2, Point p3, Point p4,
    out bool lines_intersect, out bool segments_intersect,
    out Point intersection,
    out Point close_p1, out Point close_p2)
        {
            // Get the segments' parameters.
            var dx12 = p2.X - p1.X;
            var dy12 = p2.Y - p1.Y;
            var dx34 = p4.X - p3.X;
            var dy34 = p4.Y - p3.Y;

            // Solve for t1 and t2
            var denominator = (dy12 * dx34 - dx12 * dy34);

            var t1 = ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34) / denominator;
            if (double.IsInfinity(t1))
            {
                // The lines are parallel (or close enough to it).
                lines_intersect = false;
                segments_intersect = false;
                intersection = new Point(double.NaN, double.NaN);
                close_p1 = new Point(double.NaN, double.NaN);
                close_p2 = new Point(double.NaN, double.NaN);
                return;
            }
            lines_intersect = true;

            var t2 =
                ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12) / -denominator;

            // Find the point of intersection.
            intersection = new Point(p1.X + dx12 * t1, p1.Y + dy12 * t1);

            // The segments intersect if t1 and t2 are between 0 and 1.
            segments_intersect =
                ((t1 >= 0) && (t1 <= 1) &&
                 (t2 >= 0) && (t2 <= 1));

            // Find the closest points on the segments.
            if (t1 < 0)
            {
                t1 = 0;
            }
            else if (t1 > 1)
            {
                t1 = 1;
            }

            if (t2 < 0)
            {
                t2 = 0;
            }
            else if (t2 > 1)
            {
                t2 = 1;
            }

            close_p1 = new Point(p1.X + dx12 * t1, p1.Y + dy12 * t1);
            close_p2 = new Point(p3.X + dx34 * t2, p3.Y + dy34 * t2);
        }

        internal static double FindDistanceToSegment(
            Point pt, Point p1, Point p2, out Point closest)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            if ((dx == 0) && (dy == 0))
            {
                // It's a point not a line segment.
                closest = p1;
                dx = pt.X - p1.X;
                dy = pt.Y - p1.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            // Calculate the t that minimizes the distance.
            var t = ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) /
                (dx * dx + dy * dy);

            // See if this represents one of the segment's
            // end points or a point in the middle.
            if (t < 0)
            {
                closest = new Point(p1.X, p1.Y);
                dx = pt.X - p1.X;
                dy = pt.Y - p1.Y;
            }
            else if (t > 1)
            {
                closest = new Point(p2.X, p2.Y);
                dx = pt.X - p2.X;
                dy = pt.Y - p2.Y;
            }
            else
            {
                closest = new Point(p1.X + t * dx, p1.Y + t * dy);
                dx = pt.X - closest.X;
                dy = pt.Y - closest.Y;
            }

            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal static double FindDistanceToLine(
          Point pt, Point p1, Point p2)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            if ((dx == 0) && (dy == 0))
            {
                // It's a point not a line segment.
                dx = pt.X - p1.X;
                dy = pt.Y - p1.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            // Calculate the t that minimizes the distance.
            var t = ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / (dx * dx + dy * dy);
            var closest = new Point(p1.X + t * dx, p1.Y + t * dy);
            dx = pt.X - closest.X;
            dy = pt.Y - closest.Y;

            return Math.Sqrt(dx * dx + dy * dy);
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
        internal static bool LineSegementsIntersect(Point p, Point p2, Point q, Point q2,
            out Point intersection, bool considerCollinearOverlapAsIntersect = false)
        {
            intersection = new Point();

            var r = p2 - p;
            var s = q2 - q;
            var rxs = Vector.CrossProduct(r, s);
            var qpxr = Vector.CrossProduct((q - p), (r));

            // If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
            if (rxs == 0 && qpxr == 0)
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
            if (rxs == 0 && qpxr != 0)
                return false;

            // t = (q - p) x s / (r x s)
            var t = Vector.CrossProduct((q - p), (s)) / rxs;

            // u = (q - p) x r / (r x s)

            var u = Vector.CrossProduct((q - p), r) / rxs;

            // 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
            // the two line segments meet at the point p + t r = q + u s.
            if (rxs != 0 && (0 <= t && t <= 1) && (0 <= u && u <= 1))
            {
                // We can calculate the intersection point using either t or u.
                intersection = p + t * r;

                // An intersection was found.
                return true;
            }

            // 5. Otherwise, the two line segments are not parallel but do not intersect.
            return false;
        }

        private Point getPoint(Point[] points, int index)
        {
            while (index < 0)
                index += points.Length;

            index = index % points.Length;
            return points[index];
        }

        private int getIndex(int index, List<Point> points)
        {
            while (index < 0)
                index += points.Count;

            index = index % points.Count;
            return index;
        }

        private Point getPoint(int index, List<Point> points)
        {
            index = getIndex(index, points);
            return points[index];
        }


        private bool setPoint(List<Point> points, int index, Point newValue)
        {
            if (double.IsNaN(newValue.X) || double.IsNaN(newValue.Y))
                throw new NotImplementedException();

            if (double.IsInfinity(newValue.X) || double.IsInfinity(newValue.Y))
                throw new NotImplementedException();

            while (index < 0)
                index += points.Count;

            index = index % points.Count;
            var oldValue = points[index];
            if (Math.Abs((oldValue - newValue).LengthSquared) < 1e-4)
                return false;
            points[index] = newValue;

            return true;
        }

        private bool calculateBisector(Point p_prev, Point p_i, Point p_next, double offset, out Point bisector)
        {
            bisector = new Point(double.NaN, double.NaN);

            var e1 = unitVector(p_i, p_prev);
            var e2 = unitVector(p_i, p_next);
            var e = e1 + e2;
            e.Normalize();


            var crossProduct = Vector.CrossProduct(e1, e2);
            var dotProduct = Vector.Multiply(e1, e2);

            if (softEquals(crossProduct, 0) || double.IsNaN(crossProduct))
                return false;

            if (double.IsNaN(e.X) || double.IsNaN(e.Y))
                throw new NotImplementedException();

            if (dotProduct < -1)
                dotProduct = -1;

            if (dotProduct > 1)
                dotProduct = 1;

            var orientedE = crossProduct < 0 ? e : -e;
            var point = p_i + orientedE * offset / Math.Sin(0.5 * Math.Acos(dotProduct));

            if (double.IsNaN(point.X) || double.IsNaN(point.Y))
                throw new NotImplementedException();
            bisector = point;

            return true;
        }

        private bool softEquals(double length1, double length2)
        {
            return Math.Abs(length1 - length2) < 1e-2;
        }

        private bool softGE(double v1, double v2)
        {
            return v1 - 1e-2 >= v2;
        }

        private bool softSE(double v1, double v2)
        {
            return v1 + 1e-2 <= v2;
        }


        private Point asPoint(Point2Dmm p)
        {
            return new Point(p.C1, p.C2);
        }

        private Point2Dmm asPoint2D(Point p)
        {
            return new Point2Dmm(p.X, p.Y);
        }

        private Vector unitVector(Point p1, Point p2)
        {
            var v = p2 - p1;
            v.Normalize();
            return v;
        }
    }

    class SegmentBox
    {
        private readonly Point _p1, _p2;

        private readonly double _radius;

        private readonly BoxPart[] _parts;

        internal SegmentBox(Point p1, Point p2, double radius, bool p1Circle, bool p2Circle)
        {
            _p1 = p1;
            _p2 = p2;
            _radius = radius;

            var v = p1 - p2;
            var n = new Vector(v.Y, -v.X);
            n.Normalize();

            var offset = n * radius;
            var parts = new List<BoxPart>();
            if (p1Circle)
                parts.Add(new BoxPart(p1, radius));

            if (p2Circle)
                parts.Add(new BoxPart(p2, radius));

            parts.Add(new BoxPart(p1 - offset, p2 - offset));
            parts.Add(new BoxPart(p1 + offset, p2 + offset));

            _parts = parts.ToArray();
        }


        internal IEnumerable<Point> GetOffsetIntersections(SegmentBox box)
        {
            var result = new List<Point>();
            foreach (var part1 in _parts)
            {
                foreach (var part2 in box._parts)
                {
                    var candidates = part1.GetIntersections(part2).ToArray();
                    foreach (var point in candidates)
                    {
                        var distance1 = this.GetDistance(point);
                        var distance2 = box.GetDistance(point);

                        if (Math.Abs(distance1 - _radius) > 1e-4 || Math.Abs(distance2 - _radius) > 1e-4)
                            // some intersects can be closer then desired because of full circles on ends
                            continue;

                        result.Add(point);
                    }
                }
            }

            return result;
        }

        private double GetDistance(Point point)
        {
            var distance = OffsetCalculator.FindDistanceToSegment(point, _p1, _p2, out var contactPoint);
            return distance;
        }
    }

    class BoxPart
    {
        internal readonly bool IsCircle;
        private readonly Point _start;
        private readonly Point _end;
        private readonly Point _center;
        private readonly double _radius;

        internal BoxPart(Point start, Point end)
        {
            _start = start;
            _end = end;
        }

        internal BoxPart(Point center, double radius)
        {
            IsCircle = true;
            _center = center;
            _radius = radius;
        }

        internal IEnumerable<Point> GetIntersections(BoxPart part)
        {
            BoxPart p1, p2;

            if (IsCircle)
            {
                p1 = this;
                p2 = part;
            }
            else
            {
                p1 = part;
                p2 = this;
            }

            if (p1.IsCircle && p2.IsCircle)
            {
                return twoCircleIntersections(p1, p2);
            }
            else if (p1.IsCircle)
            {
                return circleToSegmentIntersections(p1, p2);
            }
            else
            {
                return twoSegmentIntersections(p1, p2);
            }
        }

        private IEnumerable<Point> twoSegmentIntersections(BoxPart p1, BoxPart p2)
        {
            if (OffsetCalculator.LineSegementsIntersect(p1._start, p1._end, p2._start, p2._end, out var intersection))
                yield return intersection;
        }

        private IEnumerable<Point> circleToSegmentIntersections(BoxPart c1, BoxPart l2)
        {
            var count = findLineCircleIntersections(c1._center, c1._radius, l2._start, l2._end, out var intersection1, out var intersection2);
            return filterIntersections(count, intersection1, intersection2);
        }

        private IEnumerable<Point> twoCircleIntersections(BoxPart c1, BoxPart c2)
        {
            var count = findCircleCircleIntersections(c1._center, c1._radius, c2._center, c2._radius, out var intersection1, out var intersection2);
            return filterIntersections(count, intersection1, intersection2);
        }

        private IEnumerable<Point> filterIntersections(int count, params Point[] intersections)
        {
            return intersections.Take(count);
        }

        // Find the points of intersection.
        private int findLineCircleIntersections(
            Point c, double radius,
            Point s1, Point s2,
            out Point intersection1, out Point intersection2)
        {
            var cx = c.X;
            var cy = c.Y;
            var dx = s2.X - s1.X;
            var dy = s2.Y - s1.Y;

            var A = dx * dx + dy * dy;
            var B = 2 * (dx * (s1.X - cx) + dy * (s1.Y - cy));
            var C = (s1.X - cx) * (s1.X - cx) +
                (s1.Y - cy) * (s1.Y - cy) -
                radius * radius;

            var det = B * B - 4 * A * C;
            double t;
            if ((A <= 0.0000001) || (det < 0))
            {
                // No real solutions.
                intersection1 = new Point(double.NaN, double.NaN);
                intersection2 = new Point(double.NaN, double.NaN);
                return 0;
            }
            else if (det == 0)
            {
                // One solution.
                t = -B / (2 * A);
                intersection1 =
                    new Point(s1.X + t * dx, s1.Y + t * dy);
                intersection2 = new Point(double.NaN, double.NaN);
                return 1;
            }
            else
            {
                // Two solutions.
                t = ((-B + Math.Sqrt(det)) / (2 * A));
                intersection1 =
                    new Point(s1.X + t * dx, s1.Y + t * dy);
                t = ((-B - Math.Sqrt(det)) / (2 * A));
                intersection2 =
                    new Point(s1.X + t * dx, s1.Y + t * dy);
                return 2;
            }
        }

        private int findCircleCircleIntersections(Point c0, double radius0, Point c1, double radius1, out Point intersection1, out Point intersection2)
        {
            // Find the distance between the centers.
            var dx = c0.X - c1.X;
            var dy = c0.Y - c1.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            // See how many solutions there are.
            if (dist > radius0 + radius1)
            {
                // No solutions, the circles are too far apart.
                intersection1 = new Point(double.NaN, double.NaN);
                intersection2 = new Point(double.NaN, double.NaN);
                return 0;
            }
            else if (dist < Math.Abs(radius0 - radius1))
            {
                // No solutions, one circle contains the other.
                intersection1 = new Point(double.NaN, double.NaN);
                intersection2 = new Point(double.NaN, double.NaN);
                return 0;
            }
            else if ((dist == 0) && (radius0 == radius1))
            {
                // No solutions, the circles coincide.
                intersection1 = new Point(double.NaN, double.NaN);
                intersection2 = new Point(double.NaN, double.NaN);
                return 0;
            }
            else
            {
                // Find a and h.
                var a = (radius0 * radius0 -
                    radius1 * radius1 + dist * dist) / (2 * dist);
                var h = Math.Sqrt(radius0 * radius0 - a * a);

                // Find P2.
                var cx2 = c0.X + a * (c1.X - c0.X) / dist;
                var cy2 = c0.Y + a * (c1.Y - c0.Y) / dist;

                // Get the points P3.
                intersection1 = new Point(
                    (double)(cx2 + h * (c1.Y - c0.Y) / dist),
                    (double)(cy2 - h * (c1.X - c0.X) / dist));
                intersection2 = new Point(
                    (double)(cx2 - h * (c1.Y - c0.Y) / dist),
                    (double)(cy2 + h * (c1.X - c0.X) / dist));

                // See if we have 1 or 2 solutions.
                if (dist == radius0 + radius1)
                    return 1;
                return 2;
            }
        }

    }

    class Intersection
    {
        public readonly int S;

        public readonly int E;

        public readonly double RatioS;

        public readonly double RatioE;

        internal double SValue => _sValue;

        private double _sValue => S + RatioS;

        private double _eValue => E + RatioE;

        private readonly List<Intersection> _children = new List<Intersection>();

        private Intersection _parent;

        internal Intersection Parent => _parent;

        internal IEnumerable<Intersection> Children => _children;

        internal Intersection(int s, double ratioS, int e, double ratioE)
        {
            S = s;
            E = e;
            RatioS = ratioS;
            RatioE = ratioE;
        }

        public bool Contains(Intersection o)
        {
            return _sValue < o._sValue && _eValue > o._eValue;
        }

        public bool BelongsTo(Intersection o)
        {
            return _sValue >= o._sValue && _eValue < o._eValue;
        }

        public bool Interfere(Intersection o)
        {
            var s1 = _sValue;
            var s2 = o._sValue;
            var b1 = _eValue;
            var b2 = o._eValue;
            return
                (s1 < s2 && s2 < b1 && b2 > b1) ||
                (s2 < b1 && b1 < b2 && s1 < s2);
        }

        internal void BuildTree(IEnumerable<Intersection> children)
        {
            var q = new Queue<Intersection>(children.OrderBy(c => c.SValue));
            buildTree(q);
        }

        private void buildTree(Queue<Intersection> q)
        {
            while (q.Count > 0)
            {
                if (!Contains(q.Peek()))
                    break;

                var child = q.Dequeue();
                addChild(child);

                child.buildTree(q);
            }
        }

        private void addChild(Intersection child)
        {
            child._parent = this;
            this._children.Add(child);
        }

        internal Intersection Rearange(int validIndex, List<Point> points)
        {
            var newS = (S - validIndex + points.Count) % points.Count;
            var newE = (E - validIndex + points.Count) % points.Count;
            if (newE < newS)
                return new Intersection(newE, RatioE, newS, RatioS);
            else
                return new Intersection(newS, RatioS, newE, RatioE);
        }

        public override string ToString()
        {
            return $"({S},{E}) {_sValue:0.00} | {_eValue:0.00}";
        }
    }

    public abstract class DrawEvent
    {
        public abstract void Draw(DrawingContext dc);
    }

    class CircleEvent : DrawEvent
    {
        private readonly Point _center;

        private readonly double _radius;

        private readonly Pen _pen;

        internal CircleEvent(Point center, double radius, Pen pen)
        {
            _center = center;
            _radius = radius;
            _pen = pen;
        }

        public override void Draw(DrawingContext dc)
        {
            dc.DrawEllipse(null, _pen, _center, _radius, _radius);
        }
    }

    class LineEvent : DrawEvent
    {
        private readonly Point _p1;

        private readonly Point _p2;

        private readonly Pen _pen;

        internal LineEvent(Point p1, Point p2, Pen pen)
        {
            _p1 = p1;
            _p2 = p2;
            _pen = pen;
        }

        public override void Draw(DrawingContext dc)
        {
            dc.DrawLine(_pen, _p1, _p2);
        }
    }
}
