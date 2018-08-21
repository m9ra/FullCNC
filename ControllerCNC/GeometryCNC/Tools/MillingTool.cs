using GeometryCNC.Volumetric;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryCNC.Tools
{
    public abstract class MillingTool
    {
        public abstract double[,] AsConvolution(double mapResolution);

        protected double[,] getConvolutionWithOffset(HeightMap map, double shapeOffset)
        {
            var convolution = map.GetVoxelsCopy();

            for (var xi = 0; xi < convolution.GetLength(0); ++xi)
            {
                for (var yi = 0; yi < convolution.GetLength(1); ++yi)
                {
                    if (convolution[xi, yi] == 0)
                    {
                        convolution[xi, yi] = double.NegativeInfinity;
                    }
                    else
                    {
                        var toolHeight = convolution[xi, yi] - shapeOffset;
                        if (toolHeight < 0)
                            throw new InvalidOperationException();
                        convolution[xi, yi] = toolHeight;
                    }
                }
            }

            return convolution;
        }
    }
}
