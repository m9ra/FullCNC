using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    internal enum MachineResponse { IsOnline, Welcome, StateData, HomingCompleted, ConfirmationReceived, InstructionFinished, SchedulerWasEnabled, IncompleteDataErased, InvalidChecksum, Comment, StepTimeMissed, RequiresAuthentication, FullInstructionBuffer, HomingIsNotAllowed };

    class CommunicationParser
    {
        /// <summary>
        /// Determine whether next response frame is ready to use.
        /// </summary>
        internal bool IsResponseFrameComplete { get; private set; }

        /// <summary>
        /// Currently parsed response.
        /// </summary>
        internal MachineResponse Response { get; private set; }

        /// <summary>
        /// Buffer for machine state.
        /// </summary>
        internal byte[] MachineStateBuffer { get; private set; }

        /// <summary>
        /// Currently built machine state buffer.
        /// </summary>
        private List<byte> _machineStateBuffer = null;

        /// <summary>
        /// Determine whether comment is being skipped.
        /// </summary>
        private bool _isCommentSkipped = false;

        internal void Add(byte data)
        {
            IsResponseFrameComplete = false;

            if (_machineStateBuffer != null)
            {
                //machine state buffer is expected
                _machineStateBuffer.Add(data);

                if (_machineStateBuffer.Count == Configuration.MachineStateBufferSize)
                {
                    MachineStateBuffer = _machineStateBuffer.ToArray();
                    _machineStateBuffer = null;
                    IsResponseFrameComplete = true;
                }

                return;
            }

            var responseChar = (char)data;

            if (_isCommentSkipped)
            {
                if (responseChar == '\n')
                {
                    _isCommentSkipped = false;
                    IsResponseFrameComplete = true;
                }
                //skip everything until end of line
                return;
            }

            IsResponseFrameComplete = true;
            switch (responseChar)
            {
                case '1':
                    Response = MachineResponse.IsOnline;
                    break;

                case 'a':
                    Response = MachineResponse.RequiresAuthentication;
                    break;

                case 'I':
                    Response = MachineResponse.Welcome;
                    break;

                case 'H':
                    Response = MachineResponse.HomingCompleted;
                    break;

                case 'Y':
                    Response = MachineResponse.ConfirmationReceived;
                    break;

                case 'F':
                    Response = MachineResponse.InstructionFinished;
                    break;

                case 'S':
                    Response = MachineResponse.SchedulerWasEnabled;
                    break;

                case 'M':
                    Response = MachineResponse.StepTimeMissed;
                    break;

                case 'E':
                    Response = MachineResponse.IncompleteDataErased;
                    break;

                case 'C':
                    Response = MachineResponse.InvalidChecksum;
                    break;

                case 'O':
                    Response = MachineResponse.FullInstructionBuffer;
                    break;

                case 'Q':
                    Response = MachineResponse.HomingIsNotAllowed;
                    break;

                case 'D':
                    Response = MachineResponse.StateData;
                    _machineStateBuffer = new List<byte>(Configuration.MachineStateBufferSize);
                    IsResponseFrameComplete = false;
                    break;

                case '|':
                    Response = MachineResponse.Comment;
                    _isCommentSkipped = true;
                    IsResponseFrameComplete = false;
                    break;

                default:
                    throw new NotImplementedException("Unknown command: " + responseChar);
            }
        }
    }
}
