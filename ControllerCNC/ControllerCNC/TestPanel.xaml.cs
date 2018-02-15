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

using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

using ControllerCNC.Demos;

namespace ControllerCNC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class TestPanel : Window
    {
        DriverCNC _cnc;
        DispatcherTimer _positionTimer = new DispatcherTimer();
        DispatcherTimer _statusTimer = new DispatcherTimer();
        SpeedController _speedController;
        PositionController _positionController;
        Coord2DController _coord2DController;

        public TestPanel()
        {
            InitializeComponent();

            Output.ScrollToEnd();

            _cnc = new DriverCNC();
            _cnc.OnDataReceived += _driver_OnDataReceived;
            _cnc.Initialize();

            _positionController = new PositionController(_cnc);
            _speedController = new SpeedController(_cnc);
            _coord2DController = new Coord2DController(_cnc);

            _positionTimer.Interval = new TimeSpan(1 * 10 * 1000);
            _positionTimer.Tick += _positionTimer_Tick;
            _positionTimer.IsEnabled = false;

            _statusTimer.Interval = new TimeSpan(100 * 10 * 1000);
            _statusTimer.Tick += _statusTimer_Tick;
            _statusTimer.IsEnabled = true;
        }

        #region Machine status handling

        void _statusTimer_Tick(object sender, EventArgs e)
        {
            Status.Text = "Incomplete: " + _cnc.IncompleteInstructionCount;
        }

        void _driver_OnDataReceived(string data)
        {
            this.Dispatcher.BeginInvoke(((Action)(() =>
            {
                Output.AppendText(data);
                Output.ScrollToEnd();
            })));
        }

        #endregion

        #region Demo buttons handling.

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Execute(new HomingInstruction(),false);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Execute(MachineTesting.InterruptedRevolution);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Execute(MachineTesting.BackAndForwardAxisTraversal);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Execute(MachineTesting.AcceleratedMultiCross);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            // Execute(ShapeDrawing.DrawContinuousLines(ShapeDrawing.HeartCoordinates));
            Execute(MachineTesting.Shape4dTest, false);
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            Execute(ShapeDrawing.DrawSquareWithDiagonals);
        }

        private void Execute(Func<PlanBuilder> planProvider, bool duplicateUV = true)
        {
            Execute(planProvider(), duplicateUV);
        }

        private void Execute(PlanBuilder plan, bool duplicateUV = true)
        {
            Execute(plan.Build(), duplicateUV);
        }

        private void Execute(InstructionCNC instruction, bool duplicateUV = true)
        {
            Execute(new[] { instruction }, duplicateUV);
        }

        private void Execute(IEnumerable<InstructionCNC> plan, bool duplicateUV = true)
        {
            if (duplicateUV)
            {
                var builder = new PlanBuilder();
                builder.Add(plan);
                builder.DuplicateXYtoUV();
                _cnc.SEND(builder.Build());
            }
            else
            {
                _cnc.SEND(plan);
            }
        }

        #endregion

        #region Speed commands handling.

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

        #endregion

        #region Position commands handling.

        void _positionTimer_Tick(object sender, EventArgs e)
        {
            _positionTimer.Stop();

            var steps = (int)Position.Value - 200;
            _positionController.SetPosition(steps);
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

        #endregion

        #region Transition commands handling.

        private void MaxSpeed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_coord2DController != null)
                _coord2DController.SetSpeed(int.Parse(MaxSpeed.Text));
        }

        private void startTransition(int dirX, int dirY)
        {
            _coord2DController.SetSpeed(int.Parse(MaxSpeed.Text));
            _coord2DController.SetMovement(-dirX, -dirY);
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
        #endregion
    }
}
