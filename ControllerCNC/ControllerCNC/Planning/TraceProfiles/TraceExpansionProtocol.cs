using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning.TraceProfiles
{
    class TraceExpansionProtocol
    {
        /// <summary>
        /// Ticks allocated by the expansion.
        /// </summary>
        public readonly int ExpansionTicks;

        /// <summary>
        /// Steps allocated by the expansion.
        /// </summary>
        public readonly int ExpansionSteps;

        /// <summary>
        /// Speed reached at end of the profile.
        /// </summary>
        public readonly Speed FinalSpeed;

        internal TraceExpansionProtocol(int expansionTicks, int expansionSteps, Speed finalSpeed)
        {
            ExpansionTicks = expansionTicks;
            ExpansionSteps = expansionSteps;
            FinalSpeed = finalSpeed;
        }
    }
}
