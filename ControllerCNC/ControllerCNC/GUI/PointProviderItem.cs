using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    [Serializable]
    abstract class PointProviderItem : WorkspaceItem
    {
        /// <summary>
        /// Points provided by the item.
        /// </summary>
        internal abstract IEnumerable<Point4D> ItemPoints { get; }

        internal PointProviderItem(string name)
            :base(name)
        {
        }

        protected PointProviderItem(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
