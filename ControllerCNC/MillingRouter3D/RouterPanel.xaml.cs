using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ControllerCNC;
using ControllerCNC.GUI;
using ControllerCNC.Loading;
using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

using MillingRouter3D.GUI;
using MillingRouter3D.Primitives;

namespace MillingRouter3D
{
    /// <summary>
    /// Interaction logic for RouterPanel.xaml
    /// </summary>
    public partial class RouterPanel : Window, ILoadProvider
    {
        internal static readonly string[] Modes =
       {
            "Join mode",
            "Scaffolding"
        };

        internal readonly CoordController CoordController;

        internal MillingWorkspacePanel Workspace { get; private set; }

        internal readonly DriverCNC2 Cnc;

        private static string _autosaveFile = "workspace_autosave.mwspc";

        private string _workspaceFile;

        private int _tr_dirX, _tr_dirY, _tr_dirZ;

        private readonly MillingShapeFactory _factory;

        private readonly HashSet<Control> _motionCommands = new HashSet<Control>();

        private readonly HashSet<ToggleButton> _transitionToggles = new HashSet<ToggleButton>();

        private readonly DispatcherTimer _statusTimer = new DispatcherTimer();

        private readonly DispatcherTimer _messageTimer = new DispatcherTimer();

        private readonly DispatcherTimer _autosaveTime = new DispatcherTimer();

        private readonly int _messageShowDelay = 3000;

        private int _lastRemainingSeconds = 0;

        private double _positionOffsetY = 0;

        private double _positionOffsetX = 0;

        private double _zLevel = 0;

        private WorkspaceMode _currentMode = WorkspaceMode.Idle;

        private bool _addJoinsEnabled = false;

        private bool _allowCommands = false;

        private DateTime _planStart;

        private MillingItem _joinItemCandidate;

        /// <summary>
        /// Plan builder that currently streams instructions to cnc.
        /// </summary>
        private PlanBuilder3D _planStreamer;

        public RouterPanel()
        {
            Configuration.EnableRouterMode();
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(this);
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = customCulture;
            SystemUtilities.PreventSleepMode();

            InitializeComponent();
            MessageBox.Visibility = Visibility.Hidden;

            _motionCommands.Add(Calibration);
            _motionCommands.Add(GoToZerosXY);
            _motionCommands.Add(StartPlan);
            _motionCommands.Add(SetZLevel);
            _motionCommands.Add(Speed);

            Cnc = new DriverCNC2();
            Cnc.OnConnectionStatusChange += () => Dispatcher.Invoke(refreshConnectionStatus);
            Cnc.OnHomeCalibrated += () => Dispatcher.Invoke(enableMotionCommands);

            Cnc.Initialize();

            CoordController = new CoordController(Cnc);

            _messageTimer.Interval = TimeSpan.FromMilliseconds(_messageShowDelay);
            _messageTimer.IsEnabled = false;
            _messageTimer.Tick += _messageTimer_Tick;
            _statusTimer.Interval = TimeSpan.FromMilliseconds(20);
            _statusTimer.Tick += _statusTimer_Tick;
            _statusTimer.IsEnabled = true;
            _autosaveTime.IsEnabled = false;
            _autosaveTime.Interval = TimeSpan.FromMilliseconds(1000);
            _autosaveTime.Tick += _autosaveTime_Tick;

            PreviewKeyUp += previewKeyUp;
            PreviewKeyDown += previewKeyDown;
            ContextMenu = createWorkspaceMenu();

            resetWorkspace(true);

            initializeTransitionHandlers();

            _factory = new MillingShapeFactory(this);
        }

        #region Panel service

        public ReadableIdentifier UnusedVersion(ReadableIdentifier identifier)
        {
            return Workspace.UnusedVersion(identifier);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            Keyboard.Focus(this);
        }
        #endregion

        #region Message handling

        public void ShowError(string message, bool forceRefresh = false)
        {
            setMessageText(message, Brushes.DarkRed, Brushes.Red, forceRefresh);
        }

