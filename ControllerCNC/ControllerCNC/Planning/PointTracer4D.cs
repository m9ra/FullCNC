using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Primitives;

using ControllerCNC.Planning.TraceProfiles;

namespace ControllerCNC.Planning
{
    class PointTracer4D
    {
        private Point4Dstep _nextPoint = null;

        private TraceContext _context = null;

        /// <summary>
        /// Sets the next point to be reached.
        /// </summary>
        /// <param name="point">Point where the tracer will run to.</param>
        internal void StartNextPoint(Point4Dstep point)
        {
            if (_nextPoint != null)
                throw new InvalidOperationException("Cannot set next point until profile of previous one is complete");

            _nextPoint = point;
            _context = new TraceContext(_context);
        }

        internal void AddProfile(TraceProfileBase profile)
        {
            requirePointSet();
            _context.Register(profile);
        }

        internal void FinishPoint()
        {
            requirePointSet();
            while (!_context.IsTraceCovered)
            {
                var profilesToExpand = _context.AreProfilesSatisfied ? _context.Profiles : _context.NonSatisfiedProfiles;
                expandProfiles(profilesToExpand, _context);

                if (!_context.AreProfilesSatisfied)
                    throw new NotImplementedException("Undo the expansion - we can do that because expansion is done in single step manner/single direction");
            }

            if (!_context.AreProfilesSatisfied)
                throw new InvalidOperationException("Cannot satisfy all profiles");

            throw new NotImplementedException("Generate appropriate instructions");
        }

        private void expandProfiles(IEnumerable<TraceProfileBase> profiles, TraceContext context)
        {
            foreach (var profile in profiles)
            {
                profile.Expand(context);
            }
        }

        private void requirePointSet()
        {
            if (_nextPoint == null)
                throw new InvalidOperationException("Next point was not started yet.");
        }
    }
}
