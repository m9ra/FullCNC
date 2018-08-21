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
    public class BallnoseEndMill : MillingTool
    {
        public readonly double Diameter;

        public BallnoseEndMill(double diameter)
        {
            Diameter = diameter;
        }

        public override double[,] AsConvolution(double mapStepSize)
        {
            var toolShape = new HemisphereShape(new Point(Diameter / 2 - mapStepSize / 2, Diameter / 2 - mapStepSize / 2), Diameter / 2);
            var map = HeightMap.From(toolShape, new Point(0, 0), new Point(Diameter, Diameter), mapStepSize);
            return getConvolutionWithOffset(map, 0);
        }
    }
}
