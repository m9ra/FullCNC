using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    abstract class PointProviderItem : WorkspaceItem
    {
        /// <summary>
        /// Points provided by the item.
        /// </summary>
        internal abstract IEnumerable<Point4D> ItemPoints { get; }
    }
}
