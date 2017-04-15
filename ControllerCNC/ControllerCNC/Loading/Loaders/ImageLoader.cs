using ControllerCNC.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControllerCNC.Primitives;
using ControllerCNC.Planning;

namespace ControllerCNC.Loading.Loaders
{
    class ImageLoader : LoaderBase
    {
        /// <inheritdoc/>
        internal override ShapeItem Load(string path, ReadableIdentifier identifier)
        {
            Message("Image processing, please wait.");
            var interpolator = new ImageInterpolator(path);
            var points = interpolator.InterpolatePoints();
            points = ShapeFactory.Centered(points);
            HideMessage();

            var shape = new ShapeItem2D(identifier, points);
            shape.MetricWidth = 50;
            return shape;
        }
    }
}
