using ControllerCNC.Loading;
using ControllerCNC.Primitives;
using MillingRouter3D.GUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MillingRouter3D
{
    class MillingShapeFactory
    {
        private readonly ILoadProvider _provider;

        private readonly ShapeFactory3D _factory;

        internal MillingShapeFactory(ILoadProvider provider)
        {
            _factory = new ShapeFactory3D(provider);
            _provider = provider;
        }

        public MillingItem Load(string path)
        {
            var extension = Path.GetExtension(path);
            if (extension == ".gcode" || extension == ".nc")
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var identifier = new ReadableIdentifier(name);

                if (_provider != null)
                    identifier = _provider.UnusedVersion(identifier);

                return new MillingShapeItemGCode(File.ReadAllText(path), identifier);
            }


            var useHeightmap = false;
            if (useHeightmap)
            {
                var reliefShape = _factory.LoadRelief(path, out var name);
                if (reliefShape != null)
                {
                    var reliefShapeItem = new MillingShapeItemRelief(name, reliefShape);
                    reliefShapeItem.MetricHeight = 100;
                    return reliefShapeItem;
                }
            }
            else
            {
                var flatShape = _factory.Load(path, out var name);
                if (flatShape != null)
                {
                    var shapeItem = new MillingShapeItem2D(name, flatShape);
                    shapeItem.MetricHeight = 100;
                    return shapeItem;
                }
            }

            return null;
        }
    }
}
