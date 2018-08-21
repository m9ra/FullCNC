using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Volumetric
{
    public class HeightMap
    {
        private readonly double[,] _voxels;

        public readonly Point Start;

        public readonly Point End;

        public readonly double StepSize;

        public int VoxelCountX => _voxels.GetLength(0);

        public int VoxelCountY => _voxels.GetLength(1);

        private HeightMap(double[,] voxels, Point startPoint, double stepSize)
        {
            _voxels = voxels;

            StepSize = stepSize;
            Start = startPoint;
            End = new Point(Start.X + VoxelCountX * StepSize, Start.Y + VoxelCountY * StepSize);
        }

        public double GetHeight(int xi, int yi)
        {
            return _voxels[xi, yi];
        }

        public Point GetPoint(int xi, int yi)
        {
            return new Point(xi * StepSize + Start.X, yi * StepSize + Start.Y);
        }

        public double[,] GetVoxelsCopy()
        {
            return (double[,])_voxels.Clone();
        }

        public static HeightMap Flat(double height, Point p1, Point p2, double stepSize)
        {
            return createMap(p1, p2, stepSize, p => height);
        }

        public static HeightMap From(Shape3D shape, Point p1, Point p2, double stepSize)
        {
            return createMap(p1, p2, stepSize, shape.GetVerticalHeight);
        }

        public void ApplyMinSubConvolution(Point p, double convolutionOffset, double[,] convolution)
        {
            iterateConvolution(p, convolution, (height, convValue, absX, absY) =>
            {
                _voxels[absX, absY] = Math.Min(height, -convValue + convolutionOffset);
            });
        }

        public double GetMaxAddConvolution(Point p, double[,] convolution)
        {
            var currentMax = double.NegativeInfinity;
            iterateConvolution(p, convolution, (height, convValue, absX, absY) =>
            {
                var totalHeight = height + convValue;
                if (currentMax < totalHeight)
                    currentMax = totalHeight;
            });

            return currentMax;
        }

        private void iterateConvolution(Point p, double[,] convolution, Action<double, double, int, int> iterator)
        {
            var stepCountX = convolution.GetLength(0);
            var stepCountY = convolution.GetLength(1);

            var positionIndexes = getIndexes(p);
            for (var xIndex = Math.Max(0, -positionIndexes.Item1); xIndex < stepCountX; ++xIndex)
            {
                var absXIndex = xIndex + positionIndexes.Item1;
                if (absXIndex >= VoxelCountX)
                    break;

                for (var yIndex = Math.Max(0, -positionIndexes.Item2); yIndex < stepCountY; ++yIndex)
                {
                    var absYIndex = yIndex + positionIndexes.Item2;
                    if (absYIndex >= VoxelCountY)
                        break;

                    iterator(_voxels[absXIndex, absYIndex], convolution[xIndex, yIndex], absXIndex, absYIndex);
                }
            }
        }

        private static HeightMap createMap(Point p1, Point p2, double stepSize, Func<Point, double> heightDefinition)
        {
            var distanceX = Math.Abs(p1.X - p2.X);
            var distanceY = Math.Abs(p1.Y - p2.Y);

            var stepCountX = (int)Math.Ceiling(distanceX / stepSize);
            var stepCountY = (int)Math.Ceiling(distanceY / stepSize);

            var voxels = new double[stepCountX, stepCountY];

            for (var xIndex = 0; xIndex < stepCountX; ++xIndex)
            {
                var x = p1.X + xIndex * stepSize;
                for (var yIndex = 0; yIndex < stepCountY; ++yIndex)
                {
                    var y = p1.Y + yIndex * stepSize;
                    var p = new Point(x, y);

                    var height = heightDefinition(p);
                    voxels[xIndex, yIndex] = height;
                }
            }
            return new HeightMap(voxels, p1, stepSize);
        }

        private Tuple<int, int> getIndexes(Point p)
        {
            var x = (int)Math.Round((p.X - Start.X) / StepSize);
            var y = (int)Math.Round((p.Y - Start.Y) / StepSize);

            return Tuple.Create(x, y);
        }
    }
}
