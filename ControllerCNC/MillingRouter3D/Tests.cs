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
    internal class Tests
    {
        public static void Main(string[] args)
        {
            InstructionGeneration();
        }

        public static void InstructionGeneration()
        {
            var p1 = new Point3D(0, 0, 0);
            var p2 = new Point3D(0, 100, 0);
            var p3 = new Point3D(1000, 5000, 0);
            var s1 = new ToolPathSegment(p1, p2, MotionMode.IsLinear);
            var s2 = new ToolPathSegment(p2, p3, MotionMode.IsLinear);

            var start = DateTime.Now;
            var instructions = AcceleratingPlanBuilder3D.GenerateInstructions(new[] { s1, s2 });
            var end = DateTime.Now;
            Console.WriteLine((end - start).TotalSeconds);
            Console.ReadKey();

            var xInstructions = new List<StepInstrution>();
            var yInstructions = new List<StepInstrution>();
            foreach (Axes instruction in instructions)
            {
                xInstructions.Add(instruction.InstructionU);
                yInstructions.Add(instruction.InstructionV);
                Console.WriteLine($"{instruction.InstructionU}  {instruction.InstructionV}");
            }

            var previousRange = PathSpeedLimitCalculator.AccelerationRanges[0];
            foreach (var range in PathSpeedLimitCalculator.AccelerationRanges)
            {
                Console.WriteLine($"{range} {previousRange - range}");
                previousRange = range;
            }
        }

        public static void CornerLimits()
        {
            var p1 = new Point3D(0, 0, 0);
            var p2 = new Point3D(0, 100, 0);
            var p3 = new Point3D(100, 500, 0);
            var s1 = new ToolPathSegment(p1, p2, MotionMode.IsLinear);
            var s2 = new ToolPathSegment(p2, p3, MotionMode.IsLinear);

            var limitCalculator = new PathSpeedLimitCalculator(s1);
            var l1 = limitCalculator.GetLimit(0);
            var l2 = limitCalculator.GetLimit(0.99);
            var l3 = limitCalculator.GetLimit(0.999);

            var cornerLimit = PathSpeedLimitCalculator.CalculateEdgeLimit(s1, s2);
            var edgeLimits = new Dictionary<ToolPathSegment, double>();
            edgeLimits[s2] = cornerLimit;
            limitCalculator.AddLookaheadSegments(new[] { s2 }, edgeLimits);

            var al1 = limitCalculator.GetLimit(0);
            var al2 = limitCalculator.GetLimit(0.99);
            var al3 = limitCalculator.GetLimit(0.999);
        }

        public static void LineSlicing()
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
