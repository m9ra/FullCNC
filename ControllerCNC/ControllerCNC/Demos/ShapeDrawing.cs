using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

using System.IO;
using System.Windows;

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

        #region Smooth shape functions

        public static Point4D Rectangle(double percentage, double width)
        {
            var threshold = 0.25;
            if (percentage < threshold)
            {
                percentage = percentage - threshold + 0.25;
                percentage *= 4;
                return point2D(percentage - 0.5, 0 - 0.5, width);
            }

            threshold = 0.5;
            if (percentage < threshold)
            {
                percentage = percentage - threshold + 0.25;
                percentage *= 4;
                return point2D(1.0 - 0.5, percentage - 0.5, width);
            }

            threshold = 0.75;
            if (percentage < threshold)
            {
                percentage = percentage - threshold + 0.25;
                percentage *= 4;
                return point2D(1.0 - percentage - 0.5, 1.0 - 0.5, width);
            }

            threshold = 1.0;
            percentage = percentage - threshold + 0.25;
            percentage *= 4;
            return point2D(0.0 - 0.5, 1.0 - percentage - 0.5, width);
        }

        public static Point4D Circle(double percentage, double width)
        {
            var x = Math.Sin(Math.PI * 2 * percentage);
            var y = Math.Cos(Math.PI * 2 * percentage);

            var scale = width / 2;
            return point2D(x, y, scale);
        }

        #endregion

        #region 4D drawing

        public static IEnumerable<Point4D> CircleToSquare()
        {
            var metricWidth = 30;
            var size = metricWidth / Constants.MilimetersPerStep;
            var sizeDiscrete = (int)Math.Round(size);
            var points = new List<Point4D>();
            for (var i = 0; i <= 100; ++i)
            {
                var percentage = i / 100.0;

                var rectPercentage = percentage + 1.0 / 8;
                if (rectPercentage > 1.0)
                    rectPercentage -= 1;
                var circleCoord = ShapeDrawing.Circle(percentage, size);
                var rectCoord = ShapeDrawing.Rectangle(rectPercentage, size);

                var combinedCoord = new Point4D(-circleCoord.X + sizeDiscrete, circleCoord.Y + sizeDiscrete, -rectCoord.X + sizeDiscrete, -rectCoord.Y + sizeDiscrete);
                points.Add(combinedCoord);
            }

            return points;
        }

        public static IEnumerable<Point4D> CircleToPoint()
        {
            var metricWidth = 30;
            var size = metricWidth / Constants.MilimetersPerStep;
            var sizeDiscrete = (int)Math.Round(size);
            var points = new List<Point4D>();
            for (var i = 0; i <= 100; ++i)
            {
                var percentage = i / 100.0;

                var point = new Point4D(0, 0, 0, 0);
                var rectCoord = ShapeDrawing.Circle(percentage, size);
                var combinedCoord = new Point4D(-point.X + sizeDiscrete, point.Y + sizeDiscrete, -rectCoord.X + sizeDiscrete, -rectCoord.Y + sizeDiscrete);
                points.Add(combinedCoord);
            }

            return points;
        }

        #endregion

        #region Shape coordinate providers.

        /// <summary>
        /// Loads coordinates from specified COR file.
        /// </summary>
        public static IEnumerable<Point4D> LoadCoordinatesCOR(string fileName)
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

        /// <summary>
        /// Interpolates coordinates from given image.
        /// </summary>
        public static IEnumerable<Point4D> InterpolateImage(string filename, double scale)
        {
            var interpolator = new ImageInterpolator(filename);
            return interpolator.InterpolateCoordinates(scale);
        }

        /// <summary>
        /// Interpolates coordinates from given image.
        /// </summary>
        public static IEnumerable<Point2Df> InterpolateImage(string filename)
        {
            var interpolator = new ImageInterpolator(filename);
            return interpolator.InterpolateCoordinates();
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
        public static IEnumerable<Point4D> CircleCoordinates(double r = 4000)
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
