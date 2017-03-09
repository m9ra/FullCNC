using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using ControllerCNC.GUI;
using ControllerCNC.Primitives;


namespace ControllerCNC.Loading.Loaders
{
    class Cor4dLoader : LoaderBase
    {
        /// <inheritdoc/>
        internal override ShapeItem Load(string path, ReadableIdentifier identifier)
        {
            var formatter = new BinaryFormatter();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var definition = (ShapeDefinition4D)formatter.Deserialize(stream);
                var shape = new ShapeItem4D(identifier, definition.Points);
                shape.MetricThickness = definition.Thickness;
                shape.SetOriginalSize();
                return shape;
            }
        }
    }
}
