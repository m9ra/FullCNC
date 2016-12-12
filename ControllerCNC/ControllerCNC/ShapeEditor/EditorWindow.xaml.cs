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
using ControllerCNC.Machine;
using ControllerCNC.Primitives;

namespace ControllerCNC.ShapeEditor
{
    /// <summary>
    /// Interaction logic for Editor.xaml
    /// </summary>
    public partial class EditorWindow : Window
    {
        private readonly double _shapeThickness = 10;

        public EditorWindow()
        {
            InitializeComponent();
            var points = ShapeDrawing.CircleToSquare().ToArray();
         /*   var snowflake = ShapeDrawing.InterpolateImage("snowflake.png");
            snowflake = snowflake.Reverse().Select(p => new Point2Dmm(p.C1 / 40, p.C2 / 40));*/

            var facet1 = new FacetShape(points.ToUV());
            var facet2 = new FacetShape(points.ToXY());

            var points1 = centered(facet1.DefinitionPoints).ToArray();
            var points2 = centered(facet2.DefinitionPoints).ToArray();
            Facet1Pane.AddPart(new EditorShapePart(points1));
            Facet2Pane.AddPart(new EditorShapePart(points2));

            var alignedPoints = getAlignedPoints(Facet1Pane.CreateFacetShape(), Facet2Pane.CreateFacetShape());
            //alignedPoints = ShapeDrawing.CircleToSquare().As4Dstep();
            drawShape(alignedPoints.As4Dstep(), _shapeThickness);
        }

        private IEnumerable<Point2Dmm> centered(IEnumerable<Point2Dmm> points)
        {
            var maxC1 = points.Select(p => p.C1).Max();
            var maxC2 = points.Select(p => p.C2).Max();

            var minC1 = points.Select(p => p.C1).Min();
            var minC2 = points.Select(p => p.C2).Min();

            var diffC1 = maxC1 - minC1;
            var diffC2 = maxC2 - minC2;

            var c1Offset = -minC1 - diffC1 / 2;
            var c2Offset = -minC2 - diffC2 / 2;

            return points.Select(p => new Point2Dmm(p.C1 + c1Offset, p.C2 + c2Offset));
        }

        private void drawShape(IEnumerable<Primitives.Point4Dstep> points, double metricThickness)
        {
            var facet1Points = points.ToUV();
            var facet2Points = points.ToXY();
            var coordinates = facet1Points.Concat(facet2Points).SelectMany(p => new[] { p.C1, p.C2 }).ToArray();
            var maxCoord = coordinates.Max();
            var minCoord = coordinates.Min();
            var coordOffset = minCoord + (maxCoord - minCoord) / 2;
            var coordFactor = 1.0 / (maxCoord - coordOffset);

            var thickness = metricThickness / Constants.MilimetersPerStep;
            var facet1Z = -thickness / 2 * coordFactor;
            var facet2Z = thickness / 2 * coordFactor;

            var group = new Model3DGroup();
            group.Children.Add(getFacetModel(facet1Points, facet1Z, coordOffset, coordFactor, true));
            group.Children.Add(getShapeCoatingModel(facet1Points, facet1Z, facet2Points, facet2Z, coordOffset, coordFactor));
            group.Children.Add(getFacetModel(facet2Points, facet2Z, coordOffset, coordFactor, false));

            var visualModel = new ModelVisual3D();
            Trackball.Model = visualModel;
            visualModel.Content = group;
            View3D.Children.Add(visualModel);
        }

        private static GeometryModel3D getShapeCoatingModel(IEnumerable<Point2Dstep> facet1Points, double facet1Z, IEnumerable<Point2Dstep> facet2Points, double facet2Z, double coordOffset, double coordFactor)
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

                points.Add(new Point3D((f1p.C1 - coordOffset) * coordFactor, (f1p.C2 - coordOffset) * coordFactor, facet1Z));
                points.Add(new Point3D((f1p.C1 - coordOffset) * coordFactor, (f1p.C2 - coordOffset) * coordFactor, facet1Z));
                points.Add(new Point3D((f2p.C1 - coordOffset) * coordFactor, (f2p.C2 - coordOffset) * coordFactor, facet2Z));
                points.Add(new Point3D((f2p.C1 - coordOffset) * coordFactor, (f2p.C2 - coordOffset) * coordFactor, facet2Z));

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

        private static GeometryModel3D getFacetModel(IEnumerable<Point2Dstep> facetPoints, double z, double coordOffset, double coordFactor, bool isFrontFace)
        {
            var filteredPoints = new List<Point2Dstep>();
            foreach (var point in facetPoints)
            {
                if (filteredPoints.Count == 0)
                    filteredPoints.Add(point);

                if (filteredPoints.Last().C1 == point.C1 && filteredPoints.Last().C2 == point.C2)
                    //repetitive points
                    continue;

                filteredPoints.Add(point);
            }

            var facetTriangles = Triangulation2D.Triangulate(filteredPoints.As2Dmm()).ToArray();

            var points = new Point3DCollection();
            var triangles = new Int32Collection();
            foreach (var triangle in facetTriangles)
            {
                foreach (var pointmm in triangle)
                {
                    var point = pointmm.As2Dstep();
                    points.Add(new Point3D((point.C1 - coordOffset) * coordFactor, (point.C2 - coordOffset) * coordFactor, z));
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

                done = nextPointPercentage;
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

        #endregion

    }
}
