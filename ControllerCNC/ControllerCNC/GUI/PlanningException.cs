using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.GUI
{
    class PlanningException : Exception
    {
        internal PlanningException(string errorMessage) :
            base(errorMessage)
        {

        }
    }
}
