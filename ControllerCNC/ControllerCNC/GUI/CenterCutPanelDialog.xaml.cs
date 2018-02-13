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

using ControllerCNC.Planning;
using ControllerCNC.Machine;

namespace ControllerCNC.GUI
{

    internal enum CenterCutState { Negative = -1, Zero = 0, Positive = 1 };

    public partial class CenterCutPanelDialog : Window
    {
        private readonly CutterPanel _panel;

        private CenterCutState _state = CenterCutState.Zero;

        public CenterCutPanelDialog(CutterPanel panel)
        {
            InitializeComponent();

            _panel = panel;

            KeyUp += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    this.Close();
            };

            Elevation.TextChanged += (e, s) => refreshWindow();

            Up.PreviewMouseDown += Up_MouseDown;
            Up.PreviewMouseUp += stopMovement;
            Up.MouseLeave += (e, s) => stopMovement(null, null);


            Down.PreviewMouseDown += Down_MouseDown;
            Down.PreviewMouseUp += stopMovement;
            Down.MouseLeave += (e, s) => stopMovement(null, null);

            SetPositive.Click += (e, s) => setElevation(CenterCutState.Positive);
            SetZero.Click += (e, s) => setElevation(CenterCutState.Zero);
            SetNegative.Click += (e, s) => setElevation(CenterCutState.Negative);

            refreshStateButtons();
        }

        private void setElevation(CenterCutState newState)
        {
            if (_state == newState)
                //there is nothing to do
                return;

            var elevationFactor = _state - newState;
            _state = newState;

            refreshStateButtons();

            var elevation = double.Parse(Elevation.Text);
            var elevationSteps = (int)Math.Round(elevation / Configuration.MilimetersPerStep) * elevationFactor;

            var builder = new PlanBuilder();
            builder.AddRampedLineUVXY(elevationSteps, 0, -elevationSteps, 0, Configuration.MaxPlaneAcceleration, Configuration.MaxPlaneSpeed);
            var plan = builder.Build();

            _panel.Cnc.SEND(plan);
        }

        private void refreshStateButtons()
        {
            SetPositive.IsEnabled = SetNegative.IsEnabled = SetZero.IsEnabled = true;
            switch (_state)
            {
                case CenterCutState.Negative:
                    SetNegative.IsEnabled = false;
                    break;
                case CenterCutState.Zero:
                    SetZero.IsEnabled = false;
                    break;
                case CenterCutState.Positive:
                    SetPositive.IsEnabled=false;
                    break;
            }
        }

        private void stopMovement(object sender, MouseButtonEventArgs e)
        {
            _panel.CoordController.SetMovement(0, 0);
        }

        private void Down_MouseDown(object sender, MouseButtonEventArgs e)
        {
            transition(0, 1);
        }

        private void Up_MouseDown(object sender, MouseButtonEventArgs e)
        {
            transition(0, -1);
        }

        private void transition(int c1, int c2)
        {
            _panel.CoordController.SetPlanes(true, true);
            _panel.CoordController.SetSpeed(Machine.Configuration.StartDeltaT);
            _panel.CoordController.SetMovement(c1, c2);
        }

        private void writeNumber(TextBox box, double number)
        {
            var start = box.SelectionStart;
            box.Text = string.Format("{0:0.000}", number);
            box.SelectionStart = start;
            box.SelectionLength = 0;
        }

        private void refreshWindow()
        {
            double.TryParse(Elevation.Text, out double elevation);
            writeNumber(Elevation, elevation);
        }
    }
}