        public void ShowMessage(string message, bool forceRefresh = false)
        {
            setMessageText(message, Brushes.DarkOrchid, Brushes.Orchid, forceRefresh);
        }

        public void HideMessage()
        {
            _messageTimer.IsEnabled = false;
            Message.Text = "";
            MessageBox.Visibility = Visibility.Hidden;
        }

        private void setMessageText(string message, Brush borderColor, Brush color, bool forceRefresh)
        {
            Message.Text = message;
            Message.Background = color;
            Message.BorderBrush = borderColor;
            MessageBox.Visibility = Visibility.Visible;

            if (forceRefresh)
            {
                MessageBox.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            }
        }

        private void _messageTimer_Tick(object sender, EventArgs e)
        {
            HideMessage();
        }

        #endregion

        #region Workspace handling

        private void refreshFocus()
        {
            Focus();
        }

        private void resetWorkspace(bool withReload)
        {
            _workspaceFile = _autosaveFile;
            if (Workspace != null)
            {
                _autosaveTime.IsEnabled = false;
            }

            var limitPoint = PlanBuilder3D.GetPositionFromSteps(Configuration.MaxStepsU, Configuration.MaxStepsV, Configuration.MaxStepsX, Configuration.MaxStepsY);
            Workspace = new MillingWorkspacePanel(this, limitPoint.X, limitPoint.Y, limitPoint.Z);
            WorkspaceSlot.Child = Workspace;
            if (withReload)
            {
                reloadWorkspace();
            }

            Workspace.OnWorkItemListChanged += refreshItemList;
            Workspace.OnSettingsChanged += onSettingsChanged;
            Workspace.OnWorkItemClicked += onItemClicked;
            Speed.Value = 1000 * Workspace.CuttingSpeedMm;
            CuttingKerf.Text = Workspace.CuttingKerf.ToString();
            MaxLayerCut.Text = Workspace.MaxLayerCut.ToString();

            refreshItemList();
            onSettingsChanged();
        }

        private void reloadWorkspace()
        {
            if (!File.Exists(_workspaceFile))
                return;

            try
            {
                Workspace.LoadFrom(_workspaceFile);
            }
            /*   catch (Exception ex) when (
                       ex is System.Reflection.TargetInvocationException ||
                       ex is System.Runtime.Serialization.SerializationException ||
                       ex is InvalidCastException
                   )
               {
                   ShowError("Saved workspace cannot be load (it's an old version).");
                   if (_workspaceFile != _autosaveFile)
                   {
                       _workspaceFile = _autosaveFile;
                       reloadWorkspace();
                   }
                   else
                   {
                       var i = 0;
                       var backupFile = _workspaceFile + ".bak";
                       while (File.Exists(backupFile))
                       {
                           i += 1;
                           backupFile = _workspaceFile + "." + i + ".bak";
                       }
                       //keep old autosave
                       File.Move(_autosaveFile, backupFile);
                   }
               }*/
            finally
            {

            }

            Speed.Value = 1000 * Workspace.CuttingSpeedMm;
            CuttingKerf.Text = Workspace.CuttingKerf.ToString();
            MaxLayerCut.Text = Workspace.MaxLayerCut.ToString();
        }

        private void onItemClicked(MillingWorkspaceItem item)
        {
            if (!_addJoinsEnabled || !(item is MillingItem))
                //nothing to do
                return;

            if (_joinItemCandidate != null)
                _joinItemCandidate.IsHighlighted = false;

            if (item == _joinItemCandidate)
            {
                //selection was discarded
                _joinItemCandidate = null;
                return;
            }

            var pointProvider = item as MillingItem;
            pointProvider.IsHighlighted = true;
            if (_joinItemCandidate == null || pointProvider is EntryPoint)
            {
                //first item is set (entry point has to be first)
                _joinItemCandidate = pointProvider;
                return;
            }

            Workspace.SetJoin(_joinItemCandidate, pointProvider);
            _joinItemCandidate = pointProvider;
        }

