using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine.Logging
{
    class ChannelLogger
    {
        internal int NextActivation { get; private set; }

        internal bool IsActive => _stepsToActivate > 0;

        private readonly Queue<int> _steps = new Queue<int>();

        private readonly StreamWriter _log;

        private int _ticksFromLastStep;

        private int _stepsToActivate;

        internal ChannelLogger(string logPath)
        {
            _log = File.AppendText(logPath);
        }

        internal void LoadInstruction(StepInstrution instruction)
        {
            if (instruction.IsActivationBoundary)
                NextActivation = 0;

            if (_steps.Any())
                throw new InvalidOperationException("Cannot fill steps now");

            foreach (var step in instruction.GetStepTimings())
            {
                _stepsToActivate += 1;
                _steps.Enqueue(Math.Abs(step));
            }

            if (_steps.Any())
                NextActivation += _steps.Dequeue();
        }

        internal void TryStep(int ticks)
        {
            if (ticks < 0)
                throw new InvalidOperationException("can't step in negative ticks");

            NextActivation -= ticks;
            _ticksFromLastStep += ticks;

            if (NextActivation < 0 && IsActive)
                throw new InvalidOperationException();

            if (NextActivation < 0)
                return;

            if (NextActivation > Configuration.MinActivationDelay)
                return;

            if (!IsActive)
                return;

            _stepsToActivate -= 1;

            var lastActivation = NextActivation;
            if (_steps.Any())
            {
                NextActivation = _steps.Dequeue();
            }

            // compensate for activation grouping
            NextActivation += lastActivation;

            logStep(_ticksFromLastStep);
            _ticksFromLastStep = 0;
        }

        internal void LogMessage(string message)
        {
            _log.WriteLine(message);
        }

        private void logStep(int ticksFromLastStep)
        {
            _log.WriteLine(ticksFromLastStep);
        }

        public void Flush()
        {
            _log.Flush();
        }
    }
}
