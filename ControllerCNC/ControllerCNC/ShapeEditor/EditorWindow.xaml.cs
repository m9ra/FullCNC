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

using System.IO;
using System.Windows.Media.Media3D;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using ControllerCNC.Demos;
using ControllerCNC.Loading;
using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.ShapeEditor
{
    /// <summary>
    /// Interaction logic for Editor.xaml
    /// </summary>
    public partial class EditorWindow : Window
    {
        private readonly ShapeFactory _factory;

        private ModelVisual3D _3dVisualModel = null;

        private double _shapeThickness = 0;

        public EditorWindow()
        {
            InitializeComponent();
            _factory = new ShapeFactory();

            refreshEditorInputs();
        }

        private void drawShape(IEnumerable<Point4Dmm> points, double metricThickness)
        {
            if (_3dVisualModel != null)
                View3D.Children.Remove(_3dVisualModel);

            points = normalized(points, out var coordFactor);
            var facet1Points = points.ToUV();
            var facet2Points = points.ToXY();

            var thickness = metricThickness;
            var facet1Z = -thickness / 2 * coordFactor;
            var facet2Z = thickness / 2 * coordFactor;

            var group = new Model3DGroup();
            group.Children.Add(getFacetModel(facet1Points, facet1Z, true));
            group.Children.Add(getShapeCoatingModel(facet1Points, facet1Z, facet2Points, facet2Z));
            group.Children.Add(getFacetModel(facet2Points, facet2Z, false));

            _3dVisualModel = new ModelVisual3D();
            Trackball.Model = _3dVisualModel;
            _3dVisualModel.Content = group;
            View3D.Children.Add(_3dVisualModel);
        }

        private IEnumerable<Point4Dmm> normalized(IEnumerable<Point4Dmm> points, out double coordFactor)
        {
            var coords = points.Select(p => p.U);
            var maxCoordU = coords.Max();
            var minCoordU = coords.Min();
            var rangeU = maxCoordU - minCoordU;
            var offsU = minCoordU + rangeU / 2;

            coords = points.Select(p => p.V);
            var maxCoordV = coords.Max();
            var minCoordV = coords.Min();
            var rangeV = maxCoordV - minCoordV;
            var offsV = minCoordV + rangeV / 2;

            coords = points.Select(p => p.X);
            var maxCoordX = coords.Max();
            var minCoordX = coords.Min();
            var rangeX = maxCoordX - minCoordX;
            var offsX = minCoordX + rangeX / 2;

            coords = points.Select(p => p.Y);
            var maxCoordY = coords.Max();
            var minCoordY = coords.Min();
            var rangeY = maxCoordY - minCoordY;
            var offsY = minCoordY + rangeY / 2;

            var range = Math.Max(Math.Max(rangeU, rangeV), Math.Max(rangeX, rangeY));
            coordFactor = 1.0 / range;

            var normalizedCoords = points.Select(
                p =>
              new Point4Dmm((p.U - offsU) / range, (p.V - offsV) / range, (p.X - offsX) / range, (p.Y - offsY) / range)
            ).ToArray();

            return normalizedCoords;
        }

        private static GeometryModel3D getShapeCoatingModel(IEnumerable<Point2Dmm> facet1Points, double facet1Z, IEnumerable<Point2Dmm> facet2Points, double facet2Z)
        {
            var points = new Point3DCollection();
            var triangles = new Int32Collection();
            var points1 = facet1Points.ToArray();
            var points2 = facet2Points.ToArray();
            for (var i = 0; i < points1.Length; ++i)
            {
                var f1p = points1[i];
                var f2p = points2[i];

                /* points.Add(new Point3D((f1p.C1 - coordOffset) * coordFactor, (f1p.C2 - coordOffset) * coordFactor, facet1Z));
                 points.Add(new Point3D((f2p.C1 - coordOffset) * coordFactor, (f2p.C2 - coordOffset) * coordFactor, facet2Z));


                 if (points.Count < 4)
                     continue;

                 var i_p0 = points.Count - 4;
                 var i_p1 = i_p0 + 1;
                 var i_p2 = i_p0 + 2;
                 var i_p3 = i_p0 + 3;

                 var p0 = points[i_p0];
                 var p1 = points[i_p1];
                 var p2 = points[i_p2];
                 var p3 = points[i_p3];

             
                 var bodyTriangle1 = getTriangleIndexes(true,p0, i_p0, p1, i_p1, p2, i_p2);
                 var bodyTriangle2 = getTriangleIndexes(true, p1, i_p1, p2, i_p2, p3, i_p3);*/

                points.Add(new Point3D(f1p.C1, f1p.C2, facet1Z));
                points.Add(new Point3D(f1p.C1, f1p.C2, facet1Z));
                points.Add(new Point3D(f2p.C1, f2p.C2, facet2Z));
                points.Add(new Point3D(f2p.C1, f2p.C2, facet2Z));

                if (points.Count < 4)
                    continue;

                var i_p0 = points.Count - 4;
                var i_p1 = i_p0 + 2;
                var i_p2 = i_p0 + 4;
                var i_p3 = i_p0 + 6;

                var bodyTriangle1 = bothSidedTriangleIndexes(i_p0, i_p1, i_p2);
                var bodyTriangle2 = bothSidedTriangleIndexes(i_p1, i_p2, i_p3);
                foreach (var index in bodyTriangle1.Concat(bodyTriangle2))
                    triangles.Add(index);
            }

            var geometry = new MeshGeometry3D();
            geometry.Positions = points;
            geometry.TriangleIndices = triangles;

            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(Brushes.Green));
            materialGroup.Children.Add(new SpecularMaterial(Brushes.Green, 50));
            materialGroup.Children.Add(new EmissiveMaterial(Brushes.DarkGreen));

            var model = new GeometryModel3D(geometry, materialGroup);
            model.BackMaterial = materialGroup;
            var normals = geometry.Normals.ToArray();
            return model;
        }

        private static IEnumerable<int> bothSidedTriangleIndexes(int p0, int p1, int p2)
        {
            return new int[] { p0, p1, p2, p2 + 1, p1 + 1, p0 + 1 };
        }

        private static GeometryModel3D getFacetModel(IEnumerable<Point2Dmm> facetPoints, double z, bool isFrontFace)
        {
            var filteredPoints = new List<Point2Dmm>();
            foreach (var point in facetPoints)
            {
                if (filteredPoints.Count == 0)
                    filteredPoints.Add(point);

                if (filteredPoints.Last().C1 == point.C1 && filteredPoints.Last().C2 == point.C2)
                    //repetitive points
                    continue;

                filteredPoints.Add(point);
            }

            var facetTriangles = Triangulation2D.Triangulate(filteredPoints).ToArray();

            var points = new Point3DCollection();
            var triangles = new Int32Collection();
            foreach (var triangle in facetTriangles)
            {
                foreach (var point in triangle)
                {
                    points.Add(new Point3D(point.C1, point.C2, z));
                }

                var i = points.Count - 3;
                var triangleIndexes = getTriangleIndexes(isFrontFace, triangle[0], i, triangle[1], i + 1, triangle[2], i + 2);
                foreach (var index in triangleIndexes)
                    triangles.Add(index);
            }

            var geometry = new MeshGeometry3D();
            geometry.Positions = points;
            geometry.TriangleIndices = triangles;

            var brush1 = isFrontFace ? Brushes.Red : Brushes.Blue;
            var brush2 = isFrontFace ? Brushes.DarkRed : Brushes.DarkBlue;
            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(brush1));
            materialGroup.Children.Add(new SpecularMaterial(brush1, 50));
            materialGroup.Children.Add(new EmissiveMaterial(brush2));

            var model = new GeometryModel3D(geometry, materialGroup);
            return model;
        }

        private static int[] getTriangleIndexes(bool needClockwise, Point3D p0, int i0, Point3D p1, int i1, Point3D p2, int i2)
        {
            var edge1 = p1 - p0;
            var edge2 = p2 - p0;
            var norm = Vector3D.CrossProduct(edge1, edge2);

            var v0 = new Vector3D(p0.X, p0.Y, p0.Z);
            var v1 = new Vector3D(p1.X, p1.Y, p1.Z);
            var v2 = new Vector3D(p2.X, p2.Y, p2.Z);

            var centroid = (v0 + v1 + v2) / 3;
            var w = Vector3D.DotProduct(norm, centroid);
            var isClockwise = w >= 0;

            if (isClockwise == needClockwise)
                return new[] { i0, i1, i2 };
            else
                return new[] { i2, i1, i0 };
        }

        private static int[] getTriangleIndexes(bool needClockwise, Point2Dmm p0, int i0, Point2Dmm p1, int i1, Point2Dmm p2, int i2)
        {
            var isClockwise = diffProduct(p0, p1) + diffProduct(p1, p2) + diffProduct(p2, p0) >= 0;
            var isClockwise2 = diffProduct(p2, p1) + diffProduct(p1, p0) + diffProduct(p0, p2) >= 0;
            if (isClockwise == isClockwise2)
                //throw new NotSupportedException();
                return new int[0];

            if (!isClockwise)
                needClockwise = !needClockwise;

            var triangleIndexes = needClockwise ? new[] { i0, i1, i2 } : new[] { i2, i1, i0 };
            return triangleIndexes;
        }

        private static double diffProduct(Point2Dmm p1, Point2Dmm p2)
        {
            return (p2.C1 - p1.C1) * (p2.C2 + p1.C2);
        }

        private IEnumerable<Point4Dmm> getAlignedPoints(FacetShape facet1, FacetShape facet2)
        {
            var points = new List<Primitives.Point4Dmm>();
            var done = -1.0;

            while (done < 1.0)
            {
                var nextPointPercentage1 = facet1.GetNextPointPercentage(done);
                var nextPointPercentage2 = facet2.GetNextPointPercentage(done);
                var nextPointPercentage = Math.Min(nextPointPercentage1, nextPointPercentage2);

                var oldDone = done;
                done = nextPointPercentage;

                var percentageDelta = done - oldDone;
                var perimeterDelta = percentageDelta * (facet1.TotalPerimeter + facet2.TotalPerimeter);
                if (perimeterDelta < 0.00001)
                {
                    //smooth out floating point errors
                    continue;
                }


                var point1 = facet1.GetPoint(done);
                var point2 = facet2.GetPoint(done);
                var point = new Point4Dmm(point1.C1, point1.C2, point2.C1, point2.C2);
                points.Add(point);
            }
            return points;
        }

        #region GUI handlers

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var alignedPoints = getAlignedPoints(Facet1Pane.CreateFacetShape(), Facet2Pane.CreateFacetShape());
            var shape = new ShapeDefinition4D(alignedPoints, _shapeThickness);

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "4D Shape|*.4dcor";
            if (dlg.ShowDialog() == true)
            {
                var formatter = new BinaryFormatter();
                using (var stream = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    formatter.Serialize(stream, shape);
            }
        }


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadFacet1_Click(object sender, RoutedEventArgs e)
        {
            setupFacetFromFile(Facet1Pane);
        }

        private void LoadFacet2_Click(object sender, RoutedEventArgs e)
        {
            setupFacetFromFile(Facet2Pane);
        }

        private void Binding_Click(object sender, RoutedEventArgs e)
        {
            var alignedPoints = getAlignedPoints(Facet1Pane.CreateFacetShape(), Facet2Pane.CreateFacetShape());
            drawShape(alignedPoints, _shapeThickness);
        }

        private void setupFacetFromFile(FacetPanel facetPane)
        {
            var points = load2DPointsFromFile();

            facetPane.ClearParts();
            facetPane.AddPart(new EditorShapePart(points));

            refreshEditorInputs();
        }

        private IEnumerable<Point2Dmm> load2DPointsFromFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "All supported files|*.jpeg;*.jpg;*.png;*.bmp;*.dat;*.cor|Image files|*.jpeg;*.jpg;*.png;*.bmp|Coordinate files|*.dat;*.cor";

            if (dlg.ShowDialog().Value)
            {
                var filename = dlg.FileName;
                var shape = _factory.Load(filename);

                if (shape == null)
                    return null;

                return shape.ShapeDefinition.ToUV();
            }

            return null;
        }

        private void refreshEditorInputs()
        {
            if (Facet1Pane != null)
            {
                Facet1Width.Text = Facet1Pane.MetricWidth.ToString("0.000");
                Facet1Height.Text = Facet1Pane.MetricHeight.ToString("0.000");
                Facet1Left.Text = Facet1Pane.MetricShiftX.ToString("0.000");
                Facet1Top.Text = Facet1Pane.MetricShiftY.ToString("0.000");
            }

            if (Facet2Pane != null)
            {
                Facet2Width.Text = Facet2Pane.MetricWidth.ToString("0.000");
                Facet2Height.Text = Facet2Pane.MetricHeight.ToString("0.000");
            }

            if (Facet1Pane != null && Facet2Pane != null)
                Facet1Pane.FitSize(Facet2Pane);

            if (ShapeThickness != null)
                ShapeThickness.Text = _shapeThickness.ToString();
        }
        
        #endregion

        private void Facet1Top_TextChanged(object sender, TextChangedEventArgs e)
        {
            double.TryParse(Facet1Top.Text, out double result);
            if (Facet1Pane != null)
                Facet1Pane.MetricShiftY = result;

            refreshEditorInputs();
        }

        private void Facet1Left_TextChanged(object sender, TextChangedEventArgs e)
        {
            double.TryParse(Facet1Left.Text, out double result);
            if (Facet1Pane != null)
                Facet1Pane.MetricShiftX = result;

            refreshEditorInputs();
        }

        private void Facet1Width_TextChanged(object sender, TextChangedEventArgs e)
        {
            double.TryParse(Facet1Width.Text, out double result);
            if (Facet1Pane != null)
                Facet1Pane.MetricWidth = result;

            refreshEditorInputs();
        }

        private void Facet1Height_TextChanged(object sender, TextChangedEventArgs e)
        {
            double.TryParse(Facet1Height.Text, out double result);
            if (Facet1Pane != null)
                Facet1Pane.MetricHeight = result;

            refreshEditorInputs();
        }

        private void Facet2Width_TextChanged(object sender, TextChangedEventArgs e)
        {
            double.TryParse(Facet2Width.Text, out double result);
            if (Facet2Pane != null)
                Facet2Pane.MetricWidth = result;

            refreshEditorInputs();
        }

        private void Facet2Height_TextChanged(object sender, TextChangedEventArgs e)
        {
            double.TryParse(Facet2Height.Text, out double result);
            if (Facet2Pane != null)
                Facet2Pane.MetricHeight = result;

            refreshEditorInputs();
        }

        private void ShapeThickness_TextChanged(object sender, TextChangedEventArgs e)
        {
            double.TryParse(ShapeThickness.Text, out _shapeThickness);
            refreshEditorInputs();
        }
    }
}
