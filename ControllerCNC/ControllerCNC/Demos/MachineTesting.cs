using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Planning;

namespace ControllerCNC.Demos
{
    static class MachineTesting
    {        
        /// <summary>
        /// Demo with several acceleration ramps.
        /// </summary>
        public static PlanBuilder Ramping()
        {
            var builder = new PlanBuilder();

            builder.SEND_TransitionRPM(-400 * 10, 0, 1000, 500);
            builder.SEND_TransitionRPM(-400 * 10, 500, 500, 500);
            builder.SEND_TransitionRPM(-400 * 20, 500, 1500, 0);
            return builder;
        }

        /// <summary>
        /// Demo with a single revolution interrupted several times.
        /// </summary>
        public static PlanBuilder InterruptedRevolution()
        {
            var plan = new PlanBuilder();
            var segmentation = 100;
            for (var i = 0; i < 400 / segmentation; ++i)
            {
                plan.SEND_TransitionRPM(segmentation, 0, 1500, 0);
            }

            return plan;
        }

        /// <summary>
        /// A single revolution with a lot of forward/backward direction changes.
        /// </summary>
        /// <returns></returns>
        public static PlanBuilder BackAndForwardRevolution()
        {
            var plan = new PlanBuilder();
            var overShoot = 100;
            var segmentation = 4;
            for (var i = 0; i < 400 / segmentation; ++i)
            {
                plan.SEND_TransitionRPM(-overShoot, 0, 1500, 0);
                plan.SEND_TransitionRPM(segmentation + overShoot, 0, 1500, 0);
            }

            return plan;
        }
    }
}
