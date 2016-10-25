using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrajectorySimulator
{
    class ChannelTrace
    {
        private List<int> _times = new List<int>();

        public IEnumerable<int> Times { get { return _times; } }

        internal void AddTime(int currentDelta)
        {
            _times.Add(currentDelta);
        }
    }
}
