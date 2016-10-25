using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TrajectorySimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Time scale of the machine. (2MHz)
        /// </summary>
        internal static readonly int TimeScale = 2000000;

        /// <summary>
        /// How many steps for single revolution has to be done.
        /// </summary>
        internal static readonly int StepsPerRevolution = 400;

        /// <summary>
        /// Maximal safe acceleration in steps/s^2.
        /// </summary>
        internal static readonly int MaxAcceleration = 2 * StepsPerRevolution;

        public MainWindow()
        {
            InitializeComponent();

            var point1 = new Vector(0, 0);
            var point2 = new Vector(10, 200);

            var direction = (point2 - point1);
            direction.Normalize();

            var initialSpeed = direction * TimeScale / 2000;
            var desiredSpeed = direction * TimeScale / 500;

            var acceleration = direction * MaxAcceleration;
            var xAcceleration = acceleration.X;
            var yAcceleration = acceleration.Y;


            var traceX = new ChannelTrace();
            var traceX2 = new ChannelTrace();
            var traceY = new ChannelTrace();
            var traceY2 = new ChannelTrace();

            var simulator = new CncSimulator();

            var accelerationDistance = calculateAccelerationDistance(initialSpeed, desiredSpeed, acceleration);



            var xSteps = (int)Math.Round(accelerationDistance.X);
            var xInitialDelta = (int)Math.Round(TimeScale / initialSpeed.X);
            var xN = calculateN(xInitialDelta, (int)Math.Round(acceleration.X), MaxAcceleration);
            simulator.CalculateAcceleration(xSteps, xInitialDelta, (int)Math.Round(xN), traceX);
           // simulator.CalculateAccelerationExact(initialSpeed.X, desiredSpeed.X, acceleration.X, traceX2);



            var ySteps = (int)Math.Round(accelerationDistance.Y);
            var yInitialDelta = (int)Math.Round(TimeScale / initialSpeed.Y);
            var yN = calculateN(yInitialDelta, (int)Math.Round(acceleration.Y), MaxAcceleration);

            simulator.CalculateAcceleration(ySteps, yInitialDelta, (int)Math.Round(yN), traceY);
        //    simulator.CalculateAccelerationExact(initialSpeed.Y, desiredSpeed.Y, acceleration.Y, traceY2);

            var plotter = new ChannelPlotter();
            var data = plotter.Plot2D(traceX, traceY).ToArray();

            foreach (var point in data)
            {
                System.Diagnostics.Debug.WriteLine(point);
            }

            var yTime = traceY.Times.Sum();
            var xTime = traceX.Times.Sum();

            var yTime2 = traceY2.Times.Sum();
            var xTime2 = traceX2.Times.Sum();
        }

        private Vector calculateAccelerationDistance(Vector startVelocity, Vector endVelocity, Vector acceleration)
        {
            var deltaVelocity = endVelocity - startVelocity;
            var accelerationTime = deltaVelocity.Length / acceleration.Length;
            var time2 = deltaVelocity.X / acceleration.X;

            var distance = startVelocity * accelerationTime + 0.5 * acceleration * accelerationTime * accelerationTime;

            return distance;
        }

        private double calculateSteps(int startDeltaT, int endDeltaT, int accelerationNumerator, int accelerationDenominator)
        {
            var n1 = calculateN(startDeltaT, accelerationNumerator, accelerationDenominator);
            var n2 = calculateN(endDeltaT, accelerationNumerator, accelerationDenominator);

            return n2 - n1;
        }

        private double calculateN(int startDeltaT, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var n1 = (double)TimeScale * TimeScale * accelerationDenominator / 2 / startDeltaT / startDeltaT / MaxAcceleration / accelerationNumerator;

                return n1;
            }
        }
    }
}
