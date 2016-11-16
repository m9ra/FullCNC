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

namespace ControllerCNC.GUI
{
    /// <summary>
    /// Interaction logic for PointProviderPropertiesDialog.xaml
    /// </summary>
    public partial class PointProviderPropertiesDialog : Window
    {
        /// <summary>
        /// Item which properties are handled.
        /// </summary>
        private readonly PointProviderItem _item;

        internal PointProviderPropertiesDialog(PointProviderItem item)
        {
            _item = item;

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
            ShapeRotation.ValueChanged += ShapeRotation_ValueChanged;

            ShowDialog();
        }

        private void refreshWindow()
        {
            Title = _item.Name;
            writeNumber(ShapeTop, _item.PositionY * Constants.MilimetersPerStep);
            writeNumber(ShapeLeft, _item.PositionX * Constants.MilimetersPerStep);

            var shapeItem = _item as ShapeItem;
            if (shapeItem == null)
            {
                ShapeProperties.Visibility = Visibility.Hidden;
                return;
            }

            writeNumber(ShapeWidth, shapeItem.MetricWidth);
            writeNumber(ShapeHeight, shapeItem.MetricHeight);
            ShapeRotation.Value = shapeItem.RotationAngle;
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
                _item.PositionY = (int)Math.Round(value / Constants.MilimetersPerStep);

            refreshWindow();
        }

        private void ShapeLeft_TextChanged(object sender, TextChangedEventArgs e)
        {
            double value;
            if (double.TryParse(ShapeLeft.Text, out value))
                _item.PositionX = (int)Math.Round(value / Constants.MilimetersPerStep);

            refreshWindow();
        }

        private void ShapeWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            var shapeItem = _item as ShapeItem;

            double value;
            if (double.TryParse(ShapeWidth.Text, out value))
                shapeItem.MetricWidth = value;

            refreshWindow();
        }

        private void ShapeHeight_TextChanged(object sender, TextChangedEventArgs e)
        {
            var shapeItem = _item as ShapeItem;

            double value;
            if (double.TryParse(ShapeHeight.Text, out value))
                shapeItem.MetricHeight = value;

            refreshWindow();
        }

        void ShapeRotation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var shapeItem = _item as ShapeItem;
            shapeItem.RotationAngle = ShapeRotation.Value;
        }

        #endregion
    }
}
