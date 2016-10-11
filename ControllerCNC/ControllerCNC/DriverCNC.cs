using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Ports;
using System.Threading;

namespace ControllerCNC
{
    delegate void DataReceivedHandler(string data);

    class DriverCNC
    {
        /// <summary>
        /// What is maximum number of incomplete plans that can be 
        /// sent to machine.
        /// </summary>
        internal readonly int MaxIncompletePlanCount = 7;

        /// <summary>
        /// DeltaT which is used for steppers start.
        /// </summary>
        public UInt16 StartDeltaT { get { return (UInt16)_accelerationTable.Keys.Max(); } }

        /// <summary>
        /// Fastest DeltaT which is supported
        /// </summary>
        public UInt16 FastestDeltaT { get { return (UInt16)_accelerationTable.Keys.Min(); } }

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

        internal UInt16 GetFastestDeltaT(int accelerationDistance)
        {

            //gets deltaT for fastest speed that we can achieve
            for (var i = FastestDeltaT; i < StartDeltaT; ++i)
            {
                if (Math.Abs(accelerationDistance) > _accelerationTable[i])
                    return i;
            }

            return StartDeltaT;
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
                        //throw new NotImplementedException("Incomplete message erased");
                        break;
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

        internal void SEND_Transition(int startRPM, int targetRPM, int stepCount, int endRPM)
        {
            var direction = stepCount > 0;
            stepCount = Math.Abs(stepCount);
            var startDeltaT = DeltaTFromRPM(startRPM);
            var targetDeltaT = DeltaTFromRPM(targetRPM);
            var endDeltaT = DeltaTFromRPM(endRPM);

            if (startDeltaT > UInt16.MaxValue || targetDeltaT > UInt16.MaxValue || endDeltaT > UInt16.MaxValue)
                throw new NotImplementedException("split");

            var startAcceleration = createAcceleration((UInt16)startDeltaT, (UInt16)targetDeltaT, direction);
            var endAcceleration = createAcceleration((UInt16)targetDeltaT, (UInt16)endDeltaT, direction);

            if (startAcceleration != null && endAcceleration != null && startAcceleration.StepCount + endAcceleration.StepCount > stepCount)
                throw new NotSupportedException("Cannot provide specified transition due to acceleration limits.");

            if (startAcceleration != null)
            {
                SEND(startAcceleration);
                stepCount -= startAcceleration.StepCount;
            }

            if (endAcceleration != null)
            {
                stepCount -= endAcceleration.StepCount;
            }
            while (stepCount > 0)
            {
                Int16 nextSteps = (Int16)Math.Min(100, stepCount);
                stepCount -= nextSteps;
                if (!direction)
                    nextSteps = (Int16)(-nextSteps);

                SEND_Constant(nextSteps, (UInt16)targetDeltaT, 0);
            }

            SEND(endAcceleration);
        }

        public void SEND(Acceleration acceleration)
        {
            if (acceleration == null)
                return;

            if (acceleration.StepCount == 0)
                return;

            SEND_Acceleration(acceleration.StepCount, acceleration.AccelerationNumerator, acceleration.AccelerationDenominator, acceleration.InitialDeltaT);
        }

        private Acceleration createAcceleration(UInt16 startDeltaT, UInt16 endDeltaT, bool clockWise = true)
        {
            var stepSign = clockWise ? 1 : -1;
            var startDistance = GetAccelerationDistance(startDeltaT);
            var endDistance = GetAccelerationDistance(endDeltaT);

            if (startDistance == endDistance)
                return null;

            if (startDistance < endDistance)
                return new Acceleration((Int16)((endDistance - startDistance) * 5 * stepSign), (Int16)1, (Int16)5, startDeltaT);
            else
                return new Acceleration((Int16)((startDistance - endDistance) * 5 * stepSign), (Int16)(-1), (Int16)5, startDeltaT);
        }

        public Acceleration CreateAcceleration(int startDeltaT, int endDeltaT)
        {
            if (startDeltaT > UInt16.MaxValue || endDeltaT > UInt16.MaxValue)
                throw new NotSupportedException("Value out of range");

            return createAcceleration((UInt16)startDeltaT, (UInt16)endDeltaT);
        }

        internal Int16 GetAccelerationDistance(UInt16 deltaT)
        {
            var maxAccDelta = _accelerationTable.Keys.Max();
            if (deltaT >= maxAccDelta)
                return (Int16)0;

            return (Int16)_accelerationTable[deltaT];
        }

        public int DeltaTFromRPM(int rpm)
        {
            if (rpm == 0)
                return _accelerationTable.Keys.Max();

            var deltaT = 1000000 * 60 / 400 / rpm;
            deltaT = Math.Max(_accelerationTable.Keys.Min(), deltaT);
            return deltaT;
        }

        internal void SEND_Acceleration(Int16 stepCount, Int16 accelerationNumerator, Int16 accelerationDenominator, UInt16 initialDeltaT)
        {
            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'A');
            sendBuffer.AddRange(ToBytes(stepCount));
            sendBuffer.AddRange(ToBytes(accelerationNumerator));
            sendBuffer.AddRange(ToBytes(accelerationDenominator));
            sendBuffer.AddRange(ToBytes(initialDeltaT));
            send(sendBuffer);
        }

