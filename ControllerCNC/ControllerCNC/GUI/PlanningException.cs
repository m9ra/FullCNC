using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.GUI
{
    public class PlanningException : Exception
    {
        public PlanningException(string errorMessage) :
            base(errorMessage)
        {

        }
    }
}
