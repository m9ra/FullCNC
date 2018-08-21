using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.GUI;
using ControllerCNC.Primitives;

namespace ControllerCNC.Loading.Loaders
{
    public abstract class LoaderBase
    {
        private ILoadProvider _provider;

        protected ILoadProvider Panel { get { return _provider; } }

        internal abstract ShapeItem Load(string path, ReadableIdentifier identifier);

        internal void Initialize(ILoadProvider provider)
        {
            if (_provider != null)
                throw new NotSupportedException("Cannot initialize twice.");

            _provider = provider;
        }

        protected void Message(string message)
        {
            if (_provider == null)
                return;

            _provider.ShowMessage(message, forceRefresh: true);
        }

        protected void Error(string message)
        {
            if (_provider == null)
                return;

            _provider.ShowError(message, forceRefresh: true);
        }

        protected void HideMessage()
        {
            if (_provider == null)
                return;

            _provider.HideMessage();
        }
    }
}
