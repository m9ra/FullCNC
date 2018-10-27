using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine.Logging
{
    public class StepLogger
    {
        ChannelLogger _u, _v, _x, _y;
        public StepLogger(string logPath)
        {
            Directory.CreateDirectory("log");
            var filename = Path.Combine(logPath, "log", "steps_" + DateTime.Now.Ticks + "-");

            var extension = ".log";
            _u = new ChannelLogger(filename + "u" + extension);
            _v = new ChannelLogger(filename + "v" + extension);
            _x = new ChannelLogger(filename + "x" + extension);
            _y = new ChannelLogger(filename + "y" + extension);
        }

        public void LogInstruction(InstructionCNC instruction)
        {
            var axes = instruction as Axes;
            if (axes == null)
                throw new NotImplementedException();

            _u.LoadInstruction(axes.InstructionU);
            _v.LoadInstruction(axes.InstructionV);
            _x.LoadInstruction(axes.InstructionX);
            _y.LoadInstruction(axes.InstructionY);

            while (_u.IsActive || _v.IsActive || _x.IsActive || _y.IsActive)
            {
                var minActivation = int.MaxValue;

                if (_u.IsActive)
                    minActivation = Math.Min(minActivation, _u.NextActivation);

                if (_v.IsActive)
                    minActivation = Math.Min(minActivation, _v.NextActivation);

                if (_x.IsActive)
                    minActivation = Math.Min(minActivation, _x.NextActivation);

                if (_y.IsActive)
                    minActivation = Math.Min(minActivation, _y.NextActivation);

                //TODO to be precise, change dir should be considered
                _u.TryStep(minActivation);
                _v.TryStep(minActivation);
                _x.TryStep(minActivation);
                _y.TryStep(minActivation);
            }
        }

        internal void LogMessage(string message)
        {
            _u.LogMessage(message);
            _v.LogMessage(message);
            _x.LogMessage(message);
            _y.LogMessage(message);
        }

        public void Flush()
        {
            _u.Flush();
            _v.Flush();
            _x.Flush();
            _y.Flush();
        }
    }
}
