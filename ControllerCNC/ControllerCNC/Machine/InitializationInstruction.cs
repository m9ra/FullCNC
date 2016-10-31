using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    class InitializationInstruction : InstructionCNC
    {
        /// </inheritdoc>
        internal override byte[] GetInstructionBytes()
        {
            return new byte[] { (byte)'I' };
        }
    }
}
