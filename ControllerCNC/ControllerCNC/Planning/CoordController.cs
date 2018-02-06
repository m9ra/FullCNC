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

        private readonly DriverCNC _cnc;

        private readonly Thread _movementWorker;

        private readonly double[] _axesDesiredSpeed = new double[_axesCount];

        private readonly double[] _axesCurrentSpeed = new double[_axesCount];

        private readonly double _axisAcceleration = Constants.MaxPlaneAcceleration.ToMetric();

        //its safe to go back and forth with this speed delta without acceleration
        private readonly double _fastChangeSpeedDiff = 2 * Speed.FromDeltaT(Constants.StartDeltaT).ToMetric();

        /// <summary>
        /// Limit single instruction to a small time - it helps keeping controller responsive.
        /// </summary>
        private readonly double _instructionDurationLimit = 0.05;

        private readonly object _L_desiredSpeed = new object();

        public double FastChangeSpeedDelta => _fastChangeSpeedDiff;

        public CoordController(DriverCNC cnc)
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
            var lastSend = DateTime.Now;
            var sentStreak = 0;
            while (true)
            {
                lock (_L_desiredSpeed)
                {
                    while (canWorkerStop())
                    {
                        sentStreak = 0;
                        Monitor.Wait(_L_desiredSpeed);
                    }


                    for (var i = 0; i < _axesCount; ++i)
                    {
                        var isSmallDiff = Math.Abs(_axesCurrentSpeed[i] - _axesDesiredSpeed[i]) < _fastChangeSpeedDiff;
                        if (isSmallDiff)
                            _axesCurrentSpeed[i] = _axesDesiredSpeed[i];
                    }
                }

                while (sentStreak>0 && (DateTime.Now - lastSend).TotalSeconds > _instructionDurationLimit)
                {
                    --sentStreak;
                    lastSend = DateTime.Now;
                }

                if (_cnc.IncompletePlanCount < 2 && sentStreak < 3)
                {
                    sendNextPlan();
                    ++sentStreak;
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

            var preCount = _cnc.IncompletePlanCount;
            _cnc.SEND(Axes.UVXY(instructions[0], instructions[1], instructions[2], instructions[3]));
            var postCount = _cnc.IncompletePlanCount;
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
            var trajectorySteps = (int)Math.Round(trajectory / Constants.MilimetersPerStep);
            var currentSpeedSteps = currentSpeed / Constants.MilimetersPerStep;
            var desiredSpeedSteps = desiredSpeed / Constants.MilimetersPerStep;
            var builder = AccelerationBuilder.FromTo(Math.Abs(currentSpeedSteps), Math.Abs(desiredSpeedSteps), trajectorySteps, accelerationTime);
            var instruction = builder.ToInstruction();
            return instruction;
        }

        private StepInstrution getConstantInstruction(double speed)
        {
            checked
            {
                var trajectory = speed * _instructionDurationLimit;
                var trajectorySteps = (Int16)Math.Round(trajectory / Constants.MilimetersPerStep);
                var tickCount = _instructionDurationLimit * Constants.TimerFrequency;

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
