using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ControllerCNC.Machine;

namespace ControllerCNC
{
    class PositionController
    {
        private DriverCNC2 _cnc;

        private int _currentPosition;

        public PositionController(DriverCNC2 driver)
        {
            _cnc = driver;
        }

        public void SetPosition(int newPosition)
        {
            var steps = newPosition - _currentPosition;

            var builder = new Planning.PlanBuilder();
            //builder.AddRampedSteps(steps, Constants.FastestDeltaT);
            builder.AddConstantSpeedTransitionXY(steps, steps, Configuration.ReverseSafeSpeed);
            builder.DuplicateXYtoUV();
            _cnc.SEND(builder.Build());

            //position setting is blocking for now
            while (_cnc.IncompleteInstructionCount > 0)
                System.Threading.Thread.Sleep(1);

            _currentPosition = newPosition;
        }

        internal void ResetPosition()
        {
            _currentPosition = 0;
        }
    }
}
