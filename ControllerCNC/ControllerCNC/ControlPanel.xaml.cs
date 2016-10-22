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
            _driver.StepperIndex = 2;
            _driver.SEND_Constant(400, 1600, 0, 0);
            _driver.SEND_Constant(800, 800, 0, 0);
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


    }
}
