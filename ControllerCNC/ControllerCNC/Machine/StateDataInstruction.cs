using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    /// <summary>
    /// Instruction for obtaining state data from a machine.
    /// </summary>
    class StateDataInstruction : InstructionCNC
    {
        internal override byte[] GetInstructionBytes()
        {
            return new byte[] { (byte)'D' };
        }
    }
}
