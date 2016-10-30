using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    class AxesInstruction : InstructionCNC
    {
        public static AxesInstruction XY(InstructionCNC partX, InstructionCNC partY)
        {
            throw new NotImplementedException();
        }

        public static AxesInstruction X(InstructionCNC partX)
        {
            throw new NotImplementedException();
        }

        public static AxesInstruction Y(InstructionCNC partY)
        {
            throw new NotImplementedException();
        }

        /// </inheritdoc>
        internal override byte[] GetInstructionBytes()
        {
            throw new NotImplementedException();
        }
    }
}
