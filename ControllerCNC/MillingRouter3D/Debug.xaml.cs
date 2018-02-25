using ControllerCNC.Loading;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;
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

namespace MillingRouter3D
{
    /// <summary>
    /// Interaction logic for Debug.xaml
    /// </summary>
    public partial class Debug : Window
    {
        private readonly DebugCanvas _canvas;

        public Debug()
        {
            InitializeComponent();
            var canvas = new DebugCanvas(SnapshotSelector);
            var transformation = new TransformGroup();
            var scale = 2;
            transformation.Children.Add(new TranslateTransform(0, 0));
            transformation.Children.Add(new ScaleTransform(scale, scale));
            canvas.RenderTransform = transformation;
            canvas.Width = 1000 * scale;
            canvas.Height = 1000 * scale;
            _canvas = canvas;
            Viewer.Content = canvas;
        }

        private void SnapshotSelector_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _canvas.InvalidateVisual();
        }
    }

    class DebugCanvas : Canvas
    {
        private DrawEvent[][] _drawEvents;

        private readonly Slider _slider;

        internal DebugCanvas(Slider slider)
        {
            _slider = slider;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_drawEvents == null)
            {
                var loader = new ShapeFactory3D();
                var points = loader.Load("./test_offset3.png", out _).First().Reverse();
                /*/
                points = new Point2Dmm[]
                {
                      new Point2Dmm(0,0),
                      new Point2Dmm(100, 0),
                      new Point2Dmm(100, 100),
                      new Point2Dmm(60, 100),
                      new Point2Dmm(80, 110),
                      new Point2Dmm(30, 110),
                      new Point2Dmm(20, 100),
                      new Point2Dmm(0, 100),

                      new Point2Dmm(0,0),
                };/**/
                /*/
                points = new Point2Dmm[]
                {
                    new Point2Dmm(70,60),
                    new Point2Dmm(40,20),
                    new Point2Dmm(120,20),
                    new Point2Dmm(100,60),
                    new Point2Dmm(105,25),
                    new Point2Dmm(60,25),
                    new Point2Dmm(140,140),
                    new Point2Dmm(70,140),

                    new Point2Dmm(70,60),
                };/**/
                var offset = new OffsetCalculator(points, true);

                offset.WithOffset(25);
                _drawEvents = offset.GetUpdateSnapshots().ToArray();
                _slider.Maximum = _drawEvents.Length - 1;
                _slider.Value = _slider.Maximum;
            }

            var snapshotToDisplay = _drawEvents[(int)_slider.Value];

            foreach (var evt in snapshotToDisplay)
            {
                evt.Draw(dc);
            }
        }
    }
}
