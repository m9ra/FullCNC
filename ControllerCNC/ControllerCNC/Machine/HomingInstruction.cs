using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    class HomingInstruction : InstructionCNC
    {
        internal override byte[] GetInstructionBytes()
        {
            return new byte[] { (byte)'H' };
        }
    }
}
