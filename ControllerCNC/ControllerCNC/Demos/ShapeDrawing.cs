using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

using System.IO;
using System.Drawing;
using System.Drawing.Imaging;


namespace ControllerCNC.Demos
{
    public delegate IEnumerable<Point4D> CoordinateProvider();


    public static class ShapeDrawing
    {
        #region Drawing methods

        /// <summary>
        /// Draws coordinates from a provider by a constant speed
        /// </summary>
        /// <param name="provider">Provider which creates coordinates.</param>
        /// <param name="speed">Speed of drawing</param>
        public static PlanBuilder DrawByConstantSpeed(CoordinateProvider provider, Speed speed = null)
        {
            if (speed == null)
                speed = Constants.ReverseSafeSpeed;

            var points = provider();
            var trajectory = new Trajectory4D(points);

            var planner = new StraightLinePlanner(speed);
            return planner.CreateConstantPlan(trajectory);
        }

        /// <summary>
        /// Draws coordinates from a provider by a constant speed
        /// </summary>
        /// <param name="provider">Provider which creates coordinates.</param>
        /// <param name="speed">Speed of drawing</param>
        public static PlanBuilder DrawByRampedLines(CoordinateProvider provider, Speed speed = null)
        {
            if (speed == null)
                speed = Constants.MaxPlaneSpeed;

            var points = provider();
            var trajectory = new Trajectory4D(points);

            var planner = new StraightLinePlanner(speed);
            return planner.CreateRampedPlan(trajectory);
        }

        /// <summary>
        /// Draws coordinates from a provider by a continuous speed
        /// </summary>
        /// <param name="provider">Provider which creates coordinates.</param>
        /// <param name="speed">Speed of drawing</param>
        public static PlanBuilder DrawContinuousLines(CoordinateProvider provider, Speed speed = null)
        {
            if (speed == null)
                speed = Constants.MaxPlaneSpeed;

            var points = provider();
            var trajectory = new Trajectory4D(points);

            var planner = new StraightLinePlanner(speed);
            return planner.CreateContinuousPlan(trajectory);
        }

        /// <summary>
        /// Draws a square filled with diagonals - uses 45 degree acceleration methods 
        /// </summary>
        public static PlanBuilder DrawSquareWithDiagonals()
        {
            var speed = Constants.MaxPlaneSpeed;
            var acceleration = Constants.MaxPlaneAcceleration; //new Acceleration(Constants.MaxPlaneAcceleration.Speed,Constants.MaxPlaneAcceleration.Ticks*10);
            var squareSize = 6000;
            var diagonalDistance = 300;



            var builder = new PlanBuilder();

            //do a square border
            builder.AddRampedLineXY(squareSize, 0, acceleration, speed);
            builder.AddRampedLineXY(0, squareSize, acceleration, speed);
            builder.AddRampedLineXY(-squareSize, 0, acceleration, speed);
            builder.AddRampedLineXY(0, -squareSize, acceleration, speed);
            //left right diagonals
            var diagonalCount = squareSize / diagonalDistance;
            for (var i = 0; i < diagonalCount * 2; ++i)
            {
                var diagLength = (diagonalCount - Math.Abs(i - diagonalCount)) * diagonalDistance;

                if (i % 2 == 0)
                {
                    builder.AddRampedLineXY(-diagLength, diagLength, acceleration, speed);
                    if (i < diagonalCount)
                        builder.AddRampedLineXY(0, diagonalDistance, acceleration, speed);
                    else
                        builder.AddRampedLineXY(diagonalDistance, 0, acceleration, speed);
                }
                else
                {
                    builder.AddRampedLineXY(diagLength, -diagLength, acceleration, speed);
                    if (i < diagonalCount)
                        builder.AddRampedLineXY(diagonalDistance, 0, acceleration, speed);
                    else
                        builder.AddRampedLineXY(0, diagonalDistance, acceleration, speed);
                }
            }

            return builder;
        }

        #endregion

        #region Shape coordinate providers.

        public static IEnumerable<Point4D> LoadCoordinates(string fileName)
        {
            //loading for .COR files
            var lines = File.ReadAllLines(fileName);
            var scale = 25000;
            var coordinates = new List<Point4D>();
            foreach (var line in lines)
            {
                if (line.Trim() == "")
                    continue;

                var parts = line.Trim().Split('\t');
                var xCoord = double.Parse(parts[0]);
                var yCoord = double.Parse(parts[1]);

                coordinates.Add(point2D(xCoord, yCoord, scale));
            }

            return coordinates;
        }

        public static IEnumerable<Point4D> InterpolateImage(string filename, int pointCount, int pointDistance, double scale)
        {
            var image = new Bitmap(Image.FromFile(filename));
            var data = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);

            // Get the address of the first line.
            var ptr = data.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            var dataSize = Math.Abs(data.Stride) * image.Height;
            var bytes = new byte[dataSize];
            System.Runtime.InteropServices.Marshal.Copy(ptr, bytes, 0, dataSize);
            image.UnlockBits(data);

            //first pixel will be treated as mask
            var background = bytes[0];

            var points = new List<Point>();
            for (var y = 1; y < image.Height - 1; ++y)
            {
                for (var x = 1; x < image.Width - 1; ++x)
                {
                    var currentColor = bytes[y * data.Stride + x];
                    if (currentColor != background)
                        continue;

                    tryAddPoint(x + 1, y, bytes, data, background, points);
                    tryAddPoint(x, y + 1, bytes, data, background, points);
                    tryAddPoint(x - 1, y, bytes, data, background, points);
                    tryAddPoint(x, y - 1, bytes, data, background, points);
                }
            }

