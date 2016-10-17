using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    class SegmentPlanBuilder1D
    {
        /// <summary>
        /// Maximal velocity reserved for the segment.
        /// </summary>
        public readonly Velocity MaxVelocity;

        /// <summary>
        /// Length of the segment in number of steps.
        /// </summary>
        public readonly int Length;

        /// <summary>
        /// Acceleration done after entering the segment.
        /// </summary>
        public Acceleration TrailingAcceleration { get; private set; }

        /// <summary>
        /// Acceleration done before leaving the segment.
        /// </summary>
        public Acceleration LeadingAcceleration { get; private set; }
    }
}
