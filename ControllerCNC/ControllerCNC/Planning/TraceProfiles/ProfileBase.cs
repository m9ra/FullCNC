using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

namespace ControllerCNC.Planning.TraceProfiles
{
    abstract class TraceProfileBase
    {
        /// <summary>
        /// How much of the time is required for the profile.
        /// </summary>
        protected abstract int requiresTicks();

        /// <summary>
        /// Minimal time which will bring profile to next step.
        /// </summary>
        protected abstract int nextStepTicks();

        /// <summary>
        /// How many ticks is allocated for the profile already.
        /// </summary>
        protected int ProfileTickCount { get { throw new NotImplementedException(); } }

        /// <summary>
        /// Context which is actually used for profile. 
        /// </summary>
        private TraceContext _actualContext;

        protected Speed StartSpeedAxis()
        {
            throw new NotImplementedException();
        }

        protected Speed AsPlaneAxis(Speed planeSpeed)
        {
            throw new NotImplementedException();
        }

        protected double AsPlaneAxisDouble(Acceleration planeAcceleration)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determine whether profile can be further expanded.
        /// </summary>
        internal bool CanBeExpanded(TraceContext context)
        {
            requireEmptyContext();
            try
            {
                _actualContext = context;
                return nextStepTicks() != 0;
            }
            finally
            {
                _actualContext = null;
            }
        }

        /// <summary>
        /// Determine whether profile does not require any additional time to be satisfied.
        /// </summary>
        internal bool IsSatisfied(TraceContext context)
        {
            requireEmptyContext();
            try
            {
                _actualContext = context;
                return requiresTicks() == 0;
            }
            finally
            {
                _actualContext = null;
            }
        }

        /// <summary>
        /// Expands on the smallest satisfiing length or expands for a single step if already satisfied.
        /// </summary>
        internal void Expand(TraceContext context)
        {
            requireEmptyContext();
            try
            {
                _actualContext = context;

                var expansionTicks = requiresTicks();
                if (expansionTicks == 0)
                    expansionTicks = nextStepTicks();

                applyExpansionTicks(expansionTicks);
            }
            finally
            {
                _actualContext = null;
            }
        }

        private void requireEmptyContext()
        {
            if (_actualContext != null)
                throw new InvalidOperationException("Nested context operation detected.");
        }

        private void applyExpansionTicks(int expansionTicks)
        {
            throw new NotImplementedException();
        }
    }
}
