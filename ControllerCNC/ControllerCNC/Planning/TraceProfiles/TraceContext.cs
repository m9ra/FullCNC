using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Planning.TraceProfiles
{
    class TraceContext
    {
        private readonly List<TraceProfileBase> _profiles = new List<TraceProfileBase>();

        internal IEnumerable<TraceProfileBase> Profiles { get { return _profiles; } }

        internal IEnumerable<TraceProfileBase> NonSatisfiedProfiles { get { return _profiles.Where(p => !p.IsSatisfied(this)); } }

        internal bool AreProfilesSatisfied
        {
            get
            {
                return !NonSatisfiedProfiles.Any();
            }
        }

        internal bool IsTraceCovered
        {
            get
            {
                return _profiles.All(p => !p.CanBeExpanded(this));
            }
        }

        internal void Register(TraceProfileBase profile)
        {
            _profiles.Add(profile);
            throw new NotImplementedException();
        }
    }
}
