using ControllerCNC.Machine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{

    /// <summary>
    /// Determines how many steps per second will be accelerated per second
    /// </summary>
    [Serializable]
    public class Acceleration
    {
        /// <summary>
        /// Speed that will be accelerated in <see cref="Ticks"/>.
        /// </summary>
        public readonly Speed Speed;


        /// <summary>
        /// How long it take to accelerate to <see cref="Speed"/> (in CNC timer tick count).
        /// </summary>
        public readonly long Ticks;

        public Acceleration(Speed speed, long ticks)
        {
            Speed = speed;
            Ticks = ticks;
        }

        public double ToMetric()
        {
            var metricSpeed = Speed.ToMetric();
            var time = 1.0 * Ticks / Constants.TimerFrequency;

            return metricSpeed / time;
        }
    }
}