        private void onSettingsChanged()
        {
            //reset autosave timer
            _autosaveTime.Stop();
            _autosaveTime.Start();
        }

        private void refreshItemList()
        {
            WorkItemList.Items.Clear();

            foreach (var child in Workspace.Children)
            {
                var workItem = child as MillingWorkspaceItem;
                if (workItem == null)
                    continue;

                var item = createListItem(workItem);
                foreach (var join in Workspace.GetIncomingJoins(workItem))
                {
                    var joinItem = createListItem(join);
                    WorkItemList.Items.Add(joinItem);
                }
                WorkItemList.Items.Add(item);
            }
        }

        private ContextMenu createWorkspaceMenu()
        {
            var menu = new ContextMenu();

            var centerCutPanel = new MenuItem();

            return menu;
        }

        private ListBoxItem createListItem(MillingJoin join)
        {
            var item = new ListBoxItem();
            item.Content = join.Item1.Name + " --> " + join.Item2.Name;
            item.Tag = join;
            item.Foreground = Brushes.Red;

            var menu = new ContextMenu();

            var refreshItem = new MenuItem();
            refreshItem.Header = "Refresh";
            refreshItem.Click += (e, s) =>
            {
                throw new NotImplementedException();
            };
            menu.Items.Add(refreshItem);

            var deleteItem = new MenuItem();
            deleteItem.Header = "Delete";
            deleteItem.Click += (e, s) =>
            {
                Workspace.RemoveJoin(join);
            };
            menu.Items.Add(deleteItem);

            item.ContextMenu = menu;

            return item;
        }

        private ListBoxItem createListItem(MillingWorkspaceItem workItem)
        {
            var item = new ListBoxItem();
            item.Content = workItem.Name;
            item.Tag = workItem;

            var shapeItem = workItem as MillingShapeItem2D;
            if (shapeItem != null)
            {
                var menu = new ContextMenu();

                var copyItem = new MenuItem();
                copyItem.Header = "Copy";
                copyItem.Click += (e, s) =>
                {
                    Workspace.Children.Add(shapeItem.Clone(Workspace.UnusedVersion(shapeItem.Name)));
                };
                menu.Items.Add(copyItem);

                var deleteItem = new MenuItem();
                deleteItem.Header = "Delete";
                deleteItem.Click += (e, s) =>
                {
                    Workspace.Children.Remove(shapeItem);
                };
                menu.Items.Add(deleteItem);

                item.ContextMenu = menu;
            }
            return item;
        }


