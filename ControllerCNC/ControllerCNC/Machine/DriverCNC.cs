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
    public delegate void DataReceivedHandler(string data);

    public class DriverCNC
    {
        /// <summary>
        /// If set to true, simulation mode is used instead of the real device.
        /// </summary>
        private readonly bool FAKE_ONLINE_MODE = false;

        /// <summary>
        /// Determine whether simulator should use real speeds.
        /// </summary>
        private readonly bool SIMULATE_REAL_DELAY = true;

        /// <summary>
        /// Port where we will communicate with the machine.
        /// </summary>
        private SerialPort _port;

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
        /// Delay between finish and next instruction fetch.
        /// </summary>
        private readonly double _startDelay = 0.001;

        /// <summary>
        /// Ration between machine and real clock.
        /// </summary>
        private readonly double _clockRatio = 1.0 / 1.004;

        /// <summary>
        /// Baud rate for serial communication.
        /// </summary>
        private readonly int _communicationBaudRate = 128000;

        /// <summary>
        /// Queue waiting for sending.
        /// </summary>
        private readonly Queue<byte[]> _sendQueue = new Queue<byte[]>();

        /// <summary>
        /// Queue of inComplete instructions (parallel to send queue);
        /// </summary>
        private readonly Queue<InstructionCNC> _incompleteInstructionQueue = new Queue<InstructionCNC>();

        /// <summary>
        /// State which was already completed by machine.
        /// </summary>
        private StateInfo _completedState;

        /// <summary>
        /// State which will be achieved after completing all planned instructions.
        /// </summary>
        private StateInfo _plannedState;

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
        private readonly object _L_instructionCompletition = new object();

        /// <summary>
        /// Flag determining whether confirmation from machine is expected.
        /// </summary>
        private volatile bool _expectsConfirmation;

        /// <summary>
        /// Determine whether comment is expected as incomming string.
        /// </summary>
        private volatile bool _isCommentEnabled;

        /// <summary>
        /// Storage for incomming state data.
        /// </summary>
        private byte[] _stateDataBuffer = new byte[1 + 4 * 4];

        /// <summary>
        /// How many bytes we need to obtain.
        /// </summary>
        private int _stateDataRemainingBytes = 0;

        /// <summary>
        /// How many ticks was made during estimation.
        /// </summary>
        private ulong _tickEstimation;

        /// <summary>
        /// How many steps is estimated from last instruction completion.
        /// </summary>
        private volatile int _uEstimation;

        /// <summary>
        /// How many steps is estimated from last instruction completion.
        /// </summary>
        private volatile int _vEstimation;

        /// <summary>
        /// How many steps is estimated from last instruction completion.
        /// </summary>
        private volatile int _xEstimation;

        /// <summary>
        /// How many steps is estimated from last instruction completion.
        /// </summary>
        private volatile int _yEstimation;

        /// <summary>
        /// How many plans are not complete yet.
        /// </summary>
        private volatile int _incompleteInstructions;

        /// <summary>
        /// Determine whether connection needs to be reset.
        /// </summary>
        private volatile bool _resetConnection;

        /// <summary>
        /// When last finished instruction was encountered.
        /// </summary>
        private DateTime _lastInstructionStartTime;

        /// <summary>
        /// Worker that handles communication with the machine.
        /// </summary>
        private Thread _communicationWorker;

        /// <summary>
        /// Thread estimationg actual position (via known timing profiles).
        /// </summary>
        private Thread _positionEstimator;

        /// <summary>
        /// State which was already completed by the machine.
        /// </summary>
        public StateInfo CompletedState { get { return _completedState.Copy(); } }

        /// <summary>
        /// State which will be achieved after confirming all planned instructions.
        /// </summary>
        public StateInfo PlannedState { get { return _plannedState.Copy(); } }

        /// <summary>
        /// Handler that can be used for observing of received data.
        /// </summary>
        public event DataReceivedHandler OnDataReceived;

        /// <summary>
        /// Event fired when connection status change (between connected/disconnected).
        /// </summary>
        public event Action OnConnectionStatusChange;

        /// <summary>
        /// Event fired when homing result arrived.
        /// </summary>
        public event Action OnHomingEnded;

        /// <summary>
        /// Event fired when all instructions have been confirmed.
        /// </summary>
        public event Action OnInstructionQueueIsComplete;

        /// <summary>
        /// How many from sent plans was not completed
        /// </summary>
        public int IncompletePlanCount { get { return _incompleteInstructions + _sendQueue.Count; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public ulong EstimationTicks { get { return CompletedState.TickCount + _tickEstimation; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationU { get { return CompletedState.U + _uEstimation; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationV { get { return CompletedState.V + _vEstimation; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationX { get { return CompletedState.X + _xEstimation; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationY { get { return CompletedState.Y + _yEstimation; } }

        /// <summary>
        /// Determine whether machine is connected.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Determine whether last homing attempt was successful.
        /// </summary>
        public bool IsHomeCalibrated { get { return CompletedState.IsHomeCalibrated || FAKE_ONLINE_MODE; } }

        public DriverCNC()
        {
            _positionEstimator = new Thread(runPositionEstimation);
            _positionEstimator.IsBackground = true;
            _positionEstimator.Priority = ThreadPriority.Lowest;

            _communicationWorker = new Thread(SERIAL_worker);
            _communicationWorker.IsBackground = true;
            _communicationWorker.Priority = ThreadPriority.Highest;
        }

        public void Initialize()
        {
            System.Diagnostics.Process myProcess = System.Diagnostics.Process.GetCurrentProcess();
            myProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.RealTime;

            _positionEstimator.Start();
            _communicationWorker.Start();
        }

        /// <summary>
        /// Sends parts of the given plan.
        /// </summary>
        /// <param name="plan">Plan which parts will be executed.</param>
        public bool SEND(IEnumerable<InstructionCNC> plan)
        {
            var testState = _plannedState.Copy();
            foreach (var instruction in plan)
            {
                if (!checkBoundaries(instruction, ref testState))
                    //atomic behaviour for whole plan
                    return false;
            }

            foreach (var instruction in plan)
            {
                if (!SEND(instruction))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Sends a given part to the machine.
        /// </summary>
        /// <param name="part">Part to send.</param>
        public bool SEND(InstructionCNC part)
        {
            var testState = _plannedState.Copy();
            if (!checkBoundaries(part, ref testState))
                return false;

            _plannedState = testState;

            send(part);
            return true;
        }

        #region Position estimation utilities

        private void runPositionEstimation()
        {
            var uEstimator = new PositionEstimator();
            var vEstimator = new PositionEstimator();
            var xEstimator = new PositionEstimator();
            var yEstimator = new PositionEstimator();

            while (true)
            {
                Thread.Sleep(20);
                if (_incompleteInstructionQueue.Count == 0)
                    continue;

                var currentInstruction = _incompleteInstructionQueue.Peek() as Axes;
                if (currentInstruction == null)
                    continue;

                uEstimator.RegisterInstruction(currentInstruction.InstructionU);
                vEstimator.RegisterInstruction(currentInstruction.InstructionV);
                xEstimator.RegisterInstruction(currentInstruction.InstructionX);
                yEstimator.RegisterInstruction(currentInstruction.InstructionY);

                var realTargetTime = (DateTime.Now - _lastInstructionStartTime).TotalSeconds;
                var targetTime = realTargetTime * _clockRatio + _startDelay;
                var targetTicks = (long)Math.Round(targetTime * Constants.TimerFrequency);

                var uSteps = uEstimator.GetSteps(targetTicks);
                var vSteps = vEstimator.GetSteps(targetTicks);
                var xSteps = xEstimator.GetSteps(targetTicks);
                var ySteps = yEstimator.GetSteps(targetTicks);

                lock (_L_instructionCompletition)
                {
                    if (_incompleteInstructionQueue.Count == 0 || _incompleteInstructionQueue.Peek() != currentInstruction)
                        //we missed the instruction
                        continue;

                    _uEstimation += uSteps;
                    _vEstimation += vSteps;
                    _xEstimation += xSteps;
                    _yEstimation += ySteps;
                    if (targetTicks > 0)
                        _tickEstimation = (ulong)targetTicks;
                }
            }
        }

        #endregion

        #region Communication utilities

        private void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = readData(_port);
            foreach (var dataByte in data)
            {
                var response = (char)dataByte;
                if (_stateDataRemainingBytes > 0)
                {
                    _stateDataBuffer[_stateDataBuffer.Length - _stateDataRemainingBytes] = (byte)response;
                    --_stateDataRemainingBytes;

                    if (_stateDataRemainingBytes == 0)
                    {
                        lock (_L_instructionCompletition)
                        {
                            _incompleteInstructionQueue.Dequeue();
                            _completedState.SetState(_stateDataBuffer);
                            --_incompleteInstructions;

                            if (_incompleteInstructionQueue.Count > 0)
                                throw new NotSupportedException("State retrieval requires empty queue");
                            _plannedState = _completedState.Copy();

                            Monitor.Pulse(_L_instructionCompletition);
                        }
                    }

                    continue;
                }

                if (_isCommentEnabled)
                {
                    if (response == '\n')
                        _isCommentEnabled = false;

                    continue;
                }
                switch (response)
                {
                    case '1':
                        //_resetConnection = true;
                        clearDriverState();
                        break;
                    case 'I':
                        //first reset plans - the driver is blocked on expects confirmation
                        clearDriverState();
                        send(new StateDataInstruction());
                        break;

                    case 'D':
                        lock (_L_confirmation)
                        {
                            _expectsConfirmation = false;
                            Monitor.Pulse(_L_confirmation);
                            _stateDataRemainingBytes = _stateDataBuffer.Length;
                        }

                        break;
                    case 'H':

                        lock (_L_confirmation)
                        {
                            _expectsConfirmation = false;
                            Monitor.Pulse(_L_confirmation);
                        }

                        lock (_L_instructionCompletition)
                        {
                            onInstructionCompleted();
                            Monitor.Pulse(_L_instructionCompletition);
                        }

                        //homing was finished successfuly
                        OnHomingEnded?.Invoke();

                        break;
                    case 'Y':
                        lock (_L_confirmation)
                        {
                            _expectsConfirmation = false;
                            Monitor.Pulse(_L_confirmation);
                        }
                        break;
                    case 'F':
                        var incompleteInstrutionCount = 0;
                        lock (_L_instructionCompletition)
                        {
                            onInstructionCompleted();
                            incompleteInstrutionCount = _incompleteInstructionQueue.Count;
                            Monitor.Pulse(_L_instructionCompletition);
                        }

                        if (incompleteInstrutionCount == 0 && OnInstructionQueueIsComplete != null)
                            OnInstructionQueueIsComplete();

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


            OnDataReceived?.Invoke(Encoding.ASCII.GetString(data.ToArray()));
        }

        /// <summary>
        /// HAS TO BE CALLED WITH _L_instrutionCompletition
        /// </summary>
        private void onInstructionCompleted()
        {
            var now = DateTime.Now;
            _lastInstructionStartTime = now;
            _uEstimation = _vEstimation = _xEstimation = _yEstimation = 0;

            if (_incompleteInstructionQueue.Count == 0)
                //TODO probably garbage from buffer
                return;

            var instruction = _incompleteInstructionQueue.Dequeue();
            _completedState.Completed(instruction);
            --_incompleteInstructions;
        }

        private bool checkBoundaries(InstructionCNC instruction, ref StateInfo state)
        {
            state.Completed(instruction);
            return state.CheckBoundaries();
        }

        private void clearDriverState()
        {
            lock (_L_instructionCompletition)
            {
                _sendQueue.Clear();
                _incompleteInstructionQueue.Clear();
                _incompleteInstructions = 0;
                Monitor.Pulse(_L_instructionCompletition);
            }

            lock (_L_confirmation)
            {
                if (!_expectsConfirmation)
                    throw new NotSupportedException("Race condition threat");

                _stateDataRemainingBytes = 0;
                _expectsConfirmation = false;
                Monitor.Pulse(_L_confirmation);
            }
        }

        private void SERIAL_worker()
        {
            if (FAKE_ONLINE_MODE)
                runCncSimulator();

            for (; ; )
            {
                //loop forever and try to establish connection

                var portName = getCncPortName();
                if (portName != null)
                {
                    //try to connect
                    try
                    {
                        _port = new SerialPort(portName);
                        _port.DtrEnable = false;
                        _port.RtsEnable = false;
                        _port.BaudRate = _communicationBaudRate;
                        _port.Open();

                        _port.DataReceived += _port_DataReceived;
                    }
                    catch (Exception)
                    {
                        //connection was not successful
                        _port = null;
                        continue;
                    }

                    //read initial garbage
                    while (_port.BytesToRead > 0)
                    {
                        var data = readData(_port);
                        OnDataReceived?.Invoke(Encoding.ASCII.GetString(data.ToArray()));
                    }

                    IsConnected = true;
                    fireOnConnectionStatusChange();

                    try
                    {
                        //the communication handler can return only by throwing exception
                        communicate();
                    }
                    catch (Exception)
                    {
                        //disconnection appeared                     
                    }
                    finally
                    {
                        _port = null;
                        IsConnected = false;
                        fireOnConnectionStatusChange();
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private void communicate()
        {
            _resetConnection = false;
            //welcome message
            send(new InitializationInstruction());

            for (; ; )
            {
                byte[] data;
                lock (_L_sendQueue)
                {
                    while (_sendQueue.Count == 0)
                    {
                        if (_resetConnection)
                            throw new NotSupportedException("Invalid state");
                        Monitor.Wait(_L_sendQueue);
                    }

                    data = _sendQueue.Dequeue();
                }

                lock (_L_instructionCompletition)
                {
                    while (_incompleteInstructions >= MaxIncompletePlanCount)
                        Monitor.Wait(_L_instructionCompletition);

                    if (_incompleteInstructions == 0)
                        _lastInstructionStartTime = DateTime.Now;

                    _incompleteInstructions += 1;
                }

                if (_resetConnection)
                    throw new NotSupportedException("Invalid state");

                lock (_L_confirmation)
                {
                    _port.Write(data, 0, data.Length);

                    _expectsConfirmation = true;
                    while (_expectsConfirmation)
                        Monitor.Wait(_L_confirmation);
                }
            }
        }

        private string getCncPortName()
        {
            var ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
                return null;

            if (ports.Length == 1)
                //TODO the device might be something different we expect
                return ports[0];

            return "COM10";
            //TODO throw new NotImplementedException("Disambiguate ports");
        }

        private IEnumerable<byte> readData(SerialPort port)
        {
            var incommingBuffer = new byte[4096];
            var incommingSize = port.Read(incommingBuffer, 0, incommingBuffer.Length);
            return incommingBuffer.Take(incommingSize);
        }

        private void send(InstructionCNC instruction)
        {
            var data = new List<byte>(instruction.GetInstructionBytes());
            while (data.Count < _instructionLength - 2)
                data.Add(0);

            if (data.Count != _instructionLength - 2)
                throw new NotSupportedException("Invalid instruction length detected.");

            //checksum is used for error detection
            Int16 checksum = 0;
            foreach (var b in data)
            {
                checksum += b;
            }

            data.AddRange(InstructionCNC.ToBytes(checksum));
            var dataBuffer = data.ToArray();

            lock (_L_instructionCompletition)
            {
                _incompleteInstructionQueue.Enqueue(instruction);
            }
            lock (_L_sendQueue)
            {
                _sendQueue.Enqueue(dataBuffer);
                Monitor.Pulse(_L_sendQueue);
            }
        }

        private void fireOnConnectionStatusChange()
        {
            OnConnectionStatusChange?.Invoke();
        }

        private void runCncSimulator()
        {
            IsConnected = true;
            fireOnConnectionStatusChange();
            _plannedState.Completed(new HomingInstruction());
            _completedState.Completed(new HomingInstruction());

            var simulationDelay = 100;
            while (true)
            {
                Thread.Sleep(simulationDelay);

                if (_incompleteInstructionQueue.Count > 0)
                {
                    InstructionCNC instruction;
                    lock (_L_instructionCompletition)
                    {
                        _lastInstructionStartTime = DateTime.Now;
                        instruction = _incompleteInstructionQueue.Peek();
                    }

                    var tickCount = instruction.CalculateTotalTime();
                    var time = 1000 * tickCount / Constants.TimerFrequency;

                    if (SIMULATE_REAL_DELAY)
                    {
                        Thread.Sleep((int)Math.Max(0, (long)time - (long)simulationDelay));
                    }
                    else
                    {
                        Thread.Sleep(200);
                    }

                    lock (_L_instructionCompletition)
                    {
                        onInstructionCompleted();

                        if (_incompleteInstructionQueue.Count == 0 && OnInstructionQueueIsComplete != null)
                            OnInstructionQueueIsComplete();
                    }
                }
            }
        }

        #endregion
    }
}
