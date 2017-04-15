using ControllerCNC.GUI;
using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using ControllerCNC.Planning;
using ControllerCNC.Loading.Loaders;

namespace ControllerCNC.Loading
{
    class ShapeFactory
    {
        private readonly CutterPanel _panel;

        private readonly Dictionary<string, LoaderBase> _loaders = new Dictionary<string, LoaderBase>();

        internal ShapeFactory(CutterPanel panel = null)
        {
            _panel = panel;

            register<Cor4dLoader>("4dcor");
            register<LinePathLoader>("line_path");
            register<SliceCutLoader>("slice_cut");
            register<CorLoader>("cor");
            register<DatLoader>("dat");
            register<ImageLoader>("bmp", "png", "jpg");
        }

        internal ShapeItem Load(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileNameWithoutExtension(path);
            var identifier = new ReadableIdentifier(name);

            if (_panel != null)
                identifier = _panel.UnusedVersion(identifier);

            if (!_loaders.ContainsKey(extension))
            {
                ShowError("No available loader found.");
                return null;
            }

            try
            {
                return _loaders[extension].Load(path, identifier);
            }
            catch (Exception ex)
            {
                ShowError("Loading failed with an error: " + ex.Message);
                return null;
            }
        }

        internal static IEnumerable<Point2Dmm> Centered(IEnumerable<Point2Dmm> points)
        {
            var maxC1 = points.Select(p => p.C1).Max();
            var maxC2 = points.Select(p => p.C2).Max();

            var minC1 = points.Select(p => p.C1).Min();
            var minC2 = points.Select(p => p.C2).Min();

            var diffC1 = maxC1 - minC1;
            var diffC2 = maxC2 - minC2;

            var c1Offset = -minC1 - diffC1 / 2;
            var c2Offset = -minC2 - diffC2 / 2;

            return points.Select(p => new Point2Dmm(p.C1 + c1Offset, p.C2 + c2Offset)).ToArray();
        }

        /// <summary>
        /// Registers loader for all given extensions.
        /// </summary>
        /// <param name="extensions"></param>
        private void register<LoaderT>(params string[] extensions)
            where LoaderT : LoaderBase, new()
        {
            var loader = new LoaderT();
            loader.Initialize(_panel);
            foreach (var extension in extensions)
            {
                _loaders.Add("." + extension.ToLowerInvariant(), loader);
            }
        }

        internal void ShowError(string message)
        {
            if (_panel == null)
                return;

            _panel.ShowError(message);
        }
    }
}
