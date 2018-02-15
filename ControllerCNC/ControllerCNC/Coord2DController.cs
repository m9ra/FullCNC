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
    public class Coord2DController
    {
        private readonly DriverCNC _cnc;

        private readonly Thread _movementWorker;

        private volatile int _desiredDirectionX;

        private volatile int _desiredDirectionY;

        private volatile UInt16 _desiredSpeed = 0;

        private volatile int _deltaTX;

        private volatile int _deltaTY;

        private volatile bool _stop = true;

        private volatile bool _moveXY = true;

        private volatile bool _moveUV = true;

        public Coord2DController(DriverCNC cnc)
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
                    Thread.Sleep(50);
                }
                else
                {
                    if (_cnc.IncompleteInstructionCount < 4)
                        sendNextPlan();
                    else
                        Thread.Sleep(1);
                }
            }
        }

        private void sendNextPlan()
        {
            var accelerationX = CreateAcceleration(_deltaTX, _desiredSpeed * _desiredDirectionX);
            var accelerationY = CreateAcceleration(_deltaTY, _desiredSpeed * _desiredDirectionY);

            if (accelerationX.Concat(accelerationY).Count() > 2 && accelerationX.Length != accelerationY.Length)
                throw new NotImplementedException("We have to handle reverting one axis and starting the other");

            if (accelerationX.Any() && accelerationY.Any())
            {
                for (var i = 0; i < accelerationX.Length; ++i)
                {
                    sendInstruction(accelerationX[i], accelerationY[i]);
                }

                velocityReached();
                return;
            }

            //here we have single acceleration per axes
            if (accelerationX.Any() || accelerationY.Any())
            {
                foreach (var acceleration in accelerationX)
                {
                    sendInstruction(acceleration, null);
                }

                foreach (var acceleration in accelerationY)
                {
                    sendInstruction(null, acceleration);
                }

                velocityReached();
                return;
            }

            velocityReached();
            if (_deltaTX == 0 && _deltaTY == 0)
            {
                _stop = true;
                return;
            }

            var instructionTime = 100 * 350;
            var timedStepCountX = _deltaTX == 0 ? 0 : instructionTime / Math.Abs(_deltaTX);
            var timedStepCountY = _deltaTY == 0 ? 0 : instructionTime / Math.Abs(_deltaTY);

            var stepCountX = (Int16)(timedStepCountX * Math.Sign(_deltaTX));
            var stepCountY = (Int16)(timedStepCountY * Math.Sign(_deltaTY));

            var xInstruction = new ConstantInstruction(stepCountX, (UInt16)Math.Abs(_deltaTX), 0);
            var yInstruction = new ConstantInstruction(stepCountY, (UInt16)Math.Abs(_deltaTY), 0);

            sendInstruction(xInstruction, yInstruction);
        }

        private void sendInstruction(StepInstrution planeInstruction1, StepInstrution planeInstruction2)
        {
            StepInstrution u, v, x, y;
            u = v = x = y = null;

            if (_moveUV)
            {
                u = planeInstruction1;
                v = planeInstruction2;
            }

            if (_moveXY)
            {
                x = planeInstruction1;
                y = planeInstruction2;
            }

            _cnc.SEND(Axes.UVXY(u, v, x, y));
        }

        internal static AccelerationInstruction[] CreateAcceleration(int speed, int desiredSpeed)
        {
            var canSkipAcceleration = speed == desiredSpeed;
            canSkipAcceleration |= Math.Abs(speed) >= Configuration.StartDeltaT && Math.Abs(desiredSpeed) >= Configuration.StartDeltaT;
            if (canSkipAcceleration)
            {
                //no acceleration is required
                return new AccelerationInstruction[0];
            }

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
                speed = Configuration.StartDeltaT;

            if (desiredSpeed == 0)
                desiredSpeed = Configuration.StartDeltaT;

            var acceleration = PlanBuilder.CalculateBoundedAcceleration((UInt16)speed, (UInt16)desiredSpeed, stepCount);

            return new AccelerationInstruction[] { acceleration };
        }

        private void velocityReached()
        {
            _deltaTX = _desiredDirectionX * _desiredSpeed;
            _deltaTY = _desiredDirectionY * _desiredSpeed;
        }

        public void SetMovement(int dirX, int dirY)
        {
            _desiredDirectionX = dirX;
            _desiredDirectionY = dirY;
            _stop = false;
        }

        public void SetPlanes(bool uv, bool xy)
        {
            _moveUV = uv;
            _moveXY = xy;
        }

        public void SetSpeed(int speed)
        {
            _desiredSpeed = (UInt16)speed;
        }
    }
}
