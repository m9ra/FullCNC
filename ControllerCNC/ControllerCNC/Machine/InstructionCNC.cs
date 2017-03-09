using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    public abstract class InstructionCNC
    {
        /// <summary>
        /// Gets instruction byte representation understandable by the machine.
        /// </summary>
        /// <returns>The bytes.</returns>
        internal abstract byte[] GetInstructionBytes();

        /// <summary>
        /// Converts the value to bytes.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The bytes.</returns>
        public static byte[] ToBytes(Int16 value)
        {
            return new byte[]{
                (byte)(value>>8),
                (byte)(value & 255)
            };
        }

        /// <summary>
        /// Converts the value to bytes.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The bytes.</returns>
        public static byte[] ToBytes(UInt16 value)
        {
            return new byte[]{
                (byte)(value>>8),
                (byte)(value & 255)
            };
        }

        /// <summary>
        /// Converts the value to bytes.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The bytes.</returns>
        public static byte[] ToBytes(int value)
        {
            return new byte[]{
                (byte)((value>>24) & 255),
                (byte)((value>>16) & 255),
                (byte)((value>>8) & 255),
                (byte)(value & 255)
            };
        }

        internal ulong CalculateTotalTime()
        {
            var axes = this as Axes;
            if (axes == null)
                return 0;

            return axes.GetInstructionDuration();
        }
    }
}
