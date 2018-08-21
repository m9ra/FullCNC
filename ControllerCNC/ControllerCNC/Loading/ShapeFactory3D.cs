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
    abstract class LoaderBase3D : LoaderBase
    {
        internal abstract IEnumerable<Point2Dmm[]> LoadPoints(string path);
    }

    public class ShapeFactory3D
    {
        private readonly ILoadProvider _panel;

        private readonly Dictionary<string, LoaderBase3D> _loaders = new Dictionary<string, LoaderBase3D>();

        public ShapeFactory3D(ILoadProvider nameProvider = null)
        {
            _panel = nameProvider;

            register<CorLoader>("cor");
            register<DatLoader>("dat");
            register<ImageLoader>("bmp", "png", "jpg");
        }

        public double[,] LoadRelief(string path, out ReadableIdentifier identifier)
        {
            var imageLoader = new ImageLoader();
            var map = imageLoader.LoadRelief(path);

            var extension = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileNameWithoutExtension(path);
            identifier = new ReadableIdentifier(name);

            if (_panel != null)
                identifier = _panel.UnusedVersion(identifier);

            return map;
        }

        public IEnumerable<Point2Dmm[]> Load(string path, out ReadableIdentifier identifier)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileNameWithoutExtension(path);
            identifier = new ReadableIdentifier(name);

            if (_panel != null)
                identifier = _panel.UnusedVersion(identifier);

            if (!_loaders.ContainsKey(extension))
            {
                ShowError("No available loader found.");
                return null;
            }

            try
            {
                return _loaders[extension].LoadPoints(path);
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
            where LoaderT : LoaderBase3D, new()
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
