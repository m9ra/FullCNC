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
