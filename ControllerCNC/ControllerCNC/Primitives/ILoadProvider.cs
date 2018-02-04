using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    public interface ILoadProvider
    {
        ReadableIdentifier UnusedVersion(ReadableIdentifier name);

        void ShowError(string message, bool forceRefresh = false);
        void ShowMessage(string message, bool forceRefresh = false);
        void HideMessage();
    }
}
