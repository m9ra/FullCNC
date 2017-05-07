using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;

namespace ControllerCNC.Primitives
{
    internal static class Extensions
    {
        public static IEnumerable<Point4Dmm> DuplicateTo4Dmm(this IEnumerable<Point2Dmm> points)
        {
            return points.Select(p => new Point4Dmm(p.C1, p.C2, p.C1, p.C2));
        }

        public static IEnumerable<Point4Dmm> As4Dmm(this IEnumerable<Point4Dstep> points)
        {
            return points.Select(p => new Point4Dmm(stepToMm(p.U), stepToMm(p.V), stepToMm(p.X), stepToMm(p.Y)));
        }

        public static Point4Dstep As4Dstep(this Point4Dmm point)
        {
            return new Point4Dstep(mmToStep(point.U), mmToStep(point.V), mmToStep(point.X), mmToStep(point.Y));
        }

        public static Point2Dstep As2Dstep(this Point2Dmm point)
        {
            return new Point2Dstep(mmToStep(point.C1), mmToStep(point.C2));
        }

        public static Point2Dmm As2Dmm(this Point2Dstep point)
        {
            return new Point2Dmm(stepToMm(point.C1), stepToMm(point.C2));
        }

        public static IEnumerable<Point4Dstep> As4Dstep(this IEnumerable<Point4Dmm> points)
        {
            return points.Select(p => p.As4Dstep());
        }

        public static IEnumerable<Point2Dmm> As2Dmm(this IEnumerable<Point2Dstep> points)
        {
            return points.Select(p => new Point2Dmm(stepToMm(p.C1), stepToMm(p.C2)));
        }

        public static IEnumerable<Point2Dmm> ToUV(this IEnumerable<Point4Dmm> points)
        {
            return points.Select(p => new Point2Dmm(p.U, p.V));
        }

        public static IEnumerable<Point2Dstep> ToUV(this IEnumerable<Point4Dstep> points)
        {
            return points.Select(p => new Point2Dstep(p.U, p.V));
        }

        public static IEnumerable<Point2Dmm> ToXY(this IEnumerable<Point4Dmm> points)
        {
            return points.Select(p => new Point2Dmm(p.X, p.Y));
        }

        public static IEnumerable<Point2Dstep> ToXY(this IEnumerable<Point4Dstep> points)
        {
            return points.Select(p => new Point2Dstep(p.X, p.Y));
        }

        public static Speed4Dstep With(this Point4Dstep point, Speed speed)
        {
            return new Speed4Dstep(point, speed);
        }

        private static int mmToStep(double mm)
        {
            return (int)Math.Round(mm / Constants.MilimetersPerStep);
        }

        private static double stepToMm(int step)
        {
            return step * Constants.MilimetersPerStep;
        }
    }
}
