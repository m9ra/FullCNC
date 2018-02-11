using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    class PlanPart3D
    {
        internal readonly Point3Dmm StartPoint;

        internal readonly Point3Dmm EndPoint;

        internal readonly Acceleration AccelerationRamp;

        internal readonly Speed SpeedLimit;

        public PlanPart3D(Point3Dmm currentPoint, Point3Dmm endPoint, Acceleration accelerationRamp, Speed speedLimit)
        {
            StartPoint = currentPoint;
            EndPoint = endPoint;
            AccelerationRamp = accelerationRamp;
            SpeedLimit = speedLimit;
        }
    }
}
