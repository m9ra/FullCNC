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

using ControllerCNC.Machine;

namespace MillingRouter3D.GUI
{
    /// <summary>
    /// Interaction logic for PointProviderPropertiesDialog.xaml
    /// </summary>
    public partial class MillingItemPropertiesDialog : Window
    {
        /// <summary>
        /// Item which properties are handled.
        /// </summary>
        private readonly MillingWorkspaceItem _item;

        /// <summary>
        /// Workspace where item is located.
        /// </summary>
        private readonly MillingWorkspacePanel _workspace;

        internal MillingItemPropertiesDialog(MillingWorkspaceItem item, MillingWorkspacePanel workspace)
        {
            _item = item;
            _workspace = workspace;

            InitializeComponent();
            KeyUp += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    this.Close();
            };

            refreshWindow();

            ShapeTop.TextChanged += ShapeTop_TextChanged;
            ShapeLeft.TextChanged += ShapeLeft_TextChanged;
            ShapeWidth.TextChanged += ShapeWidth_TextChanged;
            ShapeHeight.TextChanged += ShapeHeight_TextChanged;
            MillingDepth.TextChanged += MillingDepth_TextChanged;
            AngleNum.TextChanged += AngleNum_TextChanged;
            ShapeRotation.ValueChanged += ShapeRotation_ValueChanged;
            ShowDialog();
        }
        
        private void refreshWindow()
        {
            Title = _item.Name.ToString();
            writeNumber(ShapeTop, _workspace.RangeY - _item.PositionY);
            writeNumber(ShapeLeft, _item.PositionX);

            var millingItem = _item as MillingShapeItem2D;
            if (millingItem == null)
            {
                ShapeProperties.Visibility = Visibility.Collapsed;
                setLayout(300, 450, 100);
                return;
            }


            writeNumber(ShapeWidth, millingItem.MetricWidth);
            writeNumber(ShapeHeight, millingItem.MetricHeight);
            writeNumber(MillingDepth, millingItem.MillingDepth);
            writeNumber(AngleNum, millingItem.RotationAngle);
            ClockwiseCut.IsChecked = millingItem.UseClockwiseCut;

            ShapeRotation.Value = millingItem.RotationAngle;
        }

        private void setLayout(double width, double layoutWidth, double height)
        {
            this.Height = height;
            this.Width = width;

            this.LayoutGrid.Height = height;
            this.LayoutGrid.Width = layoutWidth;
        }

        private void writeNumber(TextBox box, double number)
        {
            var start = box.SelectionStart;
            box.Text = string.Format("{0:0.000}", number);
            box.SelectionStart = start;
            box.SelectionLength = 0;
        }

        #region Value change handlers

        private void ShapeTop_TextChanged(object sender, TextChangedEventArgs e)
        {
            double value;
            if (double.TryParse(ShapeTop.Text, out value))
                _item.PositionY = _workspace.RangeY - value;

            refreshWindow();
        }

        private void ShapeLeft_TextChanged(object sender, TextChangedEventArgs e)
        {
            double value;
            if (double.TryParse(ShapeLeft.Text, out value))
                _item.PositionX = value;

            refreshWindow();
        }

        private void AngleNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            double value;
            if (double.TryParse(AngleNum.Text, out value))
                _item.RotationAngle= value;

            refreshWindow();
        }

        private void ShapeWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            var millingItem = _item as MillingShapeItem2D;

            double value;
            if (double.TryParse(ShapeWidth.Text, out value))
                millingItem.MetricWidth = value;

            refreshWindow();
        }

        private void ShapeHeight_TextChanged(object sender, TextChangedEventArgs e)
        {
            var millingItem = _item as MillingShapeItem2D;

            double value;
            if (double.TryParse(ShapeHeight.Text, out value))
                millingItem.MetricHeight = value;

            refreshWindow();
        }

        private void MillingDepth_TextChanged(object sender, TextChangedEventArgs e)
        {
            var millingItem = _item as MillingShapeItem2D;

            double value;
            if (double.TryParse(MillingDepth.Text, out value))
                millingItem.MillingDepth = value;

            refreshWindow();
        }

        private void ShapeRotation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var millingItem = _item as MillingShapeItem2D;
            millingItem.RotationAngle = ShapeRotation.Value;

            refreshWindow();
        }

        private void ClockwiseCut_Changed(object sender, RoutedEventArgs e)
        {
            var millingItem = _item as MillingShapeItem2D;
            var value = ClockwiseCut.IsChecked.Value;

            millingItem.UseClockwiseCut = value;
            refreshWindow();
        }

        #endregion
    }
}
