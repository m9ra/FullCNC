using GeometryCNC.Primitives;
using GeometryCNC.Volumetric;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryCNC.Tools
{
    class FlatEndMill : MillingTool
    {
        public readonly double Diameter;

        public FlatEndMill(double diameter)
        {
            Diameter = diameter;
        }

        public override double[,] AsConvolution(double mapResolution)
        {
            var shapeOffset = 1.0;
            var toolShape = new CylinderShape(new Point(Diameter / 2 - mapResolution / 2, Diameter / 2 - mapResolution / 2), shapeOffset, Diameter);
            var map = HeightMap.From(toolShape, new Point(0, 0), new Point(Diameter, Diameter), mapResolution);
            return getConvolutionWithOffset(map, shapeOffset);
        }
    }
}