        internal void SEND_Constant(Int16 stepCount, UInt16 baseDeltaT, UInt16 remainderPeriod)
        {
            var sendBuffer = new List<byte>();

            sendBuffer.Add((byte)'C');
            sendBuffer.AddRange(ToBytes(stepCount));
            sendBuffer.AddRange(ToBytes(baseDeltaT));
            sendBuffer.AddRange(ToBytes(remainderPeriod));
            send(sendBuffer);
        }

        internal void SEND_Deceleration(Int16 stepCount, Int16 accelerationNumerator, Int16 accelerationDenominator, UInt16 initialDeltaT)
        {
            SEND_Acceleration(stepCount, (Int16)(-accelerationNumerator), accelerationDenominator, initialDeltaT);
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

        /// <summary>
        /// TODO retrieve this from the CNC machine.
        /// </summary>
        private readonly Dictionary<int, int> _accelerationTable = new Dictionary<int, int>() { { 350, 51 }, { 349, 51 }, { 348, 51 }, { 347, 51 }, { 346, 52 }, { 345, 52 }, { 344, 52 }, { 343, 53 }, { 342, 53 }, { 341, 53 }, { 340, 54 }, { 339, 54 }, { 338, 54 }, { 337, 55 }, { 336, 55 }, { 335, 55 }, { 334, 56 }, { 333, 56 }, { 332, 56 }, { 331, 57 }, { 330, 57 }, { 329, 57 }, { 328, 58 }, { 327, 58 }, { 326, 58 }, { 325, 59 }, { 324, 59 }, { 323, 59 }, { 322, 60 }, { 321, 60 }, { 320, 61 }, { 319, 61 }, { 318, 61 }, { 317, 62 }, { 316, 62 }, { 315, 62 }, { 314, 63 }, { 313, 63 }, { 312, 64 }, { 311, 64 }, { 310, 65 }, { 309, 65 }, { 308, 65 }, { 307, 66 }, { 306, 66 }, { 305, 67 }, { 304, 67 }, { 303, 68 }, { 302, 68 }, { 301, 68 }, { 300, 69 }, { 299, 69 }, { 298, 70 }, { 297, 70 }, { 296, 71 }, { 295, 71 }, { 294, 72 }, { 293, 72 }, { 292, 73 }, { 291, 73 }, { 290, 74 }, { 289, 74 }, { 288, 75 }, { 287, 75 }, { 286, 76 }, { 285, 76 }, { 284, 77 }, { 283, 78 }, { 282, 78 }, { 281, 79 }, { 280, 79 }, { 279, 80 }, { 278, 80 }, { 277, 81 }, { 276, 82 }, { 275, 82 }, { 274, 83 }, { 273, 83 }, { 272, 84 }, { 271, 85 }, { 270, 85 }, { 269, 86 }, { 268, 87 }, { 267, 87 }, { 266, 88 }, { 265, 88 }, { 264, 89 }, { 263, 90 }, { 262, 91 }, { 261, 91 }, { 260, 92 }, { 259, 93 }, { 258, 93 }, { 257, 94 }, { 256, 95 }, { 255, 96 }, { 254, 96 }, { 253, 97 }, { 252, 98 }, { 251, 99 }, { 250, 100 }, { 249, 100 }, { 248, 101 }, { 247, 102 }, { 246, 103 }, { 245, 104 }, { 244, 104 }, { 243, 105 }, { 242, 106 }, { 241, 107 }, { 240, 108 }, { 239, 109 }, { 238, 110 }, { 237, 111 }, { 236, 112 }, { 235, 113 }, { 234, 114 }, { 233, 115 }, { 232, 116 }, { 231, 117 }, { 230, 118 }, { 229, 119 }, { 228, 120 }, { 227, 121 }, { 226, 122 }, { 225, 123 }, { 224, 124 }, { 223, 125 }, { 222, 126 }, { 221, 127 }, { 220, 129 }, { 219, 130 }, { 218, 131 }, { 217, 132 }, { 216, 133 }, { 215, 135 }, { 214, 136 }, { 213, 137 }, { 212, 139 }, { 211, 140 }, { 210, 141 }, { 209, 143 }, { 208, 144 }, { 207, 145 }, { 206, 147 }, { 205, 148 }, { 204, 150 }, { 203, 151 }, { 202, 153 }, { 201, 154 }, { 200, 156 }, { 199, 157 }, { 198, 159 }, { 197, 161 }, { 196, 162 }, { 195, 164 }, { 194, 166 }, { 193, 167 }, { 192, 169 }, { 191, 171 }, { 190, 173 }, { 189, 174 }, { 188, 176 }, { 187, 178 }, { 186, 180 }, { 185, 182 }, { 184, 184 }, { 183, 186 }, { 182, 188 }, { 181, 190 }, { 180, 192 }, { 179, 195 }, { 178, 197 }, { 177, 199 }, { 176, 201 }, { 175, 204 }, { 174, 206 }, { 173, 208 }, { 172, 211 }, { 171, 213 }, { 170, 216 }, { 169, 218 }, { 168, 221 }, { 167, 224 }, { 166, 226 }, { 165, 229 }, { 164, 232 }, { 163, 235 }, { 162, 238 }, { 161, 241 }, { 160, 244 }, { 159, 247 }, { 158, 250 }, { 157, 253 }, { 156, 256 }, { 155, 260 }, { 154, 263 }, { 153, 266 }, { 152, 270 }, { 151, 274 }, { 150, 277 }, { 149, 281 }, { 148, 285 }, { 147, 289 }, { 146, 293 }, { 145, 297 }, { 144, 301 }, { 143, 305 }, { 142, 309 }, { 141, 314 }, { 140, 318 }, { 139, 323 }, { 138, 328 }, { 137, 332 }, { 136, 337 }, { 135, 342 }, { 134, 348 }, { 133, 353 }, { 132, 358 }, { 131, 364 }, { 130, 369 }, { 129, 375 }, { 128, 381 }, { 127, 387 }, { 126, 393 }, { 125, 400 }, { 124, 406 }, { 123, 413 }, { 122, 419 }, { 121, 426 }, { 120, 434 }, { 119, 441 }, { 118, 448 }, { 117, 456 }, { 116, 464 }, { 115, 472 }, { 114, 480 }, { 113, 489 }, { 112, 498 }, { 111, 507 }, { 110, 516 }, { 109, 526 }, { 108, 535 }, { 107, 545 }, { 106, 556 }, { 105, 566 }, { 104, 577 }, { 103, 589 }, { 102, 600 }, { 101, 612 }, { 100, 625 } };
    }
}
