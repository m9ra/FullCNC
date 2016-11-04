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
using System.Windows.Shapes;

using System.Windows.Threading;

using ControllerCNC.Machine;
using ControllerCNC.Planning;

namespace ControllerCNC
{
    /// <summary>
    /// Interaction logic for CutterPane.xaml
    /// </summary>
    public partial class CutterPanel : Window
    {
        private readonly DriverCNC _cnc;

        private readonly Coord2DController _coordController;

        private readonly HashSet<Button> _motionCommands = new HashSet<Button>();

        private readonly DispatcherTimer _statusTimer = new DispatcherTimer();

        private int _positionOffsetX = 0;

        private int _positionOffsetY = 0;

        public CutterPanel()
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            InitializeComponent();

            _motionCommands.Add(Calibration);
            _motionCommands.Add(GoToZeros);
            _motionCommands.Add(AlignHeads);

            _cnc = new DriverCNC();
            _cnc.OnConnectionStatusChange += () => Dispatcher.Invoke(refreshConnectionStatus);
            _cnc.OnHomingEnded += () => Dispatcher.Invoke(enableMotionCommands);

            _cnc.Initialize();

            _coordController = new Coord2DController(_cnc);
            initializeTransitionHandlers();

            _statusTimer.Interval = TimeSpan.FromMilliseconds(100);
            _statusTimer.Tick += _statusTimer_Tick;
            _statusTimer.IsEnabled = true;
        }

        #region CNC machine status handling

        private void refreshConnectionStatus()
        {
            if (_cnc.IsConnected)
            {
                ConnectionStatus.Text = "Online";
                ConnectionStatus.Background = Brushes.Green;
            }
            else
            {
                ConnectionStatus.Text = "Offline";
                ConnectionStatus.Background = Brushes.Red;
            }
        }


        void _statusTimer_Tick(object sender, EventArgs e)
        {
            var positionX = Constants.MilimetersPerStep * (_cnc.ConfirmedPositionX - _positionOffsetX);
            var positionY = Constants.MilimetersPerStep * (_cnc.ConfirmedPositionY - _positionOffsetY);

            PositionX.Text = positionX.ToString("0.000");
            PositionY.Text = positionY.ToString("0.000");
        }


        #endregion

        #region Controls implementation

        private void enableMotionCommands()
        {
            foreach (var command in _motionCommands)
            {
                command.IsEnabled = true;
            }
        }

        private void disableMotionCommands()
        {
            foreach (var command in _motionCommands)
            {
                command.IsEnabled = false;
            }
        }

        private void initializeTransitionHandlers()
        {
            initializeTransitionHandlers(UpB, 0, 1);
            initializeTransitionHandlers(BottomB, 0, -1);
            initializeTransitionHandlers(LeftB, 1, 0);
            initializeTransitionHandlers(RightB, -1, 0);

            initializeTransitionHandlers(LeftUpB, 1, 1);
            initializeTransitionHandlers(UpRightB, -1, 1);
            initializeTransitionHandlers(LeftBottomB, 1, -1);
            initializeTransitionHandlers(BottomRightB, -1, -1);
        }

        private void initializeTransitionHandlers(Button button, int dirX, int dirY)
        {
            button.PreviewMouseDown += (s, o) => setTransition(dirX, dirY);
            button.PreviewMouseUp += (s, o) => setTransition(0, 0);
            _motionCommands.Add(button);
        }

        private void setTransition(int dirX, int dirY)
        {
            if (SetSlowTransition.IsChecked.Value)
            {
                _coordController.SetSpeed(Constants.StartDeltaT);
            }
            else
            {
                _coordController.SetSpeed(Constants.FastestDeltaT);
            }
            _coordController.SetMovement(dirX, dirY);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            disableMotionCommands();
            _cnc.SEND(new HomingInstruction());
        }

        private void ResetCoordinates_Click(object sender, RoutedEventArgs e)
        {
            _positionOffsetX = _cnc.ConfirmedPositionX;
            _positionOffsetY = _cnc.ConfirmedPositionY;
        }

        private void GoToZeros_Click(object sender, RoutedEventArgs e)
        {
            var planner = new PlanBuilder();
            var stepsX = _positionOffsetX - _cnc.ConfirmedPositionX;
            var stepsY = _positionOffsetY - _cnc.ConfirmedPositionY;
            planner.AddRampedLineXY(-stepsX, -stepsY, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            _cnc.SEND(planner.Build());
        }
        #endregion
    }
}
