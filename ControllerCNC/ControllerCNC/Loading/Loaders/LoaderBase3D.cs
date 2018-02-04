using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.GUI;
using ControllerCNC.Primitives;

namespace ControllerCNC.Loading.Loaders
{
    abstract class LoaderBase3D : LoaderBase
    {
        internal abstract IEnumerable<Point2Dmm[]> LoadPoints(string path);
    }
}
