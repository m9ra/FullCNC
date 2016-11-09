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

using System.Threading;
using System.Windows.Threading;

using ControllerCNC.GUI;
using ControllerCNC.Demos;
using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

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

        private readonly WorkspacePanel _workspace;

        private readonly WorkspaceItem _uvHead = new HeadCNC(Colors.Blue, true);

        private readonly WorkspaceItem _xyHead = new HeadCNC(Colors.Red, false);

        private int _positionOffsetU = 0;

        private int _positionOffsetV = 0;

        private int _positionOffsetX = 0;

        private int _positionOffsetY = 0;

        public CutterPanel()
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            InitializeComponent();

            _workspace = new WorkspacePanel(Constants.MaxStepsX, Constants.MaxStepsY);
            WorkspaceSlot.Child = _workspace;
            _workspace.Children.Add(_xyHead);
            _workspace.Children.Add(_uvHead);

            //var coordinates = ShapeDrawing.LoadCoordinatesCOR("HT22.COR");
            //var coordinates1 = ShapeDrawing.CircleCoordinates(6000);
            //var coordinates2 = ShapeDrawing.CircleCoordinates(4000);
            //var coordinates = ShapeDrawing.InterpolateImage("sun_green_mask.png",500,50,20);
            var coordinates = ShapeDrawing.InterpolateImage("snowflake2.png", 1500, 50, 20);
            //var coordinates = ShapeDrawing.HeartCoordinates();
            //var coordinates = ShapeDrawing.InterpolateImage("snowflake3.png", 1500, 50, 100);
            var shape1 = new TrajectoryShapeItem(new Trajectory4D(coordinates), _workspace);
            var yOffset = 20000;
            shape1.PositionX = 1000;
            shape1.PositionY = yOffset;

            var shape2 = new TrajectoryShapeItem(new Trajectory4D(coordinates), _workspace);
            shape2.PositionX = 25000;
            shape2.PositionY = yOffset;

            _workspace.EntryPoint.PositionX = 21000;
            _workspace.EntryPoint.PositionY = yOffset-5000;
            _workspace.SetJoin(_workspace.EntryPoint, shape1);
            _workspace.SetJoin(shape1, shape2);


            _motionCommands.Add(Calibration);
            _motionCommands.Add(GoToZeros);
            _motionCommands.Add(AlignHeads);
            _motionCommands.Add(StartPlan);

            _cnc = new DriverCNC();
            _cnc.OnConnectionStatusChange += () => Dispatcher.Invoke(refreshConnectionStatus);
            _cnc.OnHomingEnded += () => Dispatcher.Invoke(enableMotionCommands);

            _cnc.Initialize();

            _coordController = new Coord2DController(_cnc);
            initializeTransitionHandlers();

            _statusTimer.Interval = TimeSpan.FromMilliseconds(20);
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
            var state = _cnc.CompletedState;
            var positionU = Constants.MilimetersPerStep * (state.U - _positionOffsetU);
            var positionV = Constants.MilimetersPerStep * (state.V - _positionOffsetV);
            var positionX = Constants.MilimetersPerStep * (state.X - _positionOffsetX);
            var positionY = Constants.MilimetersPerStep * (state.Y - _positionOffsetY);

            PositionU.Text = positionU.ToString("0.000");
            PositionV.Text = positionV.ToString("0.000");
            PositionX.Text = positionX.ToString("0.000");
            PositionY.Text = positionY.ToString("0.000");

            _uvHead.PositionX = state.U;
            _uvHead.PositionY = state.V;

            _xyHead.PositionX = state.X;
            _xyHead.PositionY = state.Y;
        }


        #endregion

        #region Plan execution

        private void executePlan()
        {
            disableMotionCommands();
            _workspace.DisableChanges();

            var builder = new PlanBuilder();
            var point = _workspace.EntryPoint;

            var state = _cnc.PlannedState;
            var initX = point.PositionX - state.X;
            var initY = point.PositionY - state.Y;
            builder.AddRampedLineXY(initX, initY, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            _workspace.BuildPlan(builder);
            builder.AddRampedLineXY(-initX, -initY, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            builder.DuplicateXYtoUV();
            var plan = builder.Build();
            if (!_cnc.SEND(plan))
                throw new NotSupportedException("Invalid plan");

            _cnc.OnInstructionQueueIsComplete += planCompleted;
        }

        private void planCompleted()
        {
            _cnc.OnInstructionQueueIsComplete -= planCompleted;
            _workspace.EnableChanges();
            enableMotionCommands();
        }

        #endregion

        #region Controls implementation

        private void enableMotionCommands()
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var command in _motionCommands)
                {
                    command.IsEnabled = true;
                }
            });
        }

        private void disableMotionCommands()
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var command in _motionCommands)
                {
                    command.IsEnabled = false;
                }
            });
        }

        private void initializeTransitionHandlers()
        {
            initializeTransitionHandlers(UpB, 0, -1);
            initializeTransitionHandlers(BottomB, 0, 1);
            initializeTransitionHandlers(LeftB, -1, 0);
            initializeTransitionHandlers(RightB, 1, 0);

            initializeTransitionHandlers(LeftUpB, -1, -1);
            initializeTransitionHandlers(UpRightB, 1, -1);
            initializeTransitionHandlers(LeftBottomB, -1, 1);
            initializeTransitionHandlers(BottomRightB, 1, 1);
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
                _coordController.SetSpeed((int)(Constants.FoamCuttingSpeed.Ticks / Constants.FoamCuttingSpeed.StepCount));
            }
            else
            {
                _coordController.SetSpeed(Constants.FastestDeltaT);
            }
            _coordController.SetPlanes(MoveUV.IsChecked.Value, MoveXY.IsChecked.Value);
            _coordController.SetMovement(dirX, dirY);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AlignHeads_Click(object sender, RoutedEventArgs e)
        {
            var state = _cnc.PlannedState;
            var xSteps = state.X - state.U;
            var ySteps = state.Y - state.V;

            var builder = new PlanBuilder();
            builder.AddRampedLineXY(-xSteps, -ySteps, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            _cnc.SEND(builder.Build());
        }

        private void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            disableMotionCommands();
            _cnc.SEND(new HomingInstruction());
        }

        private void ResetCoordinates_Click(object sender, RoutedEventArgs e)
        {
            var state = _cnc.CompletedState;
            _positionOffsetU = state.U;
            _positionOffsetV = state.V;
            _positionOffsetX = state.X;
            _positionOffsetY = state.Y;
        }

        private void GoToZeros_Click(object sender, RoutedEventArgs e)
        {
            var state = _cnc.PlannedState;
            var stepsU = _positionOffsetU - state.U;
            var stepsV = _positionOffsetV - state.V;
            var stepsX = _positionOffsetX - state.X;
            var stepsY = _positionOffsetY - state.Y;

            var planner = new PlanBuilder();
            planner.AddRampedLineXY(stepsU, stepsV, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            planner.ChangeXYtoUV();
            _cnc.SEND(planner.Build());

            planner = new PlanBuilder();
            planner.AddRampedLineXY(stepsX, stepsY, Constants.MaxPlaneAcceleration, Constants.MaxPlaneSpeed);
            _cnc.SEND(planner.Build());
        }

        private void StartPlan_Click(object sender, RoutedEventArgs e)
        {
            executePlan();
        }
        #endregion
    }
}
