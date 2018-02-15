using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace ControllerCNC.Planning
{
    public class CoordController
    {
        private const int _axesCount = 4;

        private readonly DriverCNC2 _cnc;

        private readonly Thread _movementWorker;

        private readonly double[] _axesDesiredSpeed = new double[_axesCount];

        private readonly double[] _axesCurrentSpeed = new double[_axesCount];

        private readonly double _axisAcceleration = Configuration.MaxPlaneAcceleration.ToMetric();

        //its safe to go back and forth with this speed delta without acceleration
        private readonly double _fastChangeSpeedDiff = 2 * Speed.FromDeltaT(Configuration.StartDeltaT).ToMetric();

        /// <summary>
        /// Limit single instruction to a small time - it helps keeping controller responsive.
        /// </summary>
        private readonly double _instructionDurationLimit = 0.05;

        private readonly object _L_desiredSpeed = new object();

        public double FastChangeSpeedDelta => _fastChangeSpeedDiff;

        public CoordController(DriverCNC2 cnc)
        {
            _cnc = cnc;

            _movementWorker = new Thread(worker);
            _movementWorker.Start();
            _movementWorker.IsBackground = true;
        }

        public void SetDesiredSpeeds(double uSpeed, double vSpeed, double xSpeed, double ySpeed)
        {
            lock (_L_desiredSpeed)
            {
                _axesDesiredSpeed[0] = uSpeed;
                _axesDesiredSpeed[1] = vSpeed;
                _axesDesiredSpeed[2] = xSpeed;
                _axesDesiredSpeed[3] = ySpeed;
                Monitor.Pulse(_L_desiredSpeed);
            }
        }

        private void worker()
        {
            while (true)
            {
                lock (_L_desiredSpeed)
                {
                    while (canWorkerStop())
                    {
                        Monitor.Wait(_L_desiredSpeed);
                    }


                    for (var i = 0; i < _axesCount; ++i)
                    {
                        var isSmallDiff = Math.Abs(_axesCurrentSpeed[i] - _axesDesiredSpeed[i]) < _fastChangeSpeedDiff;
                        if (isSmallDiff)
                            _axesCurrentSpeed[i] = _axesDesiredSpeed[i];
                    }
                }
                

                if (_cnc.IncompleteInstructionCount < 2)
                {
                    sendNextPlan();
                }
                else
                    Thread.Sleep(1);

            }
        }

        private void sendNextPlan()
        {
            var instructions = new StepInstrution[4];


            var largestAccelerationTime = getLargestAccelerationTime();
            var stopEverything = false;
            if (largestAccelerationTime > 0 && _axesCurrentSpeed.Any(c => Math.Abs(c) > 0))
            {
                stopEverything = true;
            }

            for (var i = 0; i < _axesCount; ++i)
            {
                var currentSpeed = _axesCurrentSpeed[i];
                var desiredSpeed = _axesDesiredSpeed[i];
                if (stopEverything || Math.Sign(currentSpeed) * Math.Sign(desiredSpeed) < 0)
                    desiredSpeed = 0; //first stop before reverting the direction

                StepInstrution instruction;
                if (largestAccelerationTime == 0)
                {
                    instruction = getConstantInstruction(currentSpeed);
                }
                else
                {
                    instruction = getAccelerationInstruction(currentSpeed, desiredSpeed, largestAccelerationTime);
                    _axesCurrentSpeed[i] = desiredSpeed;
                }

                instructions[i] = instruction;
            }

            var preCount = _cnc.IncompleteInstructionCount;
            _cnc.SEND(Axes.UVXY(instructions[0], instructions[1], instructions[2], instructions[3]));
            var postCount = _cnc.IncompleteInstructionCount;
        }

        private double getLargestAccelerationTime()
        {
            var maxAccelerationTime = 0.0;
            for (var i = 0; i < _axesCount; ++i)
            {
                var currentSpeed = _axesCurrentSpeed[i];
                var desiredSpeed = _axesDesiredSpeed[i];
                if (Math.Sign(currentSpeed) * Math.Sign(desiredSpeed) < 0)
                    desiredSpeed = 0;

                var accelerationTime = Math.Abs(desiredSpeed - currentSpeed) / _axisAcceleration;
                maxAccelerationTime = Math.Max(maxAccelerationTime, accelerationTime);
            }

            return maxAccelerationTime;
        }

        private StepInstrution getAccelerationInstruction(double currentSpeed, double desiredSpeed, double accelerationTime)
        {
            var speedDiff = desiredSpeed - currentSpeed;
            var acceleration = speedDiff / accelerationTime;

            var trajectory = currentSpeed * accelerationTime + 0.5 * acceleration * accelerationTime * accelerationTime;
            var trajectorySteps = (int)Math.Round(trajectory / Configuration.MilimetersPerStep);
            var currentSpeedSteps = currentSpeed / Configuration.MilimetersPerStep;
            var desiredSpeedSteps = desiredSpeed / Configuration.MilimetersPerStep;
            var builder = AccelerationBuilder.FromTo(Math.Abs(currentSpeedSteps), Math.Abs(desiredSpeedSteps), trajectorySteps, accelerationTime);
            var instruction = builder.ToInstruction();
            return instruction;
        }

        private StepInstrution getConstantInstruction(double speed)
        {
            checked
            {
                var trajectory = speed * _instructionDurationLimit;
                var trajectorySteps = (Int16)Math.Round(trajectory / Configuration.MilimetersPerStep);
                var tickCount = _instructionDurationLimit * Configuration.TimerFrequency;

                StepInstrution instruction;
                if (trajectorySteps == 0)
                {
                    instruction = new ConstantInstruction(0, (int)Math.Round(tickCount), 0);
                }
                else
                {
                    var baseDeltaExact = Math.Abs(tickCount / trajectorySteps);
                    var baseDelta = Math.Abs((int)(baseDeltaExact));
                    var tickRemainder = (UInt16)(tickCount - Math.Abs(trajectorySteps) * baseDelta);

                    instruction = new ConstantInstruction(trajectorySteps, baseDelta, tickRemainder);
                }
                return instruction;
            }
        }

        private bool canWorkerStop()
        {
            for (var i = 0; i < _axesCount; ++i)
            {
                if (_axesDesiredSpeed[i] != 0)
                    return false;

                if (_axesCurrentSpeed[i] != 0)
                    return false;
            }

            return true;
        }
    }
}
