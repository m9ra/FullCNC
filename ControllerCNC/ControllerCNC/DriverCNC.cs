using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Ports;
using System.Threading;

using System.Diagnostics;

using ControllerCNC.Primitives;

namespace ControllerCNC
{
    delegate void DataReceivedHandler(string data);

    class DriverCNC
    {
        /// <summary>
        /// What is maximum number of incomplete plans that can be 
        /// sent to machine.
        /// </summary>
        internal static readonly int MaxIncompletePlanCount = 7;

        /// <summary>
        /// Time scale of the machine. (2MHz)
        /// </summary>
        internal static readonly int TimeScale = 2000000;

        /// <summary>
        /// How many steps for single revolution has to be done.
        /// </summary>
        internal static readonly int StepsPerRevolution = 400;

        /// <summary>
        /// Maximal safe acceleration in steps/s^2.
        /// </summary>
        internal static readonly int MaxAcceleration = 400 * StepsPerRevolution;

        /// <summary>
        /// How many steppers will be commanded.
        /// TODO: this is workaround only - new API is required.
        /// </summary>
        internal int StepperIndex = 1;

        /// <summary>
        /// DeltaT which is used for steppers start.
        /// </summary>
        public UInt16 StartDeltaT { get { return 2000; } }

        /// <summary>
        /// Fastest DeltaT which is supported
        /// </summary>
        public UInt16 FastestDeltaT { get { return 200; } }

        /// <summary>
        /// Port where we will communicate with the machine.
        /// </summary>
        private readonly SerialPort _port;

        /// <summary>
        /// Length of the instruction sent to machine.
        /// </summary>
        private readonly int _instructionLength = 36;

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
                    case 'E':
                        throw new NotImplementedException("Incomplete message erased");
                    case '|':
                        _isCommentEnabled = true;
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }


            if (OnDataReceived != null)
                OnDataReceived(data);
        }

        private void SERIAL_worker()
        {
            //welcome message
            send(new[] { (byte)'I' });

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

        internal void SEND_TransitionRPM(int stepCount, int startRPM, int targetRPM, int endRPM)
        {
            var startDeltaT = DeltaTFromRPM(startRPM);
            var targetDeltaT = DeltaTFromRPM(targetRPM);
            var endDeltaT = DeltaTFromRPM(endRPM);

            SEND_Transition(stepCount, startDeltaT, targetDeltaT, endDeltaT);
        }

        public void SEND_Transition(int stepCount, int startDeltaT, int targetDeltaT, int endDeltaT)
        {
            if (startDeltaT > UInt16.MaxValue || targetDeltaT > UInt16.MaxValue || endDeltaT > UInt16.MaxValue)
                throw new NotImplementedException("split");

            if (startDeltaT < 1 || targetDeltaT < 1 || endDeltaT < 1)
                throw new NotSupportedException();

            var startDeltaTI = (UInt16)startDeltaT;
            var targetDeltaTI = (UInt16)targetDeltaT;
            var endDeltaTI = (UInt16)endDeltaT;

            var maxAccelerationDistance = GetStepSlice(stepCount / 2);
            var acceleration1 = CalculateBoundedAcceleration(startDeltaTI, targetDeltaTI, maxAccelerationDistance);
            var acceleration2 = CalculateBoundedAcceleration(acceleration1.EndDeltaT, endDeltaTI, maxAccelerationDistance);

            var constantTrackSteps = stepCount - acceleration1.StepCount - acceleration2.StepCount;
            SEND(acceleration1);

            while (Math.Abs(constantTrackSteps) > 0)
            {
                var nextSteps = GetStepSlice(constantTrackSteps);
                constantTrackSteps -= nextSteps;

                SEND_Constant(nextSteps, acceleration1.EndDeltaT, 0, 0);
            }

            if (Math.Abs(stepCount) > 1)
                SEND(acceleration2);
        }

        public Int16 GetStepSlice(long steps, Int16 maxSize = 30000)
        {
            maxSize = Math.Abs(maxSize);
            if (steps > 0)
                return (Int16)Math.Min(maxSize, steps);
            else
                return (Int16)Math.Max(-maxSize, steps);
        }

        public void SEND(Acceleration acceleration)
        {
            if (acceleration == null)
                return;

            if (acceleration.StepCount == 0)
                return;

            SEND_Acceleration(acceleration.StepCount, acceleration.StartDeltaT, acceleration.StartN);
        }


        internal Acceleration CalculateBoundedAcceleration(UInt16 startDeltaT, UInt16 endDeltaT, Int16 accelerationDistanceLimit, int accelerationNumerator = 1, int accelerationDenominator = 1)
        {
            checked
            {
                var stepSign = accelerationDistanceLimit >= 0 ? 1 : -1;
                var limit = Math.Abs(accelerationDistanceLimit);

                var startN = calculateN(startDeltaT, accelerationNumerator, accelerationDenominator);
                var endN = calculateN(endDeltaT, accelerationNumerator, accelerationDenominator);

                Int16 stepCount;
                if (startN < endN)
                {
                    //acceleration
                    stepCount = (Int16)(Math.Min(endN - startN, limit));
                    var limitedDeltaT = calculateDeltaT(startDeltaT, startN, stepCount, accelerationNumerator, accelerationDenominator);
                    return new Acceleration((Int16)(stepCount * stepSign), startDeltaT, startN, limitedDeltaT);
                }
                else
                {
                    //deceleration
                    stepCount = (Int16)(Math.Min(startN - endN, limit));
                    var limitedDeltaT = calculateDeltaT(startDeltaT, (Int16)(-startN), stepCount, accelerationNumerator, accelerationDenominator);
                    return new Acceleration((Int16)(stepCount * stepSign), startDeltaT, (Int16)(-startN), limitedDeltaT);
                }
            }
        }

        private UInt16 calculateDeltaT(UInt16 startDeltaT, Int16 startN, Int16 stepCount, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var endN = Math.Abs(startN + stepCount);
                return (UInt16)(TimeScale / Math.Sqrt(2.0 * endN * (long)MaxAcceleration));
            }
        }

        private Int16 calculateN(UInt16 startDeltaT, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var n1 = (long)TimeScale * TimeScale * accelerationNumerator / 2 / startDeltaT / startDeltaT / MaxAcceleration / accelerationDenominator;
                return (Int16)Math.Max(1, n1);
            }
        }

        public Acceleration CreateAcceleration(int startDeltaT, int endDeltaT)
        {
            if (startDeltaT > UInt16.MaxValue || endDeltaT > UInt16.MaxValue)
                throw new NotSupportedException("Value out of range");

            return CalculateBoundedAcceleration((UInt16)startDeltaT, (UInt16)endDeltaT, Int16.MaxValue);
        }

        public int DeltaTFromRPM(int rpm)
        {
            if (rpm == 0)
                return StartDeltaT;

            var deltaT = TimeScale * 60 / 400 / rpm;
            return deltaT;
        }

        internal void SEND_Acceleration(Int16 stepCount, UInt16 initialDeltaT, Int16 n)
        {
            //Debug.WriteLine("A({0},{1},{2})", stepCount, initialDeltaT, n);

            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'A');
            sendBuffer.AddRange(ToBytes(stepCount));
            sendBuffer.AddRange(ToBytes(initialDeltaT));
            sendBuffer.AddRange(ToBytes(n));
            send(sendBuffer);
        }

        internal void SEND_Constant(Int16 stepCount, UInt16 baseDeltaT, UInt16 periodNumerator, UInt16 periodDenominator)
        {
            //Debug.WriteLine("C({0},{1},{2},{3})", stepCount, baseDeltaT, periodNumerator, periodDenominator);

            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'C');
            sendBuffer.AddRange(ToBytes(stepCount));
            sendBuffer.AddRange(ToBytes(baseDeltaT));
            sendBuffer.AddRange(ToBytes(periodNumerator));
            sendBuffer.AddRange(ToBytes(periodDenominator));
            send(sendBuffer);
        }

        internal void SEND_Deceleration(Int16 stepCount, UInt16 initialDeltaT, Int16 n)
        {
            SEND_Acceleration(stepCount, initialDeltaT, (Int16)(-n));
        }

        internal byte[] ToBytes(Int16 value)
        {
            return new byte[]{
                (byte)(value>>8),
                (byte)(value & 255)
            };
        }

        internal byte[] ToBytes(UInt16 value)
        {
            return new byte[]{
                (byte)(value>>8),
                (byte)(value & 255)
            };
        }

        private void send(IEnumerable<byte> sendBuffer)
        {
            var data = new List<byte>(sendBuffer);
            if (StepperIndex == 1)
            {
                if (_constructedInstruction.Count == 0)
                {
                    data.AddRange(sendBuffer.Skip(1));//send same data for the second stepper
                }
                else
                {
                    data.AddRange(_constructedInstruction);
                    _constructedInstruction.Clear();
                }
            }
            else if (StepperIndex == 2)
            {
                --StepperIndex;
                _constructedInstruction.AddRange(sendBuffer.Skip(1));
                return;
            }
            else
            {
                throw new NotImplementedException();
            }

            while (data.Count < _instructionLength - 2)
                data.Add(123); // pad with value which will enable easy checksum error detection

            Int16 checksum = 0;
            foreach (var b in data)
            {
                checksum += b;
            }
            data.AddRange(ToBytes(checksum));
            var dataBuffer = data.ToArray();
            lock (_L_sendQueue)
            {
                _sendQueue.Enqueue(dataBuffer);
                Monitor.Pulse(_L_sendQueue);
            }
        }
    }
}