        private void previewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.System)
            {
                setMode(WorkspaceMode.JoinMode);
                e.Handled = true;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _allowCommands = true;
                e.Handled = true;
                refreshTransitionSpeed();
            }

            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                setMode(WorkspaceMode.ScaffoldMode);
                e.Handled = true;
            }

            switch (e.Key)
            {
                case Key.Down:
                    setKeyTransition(0, 1, 0, e);
                    break;
                case Key.Up:
                    setKeyTransition(0, -1, 0, e);
                    break;
                case Key.Left:
                    setKeyTransition(-1, 0, 0, e);
                    break;
                case Key.Right:
                    setKeyTransition(1, 0, 0, e);
                    break;
                case Key.Add:
                case Key.PageDown:
                    setKeyTransition(0, 0, 1, e);
                    break;

                case Key.PageUp:
                case Key.Subtract:
                    setKeyTransition(0, 0, -1, e);
                    break;
            }
        }

        private void previewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                //delete all selected items
                var itemsCopy = WorkItemList.SelectedItems.OfType<object>().ToArray();
                foreach (ListBoxItem item in itemsCopy)
                {
                    var join = item.Tag as MillingJoin;
                    if (join != null)
                    {
                        Workspace.RemoveJoin(join);
                    }

                    var workItem = item.Tag as MillingWorkspaceItem;
                    if (workItem == null || workItem is EntryPoint)
                        continue;

                    Workspace.Children.Remove(workItem);
                }
            }

            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.System)
            {
                setMode(WorkspaceMode.Idle);
                e.Handled = true;
            }

            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                setMode(WorkspaceMode.Idle);
                e.Handled = true;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _allowCommands = false;
                e.Handled = true;
                refreshTransitionSpeed();
            }

            switch (e.Key)
            {

                case Key.Down:
                case Key.Up:
                    resetKeyTransition(false, true, false, e);
                    break;
                case Key.Left:
                case Key.Right:
                    resetKeyTransition(true, false, false, e);
                    break;
                case Key.PageUp:
                case Key.PageDown:
                case Key.Add:
                case Key.Subtract:
                    resetKeyTransition(false, false, true, e);
                    break;
            }
        }

        private void setKeyTransition(int dirX, int dirY, int dirZ, KeyEventArgs e)
        {
            if (dirX != 0)
                _tr_dirX = dirX;

            if (dirY != 0)
                _tr_dirY = dirY;

            if (dirZ != 0)
                _tr_dirZ = dirZ;

            e.Handled = true;
            refreshTransitionSpeed();
        }

        private void resetKeyTransition(bool x, bool y, bool z, KeyEventArgs e)
        {
            if (x)
                _tr_dirX = 0;

            if (y)
                _tr_dirY = 0;

            if (z)
                _tr_dirZ = 0;

            e.Handled = true;
            refreshTransitionSpeed();
        }

        private void _autosaveTime_Tick(object sender, EventArgs e)
        {
            _autosaveTime.IsEnabled = false;
            Workspace.SaveTo(_workspaceFile);
        }

        #endregion

        #region CNC machine status handling

        private void refreshConnectionStatus()
        {
            if (Cnc.IsConnected)
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

        private Point3Dmm getCurrentPosition()
        {
            var currentU = Cnc.EstimationU;
            var currentV = Cnc.EstimationV;
            var currentX = Cnc.EstimationX;
            var currentY = Cnc.EstimationY;

            var prePosition = PlanBuilder3D.GetPositionFromSteps(currentU, currentV, currentX, currentY);

            var position = new Point3Dmm(prePosition.X, prePosition.Y, prePosition.Z);
            return position;
        }

        private void _statusTimer_Tick(object sender, EventArgs e)
        {
            var position = getCurrentPosition();
            PositionX.Text = (position.X - _positionOffsetX).ToString("0.000");
            //TODO the axis twisting exists just because windows coord system.
            PositionY.Text = (Workspace.RangeY - position.Y - _positionOffsetY).ToString("0.000");
            PositionZ.Text = (position.Z - _zLevel).ToString("0.000");

            Workspace.HeadXYZ.Position = position;

            if (_planStreamer != null)
            {
                var elapsedSeconds = (int)Math.Round((DateTime.Now - _planStart).TotalSeconds);
                var totalSeconds = (int)Math.Round(_planStreamer.TotalSeconds)+Cnc.RemainingPlanTickEstimation;

                _lastRemainingSeconds = totalSeconds - elapsedSeconds;
                var remainingTime = new TimeSpan(0, 0, 0, _lastRemainingSeconds);
                var elapsedTime = new TimeSpan(0, 0, 0, elapsedSeconds);

                if (remainingTime.TotalDays < 2)
                {
                    ShowMessage(elapsedTime.ToString() + "/" + remainingTime.ToString());
                }
            }

            if (Cnc.CurrentState.IsHomeCalibrated)
            {
                Calibration.Foreground = Brushes.Black;
            }
            else
            {
                var blinkFrequency = 1000;
                if (DateTime.Now.Millisecond % blinkFrequency > blinkFrequency / 2)
                {
                    Calibration.Foreground = Brushes.Red;
                }
                else
                {
                    Calibration.Foreground = Brushes.Green;
                }
            }
        }


        #endregion

        #region Plan execution

        private void pausePlan()
        {
            if (_planStreamer == null)
                return;

            if (_planStreamer.IsStreaming)
            {
                _planStreamer.Stop();
                setPauseControl(isPaused: true);
            }
            else
            {
                _planStreamer.Continue();
                setPauseControl(isPaused: false);
            }
        }

        private void stopPlan()
        {
            if (_planStreamer == null)
                return;

            _planStreamer.Stop();
            planCompleted();
        }

        private void executePlan()
        {
            if (!Cnc.IsHomeCalibrated)
            {
                ShowError("Calibration is required!");
                return;
            }

            disableMotionCommands();
            Workspace.DisableChanges();
            activateExecutionControls();

            var state = Cnc.PlannedState;
            var currentPosition = PlanBuilder3D.GetPosition(state);

            var startPoint = Workspace.EntryPoint;
            var start = new Point3Dmm(startPoint.PositionX, startPoint.PositionY, _zLevel);
            var aboveStart = new Point3Dmm(start.X, start.Y, currentPosition.Z);

            var builder = new PlanBuilder3D(currentPosition.Z, _zLevel, Workspace.CuttingSpeed, Configuration.MaxPlaneSpeed, Configuration.MaxPlaneAcceleration);
            builder.SetPosition(currentPosition);

            builder.AddRampedLine(aboveStart);

            try
            {
                if (_zLevel <= aboveStart.Z)
                    throw new PlanningException("Level Z is above the current position.");

                Workspace.BuildPlan(builder);
            }
            catch (PlanningException ex)
            {
                planCompleted();
                ShowError(ex.Message);
                return;
            }

            var plan = builder.Build();
            if (!Cnc.Check(plan))
            {
                planCompleted();
                ShowError("Plan overflows the workspace!");
                return;
            }

            CheckEngineDialog.WaitForConfirmation(() =>
            {
                /**/
                builder.SetStreamingCuttingSpeed(getCuttingSpeed());
                builder.StreamingIsComplete += planCompleted;/*/
                Cnc.SEND(plan);
                Cnc.OnInstructionQueueIsComplete += planCompleted;/**/
                _planStreamer = builder;
                _planStart = DateTime.Now;
                builder.StreamInstructions(Cnc);
                this.Focus();
            });
        }

        private void planCompleted()
        {
            _planStreamer = null;
            _lastRemainingSeconds = 0;
            Cnc.OnInstructionQueueIsComplete -= planCompleted;
            Workspace.EnableChanges();
            enableMotionCommands();
            deactiveExecutionControls();
        }


        #endregion

        #region Controls implementation

        private void setPauseControl(bool isPaused)
        {
            if (isPaused)
            {
                PausePlan.Content = "Continue";
            }
            else
            {
                PausePlan.Content = "Pause";
            }
        }

        private void activateExecutionControls()
        {
            Dispatcher.Invoke(() =>
            {
                StartPlan.IsEnabled = false;
                StopPlan.IsEnabled = true;
                PausePlan.IsEnabled = true;
                setPauseControl(isPaused: false);
            });
        }

        private void deactiveExecutionControls()
        {
            Dispatcher.Invoke(() =>
            {
                StartPlan.IsEnabled = true;
                StopPlan.IsEnabled = false;
                PausePlan.IsEnabled = false;
                setPauseControl(isPaused: false);
            });
        }


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
            initializeTransitionHandlers(UpB, 0, -1, 0);
            initializeTransitionHandlers(BottomB, 0, 1, 0);
            initializeTransitionHandlers(LeftB, -1, 0, 0);
            initializeTransitionHandlers(RightB, 1, 0, 0);

            initializeTransitionHandlers(LeftUpB, -1, -1, 0);
            initializeTransitionHandlers(UpRightB, 1, -1, 0);
            initializeTransitionHandlers(LeftBottomB, -1, 1, 0);
            initializeTransitionHandlers(BottomRightB, 1, 1, 0);
            initializeTransitionHandlers(AscendB, 0, 0, -1);
            initializeTransitionHandlers(DescendB, 0, 0, 1);

            HoldMovement.Unchecked += (s, e) => setTransition(null, 0, 0, 0);
            Speed.Value = 1000 * Workspace.CuttingSpeedMm;
            MoveByCuttingSpeed.Checked += (s, e) => refreshTransitionSpeed();
            MoveByCuttingSpeed.Unchecked += (s, e) => refreshTransitionSpeed();
            refreshTransitionSpeed();

            deactiveExecutionControls();
        }

        private void initializeTransitionHandlers(ToggleButton button, int dirX, int dirY, int dirZ)
        {
            button.PreviewMouseDown += (s, e) => setTransition(button, dirX, dirY, dirZ);
            button.Checked += (s, e) =>
            {
                if (!HoldMovement.IsChecked.Value)
                {
                    button.IsChecked = false;
                }
            };
            button.PreviewMouseUp += (s, e) =>
            {
                if (!HoldMovement.IsChecked.Value)
                {
                    setTransition(null, 0, 0, 0);
                }
            };
            button.Unchecked += (s, e) => setTransition(null, 0, 0, 0);
            _motionCommands.Add(button);
            _transitionToggles.Add(button);
        }

        private void setTransition(ToggleButton button, int dirX, int dirY, int dirZ)
        {
            foreach (var buttonToDisable in _transitionToggles)
            {
                if (buttonToDisable == button)
                    continue;

                buttonToDisable.IsChecked = false;
            }

            _tr_dirX = dirX;
            _tr_dirY = dirY;
            _tr_dirZ = dirZ;
            refreshTransitionSpeed();
        }

        private void refreshTransitionSpeed()
        {
            if (CoordController == null)
                return;

            if (_planStreamer != null)
                Speed.IsEnabled = _allowCommands;

            var speed = getTransitionSpeed().ToMetric();
            var speeds = PlanBuilder3D.GetPositionRev(_tr_dirX * speed, _tr_dirY * speed, _tr_dirZ * speed);
            CoordController.SetDesiredSpeeds(speeds.U, speeds.V, speeds.X, speeds.Y);
            var builder = _planStreamer;
            if (builder != null)
                builder.SetStreamingCuttingSpeed(getCuttingSpeed());
        }

        private Speed getCuttingSpeed()
        {
            return ControllerCNC.Primitives.Speed.FromMilimetersPerSecond(Workspace.CuttingSpeedMm);
        }

        private Speed getTransitionSpeed()
        {
            if (_allowCommands)
                return Configuration.MaxPlaneSpeed;

            if (MoveByCuttingSpeed.IsChecked.Value)
            {
                return getCuttingSpeed();
            }
            else
            {
                return Configuration.MaxPlaneSpeed;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SetZLevel_Click(object sender, RoutedEventArgs e)
        {
            var position = getCurrentPosition();
            _zLevel = position.Z;
        }

        private void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            disableMotionCommands();
            Cnc.SEND(new HomingInstruction());
        }

        private void ResetXYCoordinates_Click(object sender, RoutedEventArgs e)
        {
            var position = getCurrentPosition();
            _positionOffsetX = position.X;
            _positionOffsetY = position.Y;
        }

        private void GoToZerosXY_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StartPlan_Click(object sender, RoutedEventArgs e)
        {
            executePlan();
        }

        private void StopPlan_Click(object sender, RoutedEventArgs e)
        {
            stopPlan();
        }

        private void PausePlan_Click(object sender, RoutedEventArgs e)
        {
            pausePlan();
        }

        private void AddShape_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "All supported files|*.jpeg;*.jpg;*.png;*.bmp;*.dat;*.cor;*.gcode;*.nc|Image files|*.jpeg;*.jpg;*.png;*.bmp|Coordinate files|*.dat;*.cor;*.gcode;*.nc";



            if (dlg.ShowDialog().Value)
            {
                var filename = dlg.FileName;
                var shape = _factory.Load(filename);

                if (shape != null)
                {
                    Workspace.Children.Add(shape);
                }
            }
        }

        private void RefreshJoins_Click(object sender, RoutedEventArgs e)
        {
            Workspace.RefreshJoins();
        }

        private void setMode(WorkspaceMode mode)
        {
            if (_currentMode == mode)
                //nothing to change
                return;

            if (mode == WorkspaceMode.Idle)
            {
                _currentMode = WorkspaceMode.LastMode;
                ModeSwitch.IsChecked = false;
                return;
            }

            if (!ModeSwitch.IsChecked.Value)
            {
                _currentMode = mode;
                ModeSwitch.IsChecked = true;
                return;
            }

            _currentMode = mode - 1;
            ModeSwitch.IsChecked = false;
        }

        private void ModeSwitch_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentMode == WorkspaceMode.Idle)
                //idle mode is never checked 
                _currentMode = WorkspaceMode.JoinMode;

            switch (_currentMode)
            {
                case WorkspaceMode.JoinMode:
                    _addJoinsEnabled = true;
                    _joinItemCandidate = null;

                    Workspace.ScaffoldModeEnabled = false;
                    break;
                case WorkspaceMode.ScaffoldMode:
                    Workspace.ScaffoldModeEnabled = true;

                    _addJoinsEnabled = false;
                    _joinItemCandidate = null;
                    break;

                default:
                    throw new NotImplementedException("Workspace mode");
            }

            ModeSwitch.Content = Modes[(int)_currentMode - 1];
        }

        private void ModeSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            _addJoinsEnabled = false;
            if (_joinItemCandidate != null)
                _joinItemCandidate.IsHighlighted = false;
            _joinItemCandidate = null;
            Workspace.ScaffoldModeEnabled = false;

            var nextMode = (int)(_currentMode + 1);
            if (nextMode > (int)WorkspaceMode.LastMode)
                nextMode = 0;

            _currentMode = (WorkspaceMode)nextMode;
            if (_currentMode == WorkspaceMode.Idle)
            {
                ModeSwitch.Content = Modes[0];
            }
            else
            {
                //recheck the switch
                ModeSwitch.IsChecked = true;
            }
        }

        private void CuttingDeltaT_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var speed = Speed.Value / 1000;
            refreshTransitionSpeed();

            if (Workspace != null)
                Workspace.CuttingSpeedMm = speed;

            CuttingSpeed.Text = string.Format("{0:0.000}mm/s", speed);
        }

        private void ToolKerf_TextChanged(object sender, TextChangedEventArgs e)
        {
            double kerf;
            double.TryParse(CuttingKerf.Text, out kerf);
            if (Workspace != null)
                Workspace.CuttingKerf = kerf;
        }


        private void MaxLayerCut_TextChanged(object sender, TextChangedEventArgs e)
        {
            double layerCut;
            double.TryParse(MaxLayerCut.Text, out layerCut);
            if (Workspace != null)
                Workspace.MaxLayerCut = layerCut;
        }


        private void LoadPlan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Cutter workspace|*.cwspc";
            if (dlg.ShowDialog() == true)
            {
                _workspaceFile = dlg.FileName;
                reloadWorkspace();
            }
        }

        private void SavePlan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Cutter workspace|*.cwspc";
            if (dlg.ShowDialog() == true)
            {
                _workspaceFile = dlg.FileName;
                Workspace.SaveTo(_workspaceFile);
            }
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            var stepI = new ConstantInstruction((short)4, 25000, 0);
            var waitI = new ConstantInstruction((short)0, 1000000, 0);
            var instruction = Axes.UVXY(waitI, stepI, waitI, waitI);
            Cnc.SEND(instruction);
        }

        private void NewPlan_Click(object sender, RoutedEventArgs e)
        {
            resetWorkspace(false);
        }

        private void MessageBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HideMessage();
        }

        #endregion
    }
}
