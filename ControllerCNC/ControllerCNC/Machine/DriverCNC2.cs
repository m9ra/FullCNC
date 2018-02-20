using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerCNC.Machine
{
    public class DriverCNC2
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
        /// Worker thread handling communication routines.
        /// </summary>
        private readonly Thread _communicationWorker;

        /// <summary>
        /// Thread estimationg actual position (via known timing profiles).
        /// </summary>
        private Thread _positionEstimator;

        /// <summary>
        /// Whether instruction receive confirmation is expected.
        /// </summary>
        private volatile bool _expectsConfirmation = false;

        /// <summary>
        /// Whether lastly send instruction should be resend.
        /// </summary>
        private volatile bool _resendIsNeeded = false;

        /// <summary>
        /// Lock for send synchornization.
        /// </summary>
        private readonly object _L_send = new object();

        /// <summary>
        /// Lock for instruction synchronization.
        /// </summary>
        private readonly object _L_instructionQueue = new object();

        /// <summary>
        /// Queue of byts waiting for sending.
        /// </summary>
        private readonly Queue<byte[]> _sendQueue = new Queue<byte[]>();

        /// <summary>
        /// Queue of instructions that are in progress on machine (from send to completition confirmation - after that steps from the instruction still can be executed).
        /// </summary>
        private readonly Queue<InstructionCNC> _bufferedInstructions = new Queue<InstructionCNC>();

        /// <summary>
        /// Parse for the incomming communication stream.
        /// </summary>
        private readonly CommunicationParser _parser = new CommunicationParser();

        /// <summary>
        /// State which was already completed by machine.
        /// </summary>
        private StateInfo _currentState;

        /// <summary>
        /// State which will be achieved after completing all planned instructions.
        /// </summary>
        private StateInfo _plannedState;

        /// <summary>
        /// Determine connection status.
        /// </summary>
        private volatile bool _isConnected;

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
        /// When last finished instruction was encountered.
        /// </summary>
        private DateTime _lastInstructionStartTime;

        /// <summary>
        /// Delay between finish and next instruction fetch.
        /// </summary>
        private readonly double _startDelay = 0.001;

        /// <summary>
        /// Ration between machine and real clock.
        /// </summary>
        private readonly double _clockRatio = 1.0 / 1.004;

        /// <summary>
        /// How many ticks were made during estimation.
        /// </summary>
        private ulong _tickEstimation;

        /// <summary>
        /// Determine whether machine is connected.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
            set
            {
                if (_isConnected == value)
                    return;

                _isConnected = value;
                OnConnectionStatusChange?.Invoke();
            }
        }

        /// <summary>
        /// Handler that can be used for received data observation.
        /// </summary>
        public event DataReceivedHandler OnDataReceived;

        /// <summary>
        /// Event fired when connection status change (between connected/disconnected).
        /// </summary>
        public event Action OnConnectionStatusChange;

        /// <summary>
        /// Event fired when homing result arrived.
        /// </summary>
        public event Action OnHomeCalibrated;

        /// <summary>
        /// Event fired when all instructions have been confirmed.
        /// </summary>
        public event Action OnInstructionQueueIsComplete;

        /// <summary>
        /// How many instructions sent is not processed yet.
        /// </summary>
        public int IncompleteInstructionCount { get { lock (_L_instructionQueue) return _bufferedInstructions.Count; } }

        /// <summary>
        /// State which was already completed by the machine.
        /// </summary>
        public StateInfo CurrentState { get { lock (_L_instructionQueue) return _currentState.Copy(); } }

        /// <summary>
        /// State which will be achieved after confirming all planned instructions.
        /// </summary>
        public StateInfo PlannedState { get { lock (_L_instructionQueue) return _plannedState.Copy(); } }

        /// <summary>
        /// Determine whether last homing attempt was successful.
        /// </summary>
        public bool IsHomeCalibrated { get { lock (_L_instructionQueue) return CurrentState.IsHomeCalibrated || FAKE_ONLINE_MODE; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationU { get { return CurrentState.U + _uEstimation; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationV { get { return CurrentState.V + _vEstimation; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationX { get { return CurrentState.X + _xEstimation; } }

        /// <summary>
        /// Position estimation.
        /// </summary>
        public int EstimationY { get { return CurrentState.Y + _yEstimation; } }

        public DriverCNC2()
        {
            _plannedState = new StateInfo();
            _currentState = new StateInfo();

            _currentState.Initialize();
            _plannedState = _currentState.Copy();

            _positionEstimator = new Thread(runPositionEstimation);
            _positionEstimator.IsBackground = true;
            _positionEstimator.Priority = ThreadPriority.Lowest;

            _communicationWorker = new Thread(SERIAL_worker);
            _communicationWorker.IsBackground = true;
            _communicationWorker.Priority = ThreadPriority.Highest;
        }

        /// <summary>
        /// Checks whether plan fits the workspace.
        /// </summary>
        public bool Check(IEnumerable<InstructionCNC> plan)
        {
            var testState = _plannedState.Copy();
            foreach (var instruction in plan)
            {
                if (!checkBoundaries(instruction, ref testState))
                    //atomic behaviour for whole plan
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Sends parts of the given plan.
        /// </summary>
        /// <param name="plan">Plan which parts will be executed.</param>
        public bool SEND(IEnumerable<InstructionCNC> plan)
        {
            if (!Check(plan))
                return false;

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
        /// <param name="instruction">Part to send.</param>
        public bool SEND(InstructionCNC instruction)
        {
            if (instruction.IsEmpty)
                return true;

            var testState = _plannedState.Copy();
            if (!checkBoundaries(instruction, ref testState))
                return false;

            _plannedState = testState;

            send(instruction);
            return true;
        }

        public void Initialize()
        {
            System.Diagnostics.Process myProcess = System.Diagnostics.Process.GetCurrentProcess();
            myProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.RealTime;

            _positionEstimator.Start();
            _communicationWorker.Start();
        }

        private bool checkBoundaries(InstructionCNC instruction, ref StateInfo state)
        {
            state.Accept(instruction);
            return state.CheckBoundaries();
        }

        private void SERIAL_worker()
        {
            if (FAKE_ONLINE_MODE)
                runCncSimulator();

            for (; ; )
            {
                //loop forever and try to establish connection

                var port = tryConnect();
                if (port == null)
                {
                    //connection was not successful
                    Thread.Sleep(1000);
                    continue;
                }

                var repeatCommunication = false;

                //welcome message
                send(new InitializationInstruction());
                do
                {
                    try
                    {
                        //the communication handler can return only by throwing an exception
                        communicate(port);
                    }
                    catch (ThreadAbortException)
                    {
                        //abort exception is used for fast state reinitialization
                        repeatCommunication = true;
                        Thread.ResetAbort();
                    }
                    catch (Exception)
                    {
                        //connection exception might occur
                        repeatCommunication = false;
                    }
                } while (repeatCommunication);

                IsConnected = false;
            }
        }

        #region Communication handling

        private void communicate(SerialPort port)
        {
            for (; ; )
            {
                byte[] data;
                lock (_L_instructionQueue)
                {
                    while (_sendQueue.Count == 0)
                    {
                        // wait until some work arrives
                        Monitor.Wait(_L_instructionQueue);
                    }

                    //System.Diagnostics.Debug.WriteLine(_bufferedInstructions.Peek());
                    data = _sendQueue.Dequeue();
                }

                lock (_L_send)
                {
                    do //loop until data is sent
                    {
                        _resendIsNeeded = false;
                        port.Write(data, 0, data.Length);

                        _expectsConfirmation = true;
                        while (_expectsConfirmation)
                        {
                            //wait until confirmation arrives
                            Monitor.Wait(_L_send);
                        }
                    } while (_resendIsNeeded);
                }
            }
        }

        private SerialPort tryConnect()
        {
            var portName = getCncPortName();
            if (portName == null)
                return null;

            //try to connect
            SerialPort port;
            try
            {
                port = new SerialPort(portName);
                port.DtrEnable = false;
                port.RtsEnable = false;
                port.BaudRate = Configuration.CommunicationBaudRate;
                port.Open();

                port.DataReceived += (s, e) => _port_DataReceived(port, s, e);
            }
            catch (Exception)
            {
                //connection was not successful
                return null;
            }

            //read initial garbage
            while (port.BytesToRead > 0)
                readData(port);

            return port;
        }

        private void _port_DataReceived(SerialPort port, object sender, SerialDataReceivedEventArgs e)
        {
            var data = readData(port);
            foreach (var dataByte in data)
            {
                _parser.Add(dataByte);
                if (!_parser.IsResponseFrameComplete)
                    continue;

                //System.Diagnostics.Debug.WriteLine(_parser.Response);

                switch (_parser.Response)
                {
                    case MachineResponse.IsOnline:
                        //there is nothing to do
                        break;

                    case MachineResponse.RequiresAuthentication:
                        authenticate();
                        break;

                    case MachineResponse.Welcome:
                        runInitialization();
                        break;

                    case MachineResponse.SchedulerWasEnabled:
                        System.Diagnostics.Debug.WriteLine("S: " + IncompleteInstructionCount);
                        break;

                    case MachineResponse.HomingCompleted:
                        homingCompleted();
                        break;

                    case MachineResponse.StateData:
                        setState(_parser.MachineStateBuffer);
                        instructionCompleted();
                        break;

                    case MachineResponse.ConfirmationReceived:
                        confirmationReceived();
                        break;

                    case MachineResponse.FullInstructionBuffer:
                        fullBuffer();
                        break;

                    case MachineResponse.InstructionFinished:
                        instructionCompleted();
                        break;

                    case MachineResponse.InvalidChecksum:
                    case MachineResponse.IncompleteDataErased:
                        System.Diagnostics.Debug.Write(Encoding.ASCII.GetString(data));
                        badDataReceived();
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine("Unprocessed command: " + _parser.Response);
                        break;
                }
            }
        }

        private byte[] readData(SerialPort port)
        {
            var incommingBuffer = new byte[4096];
            var incommingSize = port.Read(incommingBuffer, 0, incommingBuffer.Length);
            var receivedData = incommingBuffer.Take(incommingSize).ToArray();

            OnDataReceived?.Invoke(Encoding.ASCII.GetString(receivedData));
            return receivedData;
        }

        private void send(InstructionCNC instruction)
        {
            var data = new List<byte>(instruction.GetInstructionBytes());
            while (data.Count < Configuration.InstructionLength - 2)
                data.Add(0);

            if (data.Count != Configuration.InstructionLength - 2)
                throw new NotSupportedException("Invalid instruction length detected.");

            //checksum is used for error detection
            Int16 checksum = 0;
            foreach (var b in data)
                checksum += b;

            data.AddRange(InstructionCNC.ToBytes(checksum));
            var dataBuffer = data.ToArray();

            lock (_L_instructionQueue)
            {
                _bufferedInstructions.Enqueue(instruction);
                _sendQueue.Enqueue(dataBuffer);
                Monitor.Pulse(_L_instructionQueue);
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

        #endregion

        #region Response callbacks

        private void clearDriverState()
        {
            lock (_L_instructionQueue)
            {
                System.Diagnostics.Debug.WriteLine("Clearing driver state");
                _expectsConfirmation = false;
                _resendIsNeeded = false;

                _sendQueue.Clear();
                _bufferedInstructions.Clear();
                _communicationWorker.Abort();

                Monitor.Pulse(_L_instructionQueue);
            }
        }

        private void authenticate()
        {
            lock (_L_instructionQueue)
            {
                clearDriverState();

                _sendQueue.Enqueue(Encoding.ASCII.GetBytes("$%#"));
                Monitor.Pulse(_L_instructionQueue);
            }
        }

        private void runInitialization()
        {
            instructionCompleted();
            reportConfirmation();
            send(new StateDataInstruction());
            IsConnected = true;
        }

        private void homingCompleted()
        {
            instructionCompleted();
            reportConfirmation();
            OnHomeCalibrated?.Invoke();
        }

        private void instructionCompleted()
        {
            InstructionCNC instruction;
            lock (_L_instructionQueue)
            {
                if (_bufferedInstructions.Count == 0)
                    //probably garbage from buffer
                    return;

                instruction = _bufferedInstructions.Dequeue();
                _lastInstructionStartTime = DateTime.Now;
                _uEstimation = _vEstimation = _xEstimation = _yEstimation = 0;

                _currentState.Accept(instruction);

                if (_bufferedInstructions.Count == 0)
                    OnInstructionQueueIsComplete?.Invoke();

                Monitor.Pulse(_L_instructionQueue);
            }
        }

        private void fullBuffer()
        {
            lock (_L_send)
            {
                _resendIsNeeded = true;
                _expectsConfirmation = false;
                Monitor.Pulse(_L_send);
            }
        }

        private void badDataReceived()
        {
            lock (_L_send)
            {
                _resendIsNeeded = true;
                _expectsConfirmation = false;
                Monitor.Pulse(_L_send);
            }
        }

        private void reportConfirmation()
        {
            lock (_L_send)
            {
                _expectsConfirmation = false;
                Monitor.Pulse(_L_send);
            }
        }

        private void confirmationReceived()
        {
            reportConfirmation();
        }

        private void setState(byte[] machineStateBuffer)
        {
            lock (_L_instructionQueue)
            {
                _currentState.SetState(machineStateBuffer);
                _plannedState = _currentState.Copy();
                Monitor.Pulse(_L_instructionQueue);
            }

            reportConfirmation();
        }

        #endregion

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
                Axes currentInstruction;
                lock (_L_instructionQueue)
                {
                    if (_bufferedInstructions.Count == 0)
                        continue;

                    currentInstruction = _bufferedInstructions.Peek() as Axes;
                }
                if (currentInstruction == null)
                    continue;

                uEstimator.RegisterInstruction(currentInstruction.InstructionU);
                vEstimator.RegisterInstruction(currentInstruction.InstructionV);
                xEstimator.RegisterInstruction(currentInstruction.InstructionX);
                yEstimator.RegisterInstruction(currentInstruction.InstructionY);

                var realTargetTime = (DateTime.Now - _lastInstructionStartTime).TotalSeconds;
                var targetTime = realTargetTime * _clockRatio + _startDelay;
                var targetTicks = (long)Math.Round(targetTime * Configuration.TimerFrequency);

                var uSteps = uEstimator.GetSteps(targetTicks);
                var vSteps = vEstimator.GetSteps(targetTicks);
                var xSteps = xEstimator.GetSteps(targetTicks);
                var ySteps = yEstimator.GetSteps(targetTicks);

                lock (_L_instructionQueue)
                {
                    if (_bufferedInstructions.Count == 0 || _bufferedInstructions.Peek() != currentInstruction)
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

        #region Simulator

        private void runCncSimulator()
        {
            IsConnected = true;
            _plannedState.Accept(new HomingInstruction());
            _currentState.Accept(new HomingInstruction());

            var simulationDelay = 10;
            while (true)
            {
                Thread.Sleep(simulationDelay);

                if (_bufferedInstructions.Count > 0)
                {
                    InstructionCNC instruction;
                    lock (_L_instructionQueue)
                    {
                        _lastInstructionStartTime = DateTime.Now;
                        instruction = _bufferedInstructions.Peek();
                    }

                    var tickCount = instruction.CalculateTotalTime();
                    var time = 1000.0 * tickCount / Configuration.TimerFrequency;

                    if (SIMULATE_REAL_DELAY)
                    {
                        Thread.Sleep((int)Math.Max(0, (long)time - (long)simulationDelay));
                    }
                    else
                    {
                        Thread.Sleep(200);
                    }

                    lock (_L_instructionQueue)
                    {
                        instructionCompleted();

                        if (_bufferedInstructions.Count == 0 && OnInstructionQueueIsComplete != null)
                            OnInstructionQueueIsComplete();
                    }
                }
            }
        }

        #endregion
    }
}
