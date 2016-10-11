using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC
{
    class PositionController
    {
        private DriverCNC _cnc;

        private int _currentPosition;

        public PositionController(DriverCNC driver)
        {
            _cnc = driver;
        }

        public void SetPosition(int newPosition)
        {
            var steps = Math.Abs(newPosition - _currentPosition);
            var direction = newPosition > _currentPosition;
            Int16 accCoeficient = 1;
            var deltaT = _cnc.GetFastestDeltaT(steps / 2 / 5);
            Int16 accelerationDistance = (Int16)(_cnc.GetAccelerationDistance(deltaT) * accCoeficient);

            var constantTrackSteps = steps - 2 * accelerationDistance;



            if (!direction)
                accelerationDistance = (Int16)(-accelerationDistance);

            if (Math.Abs(accelerationDistance) > 0)
                _cnc.SEND_Acceleration(accelerationDistance, 1, accCoeficient, _cnc.StartDeltaT);

            while (constantTrackSteps > 0)
            {
                var nextSteps = (Int16)Math.Min(20000, constantTrackSteps);
                constantTrackSteps -= nextSteps;
                if (!direction)
                    nextSteps = (Int16)(-nextSteps);
                _cnc.SEND_Constant(nextSteps, deltaT, 0);
            }

            if (Math.Abs(accelerationDistance) > 0)
                _cnc.SEND_Deceleration(accelerationDistance, 1, accCoeficient, deltaT);

            //position setting is blocking for now
            while (_cnc.IncompletePlanCount > 0)
                System.Threading.Thread.Sleep(1);
            
            _currentPosition = newPosition;
        }
    }
}
