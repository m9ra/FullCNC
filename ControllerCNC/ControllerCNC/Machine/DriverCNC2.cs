using System;
using System.Collections.Generic;
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
        /// Determine whether communication restart is required.
        /// </summary>
        private volatile bool _resetCommunication = false;

        /// <summary>
        /// Whether instruction receive confirmation is expected.
        /// </summary>
        private volatile bool _expectsConfirmation = false;

        /// <summary>
        /// Lock for send queue synchornization.
        /// </summary>
        private readonly object _L_sendQueue = new object();

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
        /// Determine whether machine is connected.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Handler that can be used for observing of received data.
        /// </summary>
        public event DataReceivedHandler OnDataReceived;

        public DriverCNC2()
        {
            _communicationWorker = new Thread(SERIAL_worker);
            _communicationWorker.IsBackground = true;
            _communicationWorker.Priority = ThreadPriority.Highest;
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

                IsConnected = true;
                try
                {
                    //the communication handler can return only by throwing exception
                    communicate(port);
                }
                catch (Exception)
                {
                    //disconnection appeared                     
                }
                IsConnected = false;
            }
        }

        #region Communication handling

        private void communicate(SerialPort port)
        {
            //welcome message
            send(new InitializationInstruction());

            for (; ; )
            {
                byte[] data;
                lock (_L_sendQueue)
                {
                    while (_sendQueue.Count == 0 && _bufferedInstructions.Count >= Configuration.InstructionBufferLimit)
                    {
                        if (_resetCommunication)
                            return;

                        Monitor.Wait(_L_sendQueue);
                    }

                    if (_resetCommunication)
                        return;

                    data = _sendQueue.Dequeue();
                    port.Write(data, 0, data.Length);

                    _expectsConfirmation = true;
                    while (_expectsConfirmation)
                        Monitor.Wait(_L_sendQueue);
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

                switch (_parser.Response)
                {
                    case MachineResponse.IsOnline:
                        resetCommunication();
                        break;

                    case MachineResponse.Welcome:
                        runInitialization();
                        break;

                    case MachineResponse.HomingCompleted:
                        homingCompleted();
                        break;

                    case MachineResponse.StateData:
                        setState(_parser.MachineStateBuffer);
                        break;

                    case MachineResponse.ConfirmationReceived:
                        confirmationReceived();
                        break;

                    case MachineResponse.InstructionFinished:
                        instructionFinished();
                        break;

                    case MachineResponse.BadDataReceived:
                        badDataReceived();
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

            lock (_L_sendQueue)
            {
                _bufferedInstructions.Enqueue(instruction);
                _sendQueue.Enqueue(dataBuffer);
                Monitor.Pulse(_L_sendQueue);
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

        private void resetCommunication()
        {
            lock (_L_sendQueue)
            {
                _resetCommunication = true;
                Monitor.Pulse(_resetCommunication);
            }
        }

        private void runInitialization()
        {
            throw new NotImplementedException();
        }

        private void homingCompleted()
        {
            throw new NotImplementedException();
        }

        private void badDataReceived()
        {
            throw new NotImplementedException();
        }

        private void instructionFinished()
        {
            lock (_L_sendQueue)
            {
                var instruction = _bufferedInstructions.Dequeue();
                Monitor.Pulse(_L_sendQueue);

                throw new NotImplementedException("Instruction unbuffered");
            }
        }
        
        private void confirmationReceived()
        {
            lock (_L_sendQueue)
            {
                _expectsConfirmation = false;
                Monitor.Pulse(_L_sendQueue);
            }
        }

        private void setState(byte[] machineStateBuffer)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Simulator

        private void runCncSimulator()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
