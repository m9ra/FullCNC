using GeometryCNC.GCode;
using GeometryCNC.Primitives;
using GeometryCNC.Render;
using GeometryCNC.Tools;
using GeometryCNC.Volumetric;
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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GeometryCNC_Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Random _rnd = new Random();

        private Action<int> _sliderCallback = null;

        public MainWindow()
        {
            InitializeComponent();
            enable2dOutput();
        }

        #region Demo methods

        private void distanceDemo(object sender, RoutedEventArgs e)
        {
            enable2dOutput();

            var demoShape = getRandomClosedShape();
            output2D(demoShape);

            for (var i = 0; i < 100; ++i)
            {
                var point = getRandomOutputPoint();
                var distance = demoShape.Distance(point);

                output2D(point, stroke: Brushes.Blue, diameter: distance * 2);
            }
        }

        private void pointInsideDemo(object sender, RoutedEventArgs e)
        {
            enable2dOutput();

            var demoShape = getRandomClosedShape();
            output2D(demoShape);

            for (var i = 0; i < 20000; ++i)
            {
                var point = getRandomOutputPoint();

                if (demoShape.Involve(point))
                {
                    output2D(point, Brushes.Red);
                }
                else
                {
                    output2D(point, Brushes.Green);
                }
            }
        }

        private void heightmapDemo(object sender, RoutedEventArgs e)
        {
            enable3dOutput();

            var toolDiameter = 5.0;
            var margin = toolDiameter;
            var radius = 50.0;

            var mapResolution = 0.2;
            var center = new Point(margin + radius, margin + radius);
            var modelShape = new ConeShape(center, radius);

            var mapStart = new Point(0, 0);
            var mapEnd = new Point(mapStart.X + 2 * margin + 2 * radius, mapStart.Y + 2 * margin + 2 * radius);
            var model = HeightMap.From(modelShape, mapStart, mapEnd, mapResolution);


            //simulate parallel machining
            var blockHeight = radius + margin;
            var stock = HeightMap.Flat(blockHeight, mapStart, mapEnd, mapResolution);
            var machiningStep = 1.0;
            var tool = new BallnoseEndMill(toolDiameter);

            simulateLinearMachining(stock, tool, model, toolDiameter, machiningStep, true);
            simulateLinearMachining(stock, tool, model, toolDiameter, machiningStep, false);

            output3D(stock);
        }

        private void gCodeDemo(object sender, RoutedEventArgs e)
        {
            enable3dOutput();

            var test = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames();
            var gcodes = Encoding.ASCII.GetString(ExampleData.hemisphere_gcodes);

            var parser = new Parser(gcodes);
            var path = parser.GetToolPath();

            // collect the path segments
            var targets = path.Targets.ToArray();
            var rapidSegments = new SegmentCollection3D();
            var workingSegments = new SegmentCollection3D();
            var lastTarget = path.Targets.First();
            foreach (var target in path.Targets.Skip(1))
            {
                var activeSegments = workingSegments;
                if (target.MotionMode == MotionMode.IsLinearRapid)
                    activeSegments = rapidSegments;

                activeSegments.AddSegment(lastTarget.End, target.End);
                lastTarget = target;
            }

            // draw gcode as 3D meshes
            var lineThickness = 0.05;
            var workingMesh = workingSegments.CreateMesh(lineThickness);
            var rapidMesh = rapidSegments.CreateMesh(lineThickness);
            output3D(createModel(workingMesh, frontColor: Brushes.Blue, useComplexMaterial: true));
            output3D(createModel(rapidMesh, frontColor: Brushes.DarkOrange, useComplexMaterial: true));

            var pointSize = 1.0;
            var activePoint = createCube(targets.First().End, pointSize);
            output3D(activePoint);

            enableSlider(targets.Length, (tick) =>
            {
                remove3D(activePoint);
                activePoint = createCube(targets[tick].End, pointSize);
                output3D(activePoint);
            });
        }

        #endregion

        #region Common output utilities

        private void enable2dOutput()
        {
            if (Output2D.Parent == null)
                OutputContainer.Children.Add(Output2D);

            if (Output3D.Parent != null)
                OutputContainer.Children.Remove(Output3D);

            clearOutputs();
        }

        private void enable3dOutput()
        {
            if (Output2D.Parent != null)
                OutputContainer.Children.Remove(Output2D);

            if (Output3D.Parent == null)
                OutputContainer.Children.Add(Output3D);

            clearOutputs();
        }

        private void clearOutputs()
        {
            Output2D.Children.Clear();

            var light = View3D.Children.First();
            View3D.Children.Clear();
            View3D.Children.Add(light);

            InvalidateMeasure();
            Slider.Visibility = Visibility.Collapsed;
            _sliderCallback = null;
        }

        private void sliderValueChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _sliderCallback?.Invoke((int)(Math.Round(e.NewValue)));
        }

        private void enableSlider(int tickCount, Action<int> callback)
        {
            Slider.Maximum = tickCount - 1;
            Slider.Value = 0;
            Slider.Visibility = Visibility.Visible;

            _sliderCallback = callback;
        }

        #endregion

        #region 2D output utilities

        private void output2D(Point p, Brush fill = null, Brush stroke = null, double diameter = 5)
        {
            var circle = new Ellipse()
            {
                Width = diameter,
                Height = diameter,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 1.0
            };
            Output2D.Children.Add(circle);

            circle.SetValue(Canvas.LeftProperty, p.X - diameter / 2);
            circle.SetValue(Canvas.TopProperty, p.Y - diameter / 2);
        }

        private void output2D(Shape2D shape)
        {
            var polyline = new Polyline() { Points = new PointCollection(shape.Points) };
            polyline.Stroke = Brushes.Black;
            polyline.StrokeThickness = 1.0;
            Output2D.Children.Add(polyline);

            foreach (var point in shape.Points)
            {
                output2D(point, Brushes.Black);
            }
        }

        #endregion

        #region 3D output utilities

        private void output3D(HeightMap map)
        {
            var model = createMapModel(map);
            output3D(model);
        }

        private void output3D(Model3D model)
        {
            var group = new Model3DGroup();
            group.Children.Add(model);

            var modelVisual = new ModelVisual3D();
            modelVisual.Content = group;

            View3D.Children.Add(modelVisual);
        }

        private void remove3D(Model3D model)
        {
            var child = View3D.Children.Where(c =>
            {
                var visual = c as ModelVisual3D;
                if (visual == null)
                    return false;

                var group = visual.Content as Model3DGroup;
                if (group == null)
                    return false;

                return group.Children.Contains(model);
            }).First();

            View3D.Children.Remove(child);
        }

        private Model3D createMapModel(HeightMap map)
        {
            var geometry = Voxels.CreateMeshFrom(map);
            return createModel(geometry);
        }

        private Model3D createCube(Point3D center, double size)
        {
            var line = new SegmentCollection3D();

            var v = new Vector3D(0, 0, 1);

            var p1 = center + (v * size / 2);
            var p2 = center - (v * size / 2);

            line.AddSegment(p1, p2);

            return createModel(line.CreateMesh(size));
        }

        private Model3D createModel(MeshGeometry3D geometry, Brush frontColor = null, Brush backColor = null, bool useComplexMaterial = false)
        {
            if (frontColor == null)
                frontColor = Brushes.Red;

            if (backColor == null)
                backColor = Brushes.Yellow;

            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(frontColor));
            if (useComplexMaterial)
            {
                materialGroup.Children.Add(new EmissiveMaterial(frontColor));
            }

            var backMaterialGroup = new MaterialGroup();
            backMaterialGroup.Children.Add(new DiffuseMaterial(backColor));
            if (useComplexMaterial)
            {
                backMaterialGroup.Children.Add(new EmissiveMaterial(backColor));
            }


            var model = new GeometryModel3D(geometry, materialGroup);
            model.BackMaterial = backMaterialGroup;


            materialGroup.Freeze();
            backMaterialGroup.Freeze();
            model.Freeze();
            return model;
        }

        #endregion

        #region Data generation methods

        private Point getRandomOutputPoint()
        {
            var x = _rnd.NextDouble() * OutputContainer.ActualWidth;
            var y = _rnd.NextDouble() * OutputContainer.ActualHeight;

            return new Point(x, y);
        }

        private Shape2D getRandomClosedShape()
        {
            var polyPoints = new List<Point>();
            for (var i = -3; i < _rnd.Next(20); ++i)
            {
                polyPoints.Add(getRandomOutputPoint());
            }

            var demoShape = new Shape2D(polyPoints.ToArray());
            demoShape = demoShape.AsClosed();
            return demoShape;
        }

        private void simulateLinearMachining(HeightMap stock, BallnoseEndMill tool, HeightMap model, double toolDiameter, double machiningStep, bool useDirX)
        {
            var machiningMargin = toolDiameter;
            var toolX = -machiningMargin;
            var toolY = -machiningMargin;
            var mapEnd = stock.End;
            var mapStepSize = stock.StepSize;
            var toolConvolution = tool.AsConvolution(mapStepSize);

            var resolutionStep = mapStepSize * 0.999;

            if (useDirX)
            {
                while (toolX < mapEnd.X + machiningMargin)
                {
                    toolY = -machiningMargin;

                    while (toolY < mapEnd.Y + machiningMargin)
                    {
                        var machinedPoint = new Point(toolX, toolY);
                        var convolutedHeight = model.GetMaxAddConvolution(machinedPoint, toolConvolution);
                        var offset = convolutedHeight; //(blockHeight - convolutedHeight);

                        if (!double.IsInfinity(offset))
                            stock.ApplyMinSubConvolution(machinedPoint, offset, toolConvolution);

                        toolY += resolutionStep;
                    }
                    toolX += machiningStep;
                }
            }
            else
            {
                while (toolY < mapEnd.Y + machiningMargin)
                {
                    toolX = -machiningMargin;

                    while (toolX < mapEnd.X + machiningMargin)
                    {
                        var machinedPoint = new Point(toolX, toolY);
                        var convolutedHeight = model.GetMaxAddConvolution(machinedPoint, toolConvolution);
                        var offset = convolutedHeight; //(blockHeight - convolutedHeight);

                        if (!double.IsInfinity(offset))
                            stock.ApplyMinSubConvolution(machinedPoint, offset, toolConvolution);

                        toolX += resolutionStep;
                    }
                    toolY += machiningStep;
                }
            }
        }
        #endregion
    }
}
