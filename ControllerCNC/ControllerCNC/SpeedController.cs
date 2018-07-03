using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

using ControllerCNC.Machine;

namespace ControllerCNC
{
    class SpeedController
    {
        private readonly DriverCNC2 _cnc;

        private readonly Thread _speedWorker;

        private volatile int _desiredDeltaT;

        private double _desiredDeltaRemainder;

        private volatile int _currentDeltaT;

        private volatile bool _stop;


        private Queue<int> _plannedTimes = new Queue<int>();

        private int _plannedTimeTotal = 0;

        public volatile bool Direction;


        internal SpeedController(DriverCNC2 cnc)
        {
            _cnc = cnc;
            _desiredDeltaT = Configuration.StartDeltaT;
            _currentDeltaT = _desiredDeltaT;
            _speedWorker = new Thread(worker);
            _speedWorker.IsBackground = true;
            _stop = true;
            SetRPM(400);
            _speedWorker.Start();
        }

        private void worker()
        {
            while (true)
            {
                if (!_stop)
                {

                    while (_cnc.IncompleteInstructionCount < _plannedTimes.Count)
                    {
                        _plannedTimeTotal -= _plannedTimes.Dequeue();
                    }

                    while (_plannedTimeTotal < 100 * 1000) //100
                        sendNewPlan();
                }
                Thread.Sleep(1);
            }
        }

        private void sendNewPlan()
        {
            if (_currentDeltaT != _desiredDeltaT)
                _currentDeltaT = _desiredDeltaT;

            var stepCount = (Int16)(Math.Max(2, 20000 / _currentDeltaT));
            var sendStepCount = Direction ? (Int16)(-stepCount) : stepCount;
            var remainder = (UInt16)(_desiredDeltaRemainder * stepCount);
            var instruction = new ConstantInstruction(sendStepCount, _currentDeltaT, remainder);
            _cnc.SEND(Axes.UVXY(instruction, instruction, instruction, instruction));

            var plannedTime = stepCount * _currentDeltaT;
            _plannedTimeTotal += plannedTime;
            _plannedTimes.Enqueue(plannedTime);
        }

        internal void Start()
        {
            _stop = false;
        }

        internal void Stop()
        {
            _stop = true;
        }

        internal void SetRPM(int rpm)
        {
            var deltaT = 2000000.0 * 60 / 400 / rpm;


            if (deltaT > UInt16.MaxValue)
                throw new NotSupportedException("Value is out of range");

            _desiredDeltaT = (int)deltaT;
            _desiredDeltaRemainder = deltaT - Math.Floor(deltaT);
        }
    }
}
