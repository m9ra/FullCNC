using ControllerCNC.GUI;
using ControllerCNC.Machine;
using ControllerCNC.Machine.Logging;
using ControllerCNC.Planning;
using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace MillingRouter3D
{
    class Tests
    {
        public static void Main(string[] args)
        {
            Configuration.EnableRouterMode();
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = customCulture;
            SystemUtilities.PreventSleepMode();

            var speed1Ticks = 900;
            var speed2Ticks = 10000;
            var timeGrainTicks = 3000;

            var lengthMm = 100;
            var v = new Vector(
                ControllerCNC.Primitives.Speed.FromDeltaT(speed1Ticks).ToMetric(),
                ControllerCNC.Primitives.Speed.FromDeltaT(speed2Ticks).ToMetric()
                );


            var timeGrain = 1.0 * timeGrainTicks / Configuration.TimerFrequency;
            var speed = v.Length;
            v = v * lengthMm;

            var segment = new ToolPathSegment(new Point3D(0, 0, 0), new Point3D(v.X, v.Y, 0), MotionMode.IsLinearRapid);
            var logger = new StepLogger(".");


            var slicer = new ToolPathSegmentSlicer(segment);
            for (var i = 0; i < 15; ++i)
            {
                var instruction = slicer.Slice(speed, timeGrain);
                logger.LogInstruction(instruction);
            }
            logger.Flush();
        }
    }
}
