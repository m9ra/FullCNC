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

using System.Windows.Media.Media3D;

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
        public EditorWindow()
        {
            InitializeComponent();
            var points = ShapeDrawing.CircleToSquare().As4Df();

            var facet1 = new FacetShape(points.ToUV());
            var facet2 = new FacetShape(points.ToXY());

            drawShape(facet1, 100, facet2);
        }

        private void drawShape(FacetShape facet1, double metricThickness, FacetShape facet2)
        {
            if (facet1.Points.Count() != facet2.Points.Count())
                throw new NotSupportedException("Invalid facets");

            var coordinates = facet1.Points.Concat(facet2.Points).SelectMany(p => new[] { p.C1, p.C2 }).ToArray();
            var maxCoord = coordinates.Max();
            var minCoord = coordinates.Min();
            var coordOffset = minCoord + (maxCoord - minCoord) / 2;
            var coordFactor = 1.0 / (maxCoord - coordOffset);

            var thickness = metricThickness / Constants.MilimetersPerStep;
            var facet1Z = -thickness / 2 * coordFactor;
            var facet2Z = thickness / 2 * coordFactor;

            var group = new Model3DGroup();
            group.Children.Add(getFacetModel(facet1, facet1Z, coordOffset, coordFactor, true));
            group.Children.Add(getShapeCoatingModel(facet1, facet1Z, facet2, facet2Z, coordOffset, coordFactor));
            group.Children.Add(getFacetModel(facet2, facet2Z, coordOffset, coordFactor, false));

            var visualModel = new ModelVisual3D();
            Trackball.Model = visualModel;
            visualModel.Content = group;
            View3D.Children.Add(visualModel);
        }

        private static GeometryModel3D getShapeCoatingModel(FacetShape facet1, double facet1Z, FacetShape facet2, double facet2Z, double coordOffset, double coordFactor)
        {
            var points = new Point3DCollection();
            var triangles = new Int32Collection();
            var points1 = facet1.Points.ToArray();
            var points2 = facet2.Points.ToArray();
            for (var i = 0; i < points1.Length; ++i)
            {
                var f1p = points1[i];
                var f2p = points2[i];

                points.Add(new Point3D((f1p.C1 - coordOffset) * coordFactor, (f1p.C2 - coordOffset) * coordFactor, facet1Z));
                points.Add(new Point3D((f2p.C1 - coordOffset) * coordFactor, (f2p.C2 - coordOffset) * coordFactor, facet2Z));

                var i2 = i * 2;
                var bodyTriangle1 = new int[] { i2 + 1, i2, i2 + 2 };
                var bodyTriangle2 = new int[] { i2 + 1, i2 + 2, i2 + 3 };
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
            return model;
        }

        private static GeometryModel3D getFacetModel(FacetShape facet, double z, double coordOffset, double coordFactor, bool isFrontFace)
        {
            var facetTriangles = Triangulation2D.Triangulate(facet.Points);

            var points = new Point3DCollection();
            var triangles = new Int32Collection();
            foreach (var triangle in facetTriangles)
            {
                foreach (var point in triangle)
                    points.Add(new Point3D((point.C1 - coordOffset) * coordFactor, (point.C2 - coordOffset) * coordFactor, z));

                var i = points.Count - 3;
                var triangleIndexes = isFrontFace ? new[] { i, i + 2, i + 1 } : new[] { i, i + 1, i + 2 };
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
    }
}
