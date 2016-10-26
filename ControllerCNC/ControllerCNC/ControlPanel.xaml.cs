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

using System.Windows.Threading;

using System.Threading;

using System.IO.Ports;

using ControllerCNC.Planning;
using ControllerCNC.Primitives;

namespace ControllerCNC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DriverCNC _driver;
        DispatcherTimer _positionTimer = new DispatcherTimer();
        DispatcherTimer _statusTimer = new DispatcherTimer();
        SpeedController _speedController;
        PositionController _positionController;
        Coord2DController _coord2DController;

        public MainWindow()
        {
            InitializeComponent();
            System.Diagnostics.Process myProcess = System.Diagnostics.Process.GetCurrentProcess();
            myProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.RealTime;

            Output.ScrollToEnd();

            _driver = new DriverCNC();
            _driver.OnDataReceived += _driver_OnDataReceived;
            _driver.Initialize();

            _positionController = new PositionController(_driver);
            _speedController = new SpeedController(_driver);
            _coord2DController = new Coord2DController(_driver);

            _positionTimer.Interval = new TimeSpan(1 * 10 * 1000);
            _positionTimer.Tick += _positionTimer_Tick;
            _positionTimer.IsEnabled = false;

            _statusTimer.Interval = new TimeSpan(100 * 10 * 1000);
            _statusTimer.Tick += _statusTimer_Tick;
            _statusTimer.IsEnabled = true;
        }


        void _statusTimer_Tick(object sender, EventArgs e)
        {
            Status.Text = "Incomplete: " + _driver.IncompletePlanCount;
        }

        void _positionTimer_Tick(object sender, EventArgs e)
        {
            _positionTimer.Stop();

            var steps = (int)Position.Value - 200;
            _positionController.SetPosition(steps);
        }

        void _driver_OnDataReceived(string data)
        {
            this.Dispatcher.BeginInvoke(((Action)(() =>
            {
                Output.AppendText(data);
                Output.ScrollToEnd();
            })));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //_driver.SEND_Transition(0, 1500, 400 * 1500, 0);

            _driver.SEND_TransitionRPM(-400 * 10, 0, 1000, 500);
            _driver.SEND_TransitionRPM(-400 * 10, 500, 500, 500);
            _driver.SEND_TransitionRPM(-400 * 20, 500, 1500, 0);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var segmentation = 100;
            for (var i = 0; i < 400 / segmentation; ++i)
            {
                _driver.SEND_TransitionRPM(segmentation, 0, 1500, 0);
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            /*
A(2,15000,1)
A(2,4082,-3)
A(-3,15000,1)
C(-1,3535,0,0)
             */


            // _driver.SEND_Constant(2, 65000, 0, 1);
            // _driver.SEND_Constant(-2, 65000, 0, 1);

            var overShoot = 100;
            var segmentation = 4;
            for (var i = 0; i < 400 / segmentation; ++i)
            {
                _driver.SEND_TransitionRPM(-overShoot, 0, 1500, 0);
                _driver.SEND_TransitionRPM(segmentation + overShoot, 0, 1500, 0);
                //_positionController.SetPosition(i * segmentation - overShoot);
                //_positionController.SetPosition(i * segmentation + overShoot + segmentation);
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

            var tracer = new PathTracer2D();

            var maxAcceleration = 20 * 400;
            var direction1 = new Vector(400, 2);
            var direction2 = new Vector(20, 400);
            direction1.Normalize();
            direction2.Normalize();

            tracer.AppendAcceleration(direction1 * maxAcceleration, 0.2);
            tracer.Continue(2);
            tracer.AppendAcceleration(direction2 * maxAcceleration, 0.2);
            tracer.Continue(2);
            tracer.AppendAcceleration(direction1 * maxAcceleration, 0.2);
            tracer.Continue(2);
            tracer.Execute(_driver);

            return;


            /*_driver.StepperIndex = 2;
            _driver.SEND_Constant(25, 1000000, 0, 0);
            _driver.SEND_Constant(10000, 2500, 0, 0);

            _driver.StepperIndex = 2;
            _driver.SEND_Constant(10000, 2000, 0, 0);
            _driver.SEND_Constant(10000, 2000, 0, 0);
            return;*/

            /*/
            _driver.StepperIndex = 2;
            //_driver.SEND_Constant(0, 1600, 0, 0);
            //_driver.SEND_Constant(400 * 50, 800, 0, 0);
            _driver.SEND_Acceleration(6250, 2002, 9363);
            _driver.SEND_Acceleration(310, 40050, 468);

            _driver.StepperIndex = 2;
            _driver.SEND_Constant(-6250, 310 * 6, 0, 0);
            _driver.SEND_Constant(-310, 6250 * 6, 0, 0);
            return;
             /**/

            var xDelta = 9000;
            var yDelta = 1000;

            StraightLinePlanner2D.AcceleratedTransition(xDelta, yDelta, _driver);

            var length = 5000;
            for (var i = 70; i < 80; ++i)
            {
                var radAngle = i / Math.PI / 2;
                var point = point2D(Math.Sin(radAngle), Math.Cos(radAngle), length);
                //StraightLinePlanner2D.AcceleratedTransition(point.X, point.Y, _driver);
                //StraightLinePlanner2D.AcceleratedTransition(-point.X, -point.Y, _driver);
            }


            /*  while (_driver.IncompletePlanCount > 0)
                  Thread.Sleep(1);

              Thread.Sleep(1000);

              var time = Math.Max(Math.Abs(xDelta * 2000), Math.Abs(yDelta * 2000));
              StraightLinePlanner2D.SendTransition2(-xDelta, -yDelta, time, _driver);*/
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            var squareSize = 3000;
            var topSpeed = 300;
            var diagonalDistance = 300;

            //do a square border
            acceleratedLine(squareSize, 0, topSpeed);
            acceleratedLine(0, squareSize, topSpeed);
            acceleratedLine(-squareSize, 0, topSpeed);
            acceleratedLine(0, -squareSize, topSpeed);
            //left right diagonals
            var diagonalCount = squareSize / diagonalDistance;
            for (var i = 0; i < diagonalCount * 2; ++i)
            {
                var diagLength = (diagonalCount - Math.Abs(i - diagonalCount)) * diagonalDistance;

                if (i % 2 == 0)
                {
                    acceleratedLine(-diagLength, diagLength, topSpeed);
                    if (i < diagonalCount)
                        acceleratedLine(0, diagonalDistance, topSpeed);
                    else
                        acceleratedLine(diagonalDistance, 0, topSpeed);
                }
                else
                {
                    acceleratedLine(diagLength, -diagLength, topSpeed);
                    if (i < diagonalCount)
                        acceleratedLine(diagonalDistance, 0, topSpeed);
                    else
                        acceleratedLine(0, diagonalDistance, topSpeed);
                }
            }
        }

        private void acceleratedLine(int x, int y, int maxSpeed)
        {
            var maxAccelerationDistance = Math.Max(Math.Abs(x / 2), Math.Abs(y / 2));

            var accelerationX = _driver.CalculateBoundedAcceleration(_driver.StartDeltaT, (UInt16)maxSpeed, (Int16)(Math.Sign(x) * maxAccelerationDistance));
            var accelerationY = _driver.CalculateBoundedAcceleration(_driver.StartDeltaT, (UInt16)maxSpeed, (Int16)(Math.Sign(y) * maxAccelerationDistance));

            var decelX = accelerationX.Invert();
            var decelY = accelerationY.Invert();

            _driver.StepperIndex = 2;
            _driver.SEND(accelerationX);
            _driver.SEND(accelerationY);

            var remainingX = x - decelX.StepCount - accelerationX.StepCount;
            var remainingY = y - decelY.StepCount - accelerationY.StepCount;

            while (_driver.HasSteps(remainingX) || _driver.HasSteps(remainingY))
            {
                var sliceX = _driver.GetStepSlice(remainingX);
                var sliceY = _driver.GetStepSlice(remainingY);

                remainingX -= sliceX;
                remainingY -= sliceY;

                _driver.StepperIndex = 2;
                _driver.SEND_Constant(sliceX, accelerationX.EndDeltaT, 0, 0);
                _driver.SEND_Constant(sliceY, accelerationY.EndDeltaT, 0, 0);
            }

            _driver.StepperIndex = 2;
            _driver.SEND(decelX);
            _driver.SEND(decelY);
        }

        private void IsSpeedTesterEnabled_Checked(object sender, RoutedEventArgs e)
        {
            IsPositionTesterEnabled.IsChecked = false;
            _speedController.Start();
        }

        private void IsSpeedTesterEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            _speedController.Stop();
        }
        private void IsReversed_Unchecked(object sender, RoutedEventArgs e)
        {
            _speedController.Direction = !_speedController.Direction;
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_speedController == null)
                return;

            var rpm = (int)Speed.Value;
            RPMDisplay.Text = rpm + "rpm";

            _speedController.SetRPM(rpm);
        }

        private void IsPositionTesterEnabled_Checked(object sender, RoutedEventArgs e)
        {
            IsSpeedTesterEnabled.IsChecked = false;
        }

        private void Position_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsPositionTesterEnabled.IsChecked.Value)
                return;

            _positionTimer.Stop();
            _positionTimer.Start();//reset the timer

            var steps = (int)Position.Value;
            StepDisplay.Text = steps.ToString();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var trajectoryPoints = createSpiral();
            var trajectory = new Trajectory4D(trajectoryPoints);

            var planner = new StraightLinePlanner2D(trajectory, new Velocity(1, 300), null);
            planner.Run(_driver);
        }

        private IEnumerable<Point4D> createHeart()
        {
            var top = new List<Point4D>();
            var bottom = new List<Point4D>();

            var smoothness = 200;
            var scale = 5000;

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

        private IEnumerable<Point4D> createTriangle()
        {
            return new[]{
                point2D(0,0),
                point2D(4000,2000),
                point2D(-4000,2000),
                point2D(0,0)
            };
        }

        private IEnumerable<Point4D> createCircle()
        {
            var circlePoints = new List<Point4D>();
            var r = 5000;
            var smoothness = 5;
            for (var i = 0; i <= 360 * smoothness; ++i)
            {
                var x = Math.Sin(i * Math.PI / 180 / smoothness);
                var y = Math.Cos(i * Math.PI / 180 / smoothness);
                circlePoints.Add(point2D(x, y, r));
            }
            return circlePoints;
        }

        private IEnumerable<Point4D> createLine()
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

        private IEnumerable<Point4D> createSpiral()
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

        private Point4D point2D(int x, int y)
        {
            return new Point4D(0, 0, x, y);
        }

        private Point4D point2D(double x, double y, double scale)
        {
            return new Point4D(0, 0, (int)Math.Round(x * scale), (int)Math.Round(y * scale));
        }

        private void MaxSpeed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_coord2DController != null)
                _coord2DController.SetSpeed(int.Parse(MaxSpeed.Text));
        }

        private void startTransition(int dirX, int dirY)
        {
            _coord2DController.SetMovement(dirX, dirY);
        }

        private void stopTransition()
        {
            startTransition(0, 0);
        }

        private void UpB_MouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(0, 1);
        }

        private void UpB_MouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }

        private void LeftB_MouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(1, 0);
        }

        private void LeftB_MouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }

        private void RightB_MouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(-1, 0);
        }

        private void RightB_MouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }

        private void BottomB_MouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(0, -1);
        }

        private void BottomB_MouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }


        private void LeftUpB_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(1, 1);
        }

        private void LeftUpB_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }

        private void UpRightB_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(-1, 1);
        }

        private void UpRightB_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }

        private void LeftBottomB_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(1, -1);
        }

        private void LeftBottomB_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }

        private void BottomRightB_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            startTransition(-1, -1);
        }

        private void BottomRightB_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            stopTransition();
        }


    }
}
