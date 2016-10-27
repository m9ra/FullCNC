using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC
{
    class PositionController
    {
        private DriverCNC _cnc;

        private int _currentPosition;

        bool _lastDirection;

        public PositionController(DriverCNC driver)
        {
            _cnc = driver;
        }

        public void SetPosition(int newPosition)
        {
            var steps = newPosition - _currentPosition;

            throw new NotImplementedException("Refactoring");

            //if (_cnc.IncompletePlanCount == 0)
            //  throw new NotSupportedException("Race condition.");
            //position setting is blocking for now
            while (_cnc.IncompletePlanCount > 0)
                System.Threading.Thread.Sleep(1);

            _currentPosition = newPosition;
        }

        internal void ResetPosition()
        {
            _currentPosition = 0;
        }
    }
}
