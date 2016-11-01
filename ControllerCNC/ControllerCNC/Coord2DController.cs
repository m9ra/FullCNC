using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

using ControllerCNC.Machine;
using ControllerCNC.Planning;
using ControllerCNC.Primitives;

namespace ControllerCNC
{
    class Coord2DController
    {
        private readonly DriverCNC _cnc;

        private readonly Thread _movementWorker;

        private volatile int _desiredDirectionX;

        private volatile int _desiredDirectionY;

        private volatile UInt16 _desiredVelocity = 0;

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
            var accelerationX = CreateAcceleration(_velocityX, _desiredVelocity * _desiredDirectionX);
            var accelerationY = CreateAcceleration(_velocityY, _desiredVelocity * _desiredDirectionY);

            if (accelerationX.Concat(accelerationY).Count() > 2 && accelerationX.Length != accelerationY.Length)
                throw new NotImplementedException("We have to handle reverting one axis and starting the other");

            if (accelerationX.Any() && accelerationY.Any())
            {
                for (var i = 0; i < accelerationX.Length; ++i)
                {
                    _cnc.SEND(Axes.XY(accelerationX[i], accelerationY[i]));
                }

                velocityReached();
                return;
            }

            //here we have single acceleration per axes
            if (accelerationX.Any() || accelerationY.Any())
            {
                foreach (var acceleration in accelerationX)
                {
                    _cnc.SEND(Axes.X(acceleration));
                }

                foreach (var acceleration in accelerationY)
                {
                    _cnc.SEND(Axes.Y(acceleration));
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

            var xInstruction = new ConstantInstruction(stepCountX, (UInt16)Math.Abs(_velocityX), 0);
            var yInstruction = new ConstantInstruction(stepCountY, (UInt16)Math.Abs(_velocityY), 0);
            _cnc.SEND(Axes.XY(xInstruction, yInstruction));
        }

        internal static AccelerationInstruction[] CreateAcceleration(int speed, int desiredSpeed)
        {
            if (speed == desiredSpeed)
                //no acceleration is required
                return new AccelerationInstruction[0];

            if (Math.Abs(Math.Sign(speed) - Math.Sign(desiredSpeed)) > 1)
            {
                throw new NotImplementedException("Stop and run in other direction");
            }

            var stepCount = desiredSpeed > 0 ? (Int16)5000 : (Int16)(-5000);
            if (desiredSpeed == 0)
                stepCount = speed > 0 ? (Int16)5000 : (Int16)(-5000);

            speed = Math.Abs(speed);
            desiredSpeed = Math.Abs(desiredSpeed);
            if (speed == 0)
                speed = Constants.StartDeltaT;

            if (desiredSpeed == 0)
                desiredSpeed = Constants.StartDeltaT;

            var acceleration = PlanBuilder.CalculateBoundedAcceleration((UInt16)speed, (UInt16)desiredSpeed, stepCount);

            return new AccelerationInstruction[] { acceleration };
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