            var desiredPointCount = pointCount;
            var pointPeriod = points.Count / desiredPointCount;
            var currentPoint = new Point();
            var orderedPoints = new List<Point>();
            while (points.Count > 0)
            {
                var currentBestPointIndex = 0;
                var currentBestDistance = double.PositiveInfinity;
                for (var i = 0; i < points.Count; ++i)
                {
                    var point = points[i];
                    var distance = Math.Sqrt(Math.Pow(point.X - currentPoint.X, 2) + Math.Pow(point.Y - currentPoint.Y, 2));
                    if (distance <= currentBestDistance)
                    {
                        currentBestPointIndex = i;
                        currentBestDistance = distance;
                    }
                }

                currentPoint = points[currentBestPointIndex];
                points.RemoveAt(currentBestPointIndex);
                if (currentBestDistance > 0 && (points.Count % Math.Max(1, pointPeriod)) == 0)
                {
                    if (currentBestDistance < pointDistance)
                        orderedPoints.Add(currentPoint);
                }
            }

            orderedPoints.Add(orderedPoints[0]);//make the drawing closed

            var result = new List<Point4D>();
            foreach (var point in orderedPoints)
            {
                result.Add(point2D(point.X, point.Y, scale));
            }
            return result;
        }

        private static void tryAddPoint(int x, int y, byte[] bytes, BitmapData data, byte background, List<Point> points)
        {
            var color = bytes[y * data.Stride + x];
            if (color != background)
                points.Add(new Point(x, y));
        }

        /// <summary>
        /// Coordinates of a heart.
        /// </summary>
        public static IEnumerable<Point4D> HeartCoordinates()
        {
            var top = new List<Point4D>();
            var bottom = new List<Point4D>();

            var smoothness = 200;
            var scale = 2000;

            for (var i = 0; i <= smoothness; ++i)
            {
                var x = -2 + (4.0 * i / smoothness);
                var y1 = Math.Sqrt(1.0 - Math.Pow(Math.Abs(x) - 1, 2));
                var y2 = -3 * Math.Sqrt(1 - (Math.Sqrt(Math.Abs(x)) / Math.Sqrt(2)));

                top.Add(point2D(x, y1, scale));
                bottom.Add(point2D(x, y2, scale));
            }
            top.Reverse();
            var result = bottom.Concat(top).ToArray();

            return result;
        }

        /// <summary>
        /// Coordinates of a triangle.
        /// </summary>
        public static IEnumerable<Point4D> TriangleCoordinates()
        {
            return new[]{
                point2D(0,0),
                point2D(4000,2000),
                point2D(-4000,2000),
                point2D(0,0)
            };
        }

        /// <summary>
        /// Coordinates of a circle.
        /// </summary>
        public static IEnumerable<Point4D> CircleCoordinates(double r=4000)
        {
            var circlePoints = new List<Point4D>();
            var smoothness = 1;
            for (var i = 0; i <= 360 * smoothness; ++i)
            {
                var x = Math.Sin(i * Math.PI / 180 / smoothness);
                var y = Math.Cos(i * Math.PI / 180 / smoothness);
                circlePoints.Add(point2D(x, y, r));
            }
            return circlePoints;
        }

        /// <summary>
        /// Coordinates of a multicross.
        /// </summary>
        public static IEnumerable<Point4D> MulticrossCoordinates()
        {
            var points = new List<Point4D>();
            var r = 15000;
            var smoothness = 0.5;
            for (var i = 0; i <= 360 * smoothness; ++i)
            {
                var x = Math.Sin(i * Math.PI / 180 / smoothness);
                var y = Math.Cos(i * Math.PI / 180 / smoothness);
                points.Add(point2D(x, y, r));
                points.Add(point2D(0, 0, r));
            }
            return points;
        }

        /// <summary>
        /// Coordinates of a spiral.
        /// </summary>
        public static IEnumerable<Point4D> SpiralCoordinates()
        {
            var spiralPoints = new List<Point4D>();
            var r = 15000;
            for (var i = 0; i <= r; ++i)
            {
                var x = Math.Sin(i * Math.PI / 180);
                var y = Math.Cos(i * Math.PI / 180);
                spiralPoints.Add(point2D(x, y, i));
            }
            return spiralPoints;
        }

        /// <summary>
        /// Coordinates of a line.
        /// </summary>
        public static IEnumerable<Point4D> LineCoordinates()
        {
            var start = point2D(0, 0);
            var end = point2D(50000, 30000);

            var segmentCount = 5000;

            var linePoints = new List<Point4D>();
            for (var i = 0; i <= segmentCount; ++i)
            {
                var x = 1.0 * (end.X - start.X) / segmentCount * i;
                var y = 1.0 * (end.Y - start.Y) / segmentCount * i;
                linePoints.Add(point2D(x, y, 1));
            }

            return linePoints;
        }
        #endregion

        #region Private utilities

        /// <summary>
        /// Converts 2D coordinates into Point4D.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>The <see cref="Point4D"/>.</returns>
        private static Point4D point2D(int x, int y)
        {
            return new Point4D(0, 0, -x, -y);
        }

        /// <summary>
        /// Converts and scales 2D coordinates into Point4D.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="scale">Scale of the coordinates.</param>
        /// <returns>The <see cref="Point4D"/>.</returns>
        private static Point4D point2D(double x, double y, double scale)
        {
            return point2D((int)Math.Round(x * scale), (int)Math.Round(y * scale));
        }

        #endregion
    }
}
