using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

using System.IO.Ports;
using System.Threading;

using System.Diagnostics;

using ControllerCNC.Planning;
using ControllerCNC.Primitives;

namespace ControllerCNC.Machine
{
    delegate void DataReceivedHandler(string data);

    class DriverCNC
    {
        /// <summary>
        /// Port where we will communicate with the machine.
        /// </summary>
        private readonly SerialPort _port;

        /// <summary>
        /// What is maximum number of incomplete plans that can be 
        /// sent to machine.
        /// </summary>
        internal static readonly uint MaxIncompletePlanCount = 7;

        /// <summary>
        /// Length of the instruction sent to machine.
        /// </summary>
        private readonly int _instructionLength = 59;

        /// <summary>
        /// Queue waiting for sending.
        /// </summary>
        private readonly Queue<byte[]> _sendQueue = new Queue<byte[]>();

        /// <summary>
        /// Buffer for instruction which is currently construted.
        /// </summary>
        private readonly List<byte> _constructedInstruction = new List<byte>();

        /// <summary>
        /// Lock for send queue synchornization.
        /// </summary>
        private readonly object _L_sendQueue = new object();

        /// <summary>
        /// Lock for message confirmation flag.
        /// </summary>
        private readonly object _L_confirmation = new object();

        /// <summary>
        /// Lock for counter of incomplete plans.
        /// </summary>
        private readonly object _L_planCompletition = new object();

        /// <summary>
        /// Flag determining whether confirmation from machine is expected.
        /// </summary>
        private volatile bool _expectsConfirmation;

        /// <summary>
        /// Determine whether comment is expected as incomming string.
        /// </summary>
        private volatile bool _isCommentEnabled;

        /// <summary>
        /// How many plans are not complete yet.
        /// </summary>
        private volatile int _incompletePlans;

        /// <summary>
        /// Worker that handles communication with the machine.
        /// </summary>
        private readonly Thread _communicationWorker;

        /// <summary>
        /// Handler that can be used for observing of received data.
        /// </summary>
        public event DataReceivedHandler OnDataReceived;

        /// <summary>
        /// How many from sent plans was not completed
        /// </summary>
        public int IncompletePlanCount { get { return _incompletePlans + _sendQueue.Count; } }

        public DriverCNC()
        {
            //1250,625,4799,4000000
            //var plan = new AccelerationProfile(1250, 625, 4799, 4000000);
            //var acc=new AccelerationProfile(105737.12634405641, 1398, 1431, 4000000);
            //var dec=new AccelerationProfile(-105737.12634405641,int.MaxValue,1431,4000000);

            _port = new SerialPort("COM27");
            _port.BaudRate = 128000;
            _port.Open();

            _port.DataReceived += _port_DataReceived;

            _communicationWorker = new Thread(SERIAL_worker);
            _communicationWorker.IsBackground = true;
            _communicationWorker.Priority = ThreadPriority.Highest;

            _communicationWorker.Start();
        }

        internal void Initialize()
        {
            //read initial garbage
            while (_port.BytesToRead > 0)
            {
                var data = readData(_port);
                if (OnDataReceived != null)
                    OnDataReceived(data);
            }
        }

        /// <summary>
        /// Sends parts of the given plan.
        /// </summary>
        /// <param name="plan">Plan which parts will be executed.</param>
        public void SEND(IEnumerable<InstructionCNC> plan)
        {
            foreach (var part in plan)
            {
                SEND(part);
            }
        }

        /// <summary>
        /// Sends a given part to the machine.
        /// </summary>
        /// <param name="part">Part to send.</param>
        public void SEND(InstructionCNC part)
        {
            throw new NotImplementedException();
        }

        #region Sending utilities

        private void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = readData(_port);
            for (var i = 0; i < data.Length; ++i)
            {
                var response = data[i];
                if (_isCommentEnabled)
                {
                    if (response == '\n')
                        _isCommentEnabled = false;

                    continue;
                }
                switch (response)
                {
                    case 'I':
                        lock (_L_confirmation)
                        {
                            _expectsConfirmation = false;
                            Monitor.Pulse(_L_confirmation);
                        }

                        lock (_L_planCompletition)
                        {
                            _incompletePlans = 0;
                            Monitor.Pulse(_L_planCompletition);
                        }
                        break;
                    case 'Y':
                        lock (_L_confirmation)
                        {
                            _expectsConfirmation = false;
                            Monitor.Pulse(_L_confirmation);
                        }
                        break;
                    case 'F':
                        lock (_L_planCompletition)
                        {
                            --_incompletePlans;
                            Monitor.Pulse(_L_planCompletition);
                        }
                        break;
                    case 'S':
                        //scheduler was enabled
                        break;
                    case 'M':
                        //step time was missed
                        break;
                    case 'E':
                        throw new NotImplementedException("Incomplete message erased");
                    case '|':
                        _isCommentEnabled = true;
                        break;

                    default:
                        // throw new NotImplementedException();
                        break;
                }
            }


            if (OnDataReceived != null)
                OnDataReceived(data);
        }

        private void SERIAL_worker()
        {
            //welcome message
            send(new InitializationInstruction());

            for (; ; )
            {
                byte[] data;
                lock (_L_sendQueue)
                {
                    while (_sendQueue.Count == 0)
                        Monitor.Wait(_L_sendQueue);

                    data = _sendQueue.Dequeue();
                }

                lock (_L_planCompletition)
                {
                    while (_incompletePlans >= MaxIncompletePlanCount)
                        Monitor.Wait(_L_planCompletition);

                    _incompletePlans += 1;
                }

                lock (_L_confirmation)
                {
                    _port.Write(data, 0, data.Length);

                    _expectsConfirmation = true;
                    while (_expectsConfirmation)
                        Monitor.Wait(_L_confirmation);
                }
            }
        }

        private static string readData(SerialPort port)
        {
            var incommingBuffer = new byte[4096];
            var incommingSize = port.Read(incommingBuffer, 0, incommingBuffer.Length);
            var incommingData = Encoding.ASCII.GetString(incommingBuffer, 0, incommingSize);
            return incommingData;
        }

        private void send(InstructionCNC instruction)
        {
            var data = new List<byte>(instruction.GetInstructionBytes());
            while (data.Count < _instructionLength - 2)
                data.Add(0);

            //checksum is used for error detection
            Int16 checksum = 0;
            foreach (var b in data)
            {
                checksum += b;
            }

            data.AddRange(InstructionCNC.ToBytes(checksum));
            var dataBuffer = data.ToArray();
            lock (_L_sendQueue)
            {
                _sendQueue.Enqueue(dataBuffer);
                Monitor.Pulse(_L_sendQueue);
            }
        }

        #endregion
    }
}
