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

        /// <summary>
        /// whether changes on StickSpeed will be reflected
        /// </summary>
        private bool _blockChanges_StickSpeed = false;

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
            BlockThickness.TextChanged += BlockThickness_TextChanged;
            ShapeRotation.ValueChanged += ShapeRotation_ValueChanged;
            KerfUV.TextChanged += KerfUV_TextChanged;
            KerfXY.TextChanged += KerfXY_TextChanged;

            ShowDialog();
        }

        private void refreshWindow()
        {
            Title = _item.Name.ToString();
            writeNumber(ShapeTop, _item.PositionC2 * Constants.MilimetersPerStep);
            writeNumber(ShapeLeft, _item.PositionC1 * Constants.MilimetersPerStep);

            var shapeItem = _item as ShapeItem;
            if (shapeItem == null)
            {
                ShapeProperties.Visibility = Visibility.Collapsed;
                setLayout(300, 450, 100);
                return;
            }

            var shapeItem4D = shapeItem as ShapeItem4D;
            if (shapeItem4D == null)
            {
                BlockProperties.Visibility = Visibility.Collapsed;
                setLayout(300, 345, 250);
            }

            writeNumber(ShapeWidth, shapeItem.MetricWidth);
            writeNumber(ShapeHeight, shapeItem.MetricHeight);
            ClockwiseCut.IsChecked = shapeItem.UseClockwiseCut;

            if (shapeItem4D != null)
            {
                writeNumber(BlockThickness, shapeItem4D.MetricThickness);
                UvXySwitched.IsChecked = shapeItem4D.IsUvXySwitched;
                UseExplicitKerf.IsChecked = shapeItem4D.UseExplicitKerf;

                var kerfEnabled = shapeItem4D.UseExplicitKerf;
                KerfUV.IsEnabled = kerfEnabled;
                KerfXY.IsEnabled = kerfEnabled;
                StickSpeedUV.IsEnabled = kerfEnabled;
                StickSpeedXY.IsEnabled = kerfEnabled;

                writeNumber(KerfUV, shapeItem4D.KerfUV);
                writeNumber(KerfXY, shapeItem4D.KerfXY);

                switch (shapeItem4D.SpeedAlgorithm)
                {
                    case SpeedAlgorithm.TowerBased:
                        StickSpeedUV.IsChecked = false;
                        StickSpeedXY.IsChecked = false;
                        break;

                    case SpeedAlgorithm.StickToFacetUV:
                        StickSpeedUV.IsChecked = true && kerfEnabled;
                        StickSpeedXY.IsChecked = false;
                        break;

                    case SpeedAlgorithm.StickToFacetXY:
                        StickSpeedUV.IsChecked = false;
                        StickSpeedXY.IsChecked = true && kerfEnabled;
                        break;
                }
            }
            ShapeRotation.Value = shapeItem.RotationAngle;
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
                _item.PositionC2 = (int)Math.Round(value / Constants.MilimetersPerStep);

            refreshWindow();
        }

        private void ShapeLeft_TextChanged(object sender, TextChangedEventArgs e)
        {
            double value;
            if (double.TryParse(ShapeLeft.Text, out value))
                _item.PositionC1 = (int)Math.Round(value / Constants.MilimetersPerStep);

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

        private void BlockThickness_TextChanged(object sender, TextChangedEventArgs e)
        {
            var shapeItem = _item as ShapeItem4D;

            double value;
            if (double.TryParse(BlockThickness.Text, out value))
                shapeItem.MetricThickness = value;

            refreshWindow();
        }

        private void KerfUV_TextChanged(object sender, TextChangedEventArgs e)
        {
            var shapeItem = _item as ShapeItem4D;

            double value;
            if (double.TryParse(KerfUV.Text, out value))
                shapeItem.KerfUV = value;

            refreshWindow();
        }

        private void KerfXY_TextChanged(object sender, TextChangedEventArgs e)
        {
            var shapeItem = _item as ShapeItem4D;

            double value;
            if (double.TryParse(KerfXY.Text, out value))
                shapeItem.KerfXY = value;

            refreshWindow();
        }

        private void ShapeRotation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var shapeItem = _item as ShapeItem;
            shapeItem.RotationAngle = ShapeRotation.Value;
        }

        private void UseExplicitKerf_Changed(object sender, RoutedEventArgs e)
        {
            var shapeItem = _item as ShapeItem4D;
            shapeItem.UseExplicitKerf = UseExplicitKerf.IsChecked.Value;
            refreshWindow();
        }

        private void UvXySwitched_Changed(object sender, RoutedEventArgs e)
        {
            var shapeItem = _item as ShapeItem4D;
            var value = UvXySwitched.IsChecked.Value;

            if (value != shapeItem.IsUvXySwitched)
            {
                // switch kerf for axes
                var uvTmp = shapeItem.KerfUV;
                shapeItem.KerfUV = shapeItem.KerfXY;
                shapeItem.KerfXY = uvTmp;
            }

            shapeItem.IsUvXySwitched = value;
            refreshWindow();
        }

        private void ClockwiseCut_Changed(object sender, RoutedEventArgs e)
        {
            var shapeItem = _item as ShapeItem;
            var value = ClockwiseCut.IsChecked.Value;

            shapeItem.UseClockwiseCut = value;
            refreshWindow();
        }

        private void StickSpeedUV_Changed(object sender, RoutedEventArgs e)
        {
            var shapeItem = _item as ShapeItem4D;
            if (_blockChanges_StickSpeed || !shapeItem.UseExplicitKerf)
                return;

            if (StickSpeedUV.IsChecked.Value)
                shapeItem.SpeedAlgorithm = SpeedAlgorithm.StickToFacetUV;
            else
                shapeItem.SpeedAlgorithm = SpeedAlgorithm.TowerBased;

            _blockChanges_StickSpeed = true;
            refreshWindow();
            _blockChanges_StickSpeed = false;
        }

        private void StickSpeedXY_Changed(object sender, RoutedEventArgs e)
        {
            var shapeItem = _item as ShapeItem4D;
            if (_blockChanges_StickSpeed || !shapeItem.UseExplicitKerf)
                return;

            if (StickSpeedXY.IsChecked.Value)
                shapeItem.SpeedAlgorithm = SpeedAlgorithm.StickToFacetXY;
            else
                shapeItem.SpeedAlgorithm = SpeedAlgorithm.TowerBased;

            _blockChanges_StickSpeed = true;
            refreshWindow();
            _blockChanges_StickSpeed = false;
        }

        #endregion
    }
}
