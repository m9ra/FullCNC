using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

using ControllerCNC.Primitives;

namespace ControllerCNC
{
    class Coord2DController
    {
        private readonly DriverCNC _cnc;

        private readonly Thread _movementWorker;

        private volatile int _desiredDirectionX;

        private volatile int _desiredDirectionY;

        private volatile UInt16 _desiredVelocity = 350;

        private volatile int _velocityX;

        private volatile int _velocityY;

        private volatile bool _stop = true;

        internal Coord2DController(DriverCNC cnc)
        {
            _cnc = cnc;

            _movementWorker = new Thread(worker);
            _movementWorker.Start();
            _movementWorker.IsBackground = true;
        }

        private void worker()
        {
            while (true)
            {
                if (_stop)
                {
                    Thread.Sleep(100);
                }
                else
                {
                    if (_cnc.IncompletePlanCount < 3)
                        sendNextPlan();
                    else
                        Thread.Sleep(1);
                }
            }
        }

        private void sendNextPlan()
        {
            var accelerationX = createAcceleration(_velocityX, _desiredVelocity * _desiredDirectionX);
            var accelerationY = createAcceleration(_velocityY, _desiredVelocity * _desiredDirectionY);

            if (accelerationX.Concat(accelerationY).Count() > 2 && accelerationX.Length != accelerationY.Length)
                throw new NotImplementedException("We have to handle reverting one axis and starting the other");

            if (accelerationX.Any() && accelerationY.Any())
            {
                for (var i = 0; i < accelerationX.Length; ++i)
                {
                    _cnc.StepperIndex = 2;
                    _cnc.SEND(accelerationX[i]);
                    _cnc.SEND(accelerationY[i]);
                }

                velocityReached();
                return;
            }

            //here we have single acceleration per axes
            if (accelerationX.Any() || accelerationY.Any())
            {
                foreach (var acceleration in accelerationX)
                {
                    _cnc.StepperIndex = 2;
                    _cnc.SEND(acceleration);
                    _cnc.SEND_Acceleration(0, 0, 0, 0, 1);
                }

                foreach (var acceleration in accelerationY)
                {
                    _cnc.StepperIndex = 2;
                    _cnc.SEND_Acceleration(0, 0, 0, 0, 1);
                    _cnc.SEND(acceleration);
                }

                velocityReached();
                return;
            }


            if (_velocityX == 0 && _velocityY == 0)
            {
                _stop = true;
                return;
            }

            var stepCountX = (Int16)(200 * 1 * Math.Sign(_velocityX));
            var stepCountY = (Int16)(200 * 1 * Math.Sign(_velocityY));
            _cnc.StepperIndex = 2;
            _cnc.SEND_Constant(stepCountX, (UInt16)Math.Abs(_velocityX), 0, 0);
            _cnc.SEND_Constant(stepCountY, (UInt16)Math.Abs(_velocityY), 0, 0);
        }

        AccelerationPlan[] createAcceleration(int velocity, int desiredVelocity)
        {
            if (velocity == desiredVelocity)
                //no acceleration is required
                return new AccelerationPlan[0];

            if (Math.Abs(Math.Sign(velocity) - Math.Sign(desiredVelocity)) > 1)
            {
                throw new NotImplementedException("Stop and run in other direction");
            }

            var stepCount = desiredVelocity > 0 ? (Int16)5000 : (Int16)(-5000);
            if (desiredVelocity == 0)
                stepCount = velocity > 0 ? (Int16)5000 : (Int16)(-5000);

            velocity = Math.Abs(velocity);
            desiredVelocity = Math.Abs(desiredVelocity);
            if (velocity == 0)
                velocity = _cnc.StartDeltaT;

            if (desiredVelocity == 0)
                desiredVelocity = _cnc.StartDeltaT;

            var acceleration = _cnc.CalculateBoundedAcceleration((UInt16)velocity, (UInt16)desiredVelocity, stepCount);

            return new AccelerationPlan[] { acceleration };
        }

        private void velocityReached()
        {
            _velocityX = _desiredDirectionX * _desiredVelocity;
            _velocityY = _desiredDirectionY * _desiredVelocity;
        }

        internal void SetMovement(int dirX, int dirY)
        {
            _desiredDirectionX = dirX;
            _desiredDirectionY = dirY;
            _stop = false;
        }

        internal void SetSpeed(int velocity)
        {
            _desiredVelocity = (UInt16)velocity;
        }
    }
}
