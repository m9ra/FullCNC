using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.GUI;
using ControllerCNC.Primitives;

namespace ControllerCNC.Loading.Loaders
{
    abstract class LoaderBase
    {
        private CutterPanel _panel;

        protected CutterPanel Panel { get { return _panel; } }

        internal abstract ShapeItem Load(string path, ReadableIdentifier identifier);

        internal void Initialize(CutterPanel panel)
        {
            if (_panel != null)
                throw new NotSupportedException("Cannot initialize twice.");

            _panel = panel;
        }

        protected void Message(string message)
        {
            if (_panel == null)
                return;

            _panel.ShowMessage(message, forceRefresh: true);
        }

        protected void Error(string message)
        {
            if (_panel == null)
                return;

            _panel.ShowError(message, forceRefresh: true);
        }

        protected void HideMessage()
        {
            if (_panel == null)
                return;

            _panel.HideMessage();
        }
    }
}
