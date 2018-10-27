using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControllerCNC.Primitives;

namespace ControllerCNC.Planning
{
    public class PlanPart3D
    {
        public readonly Point3Dmm StartPoint;

        public readonly Point3Dmm EndPoint;

        public readonly Acceleration AccelerationRamp;

        public readonly Speed SpeedLimit;

        public PlanPart3D(Point3Dmm currentPoint, Point3Dmm endPoint, Acceleration accelerationRamp, Speed speedLimit)
        {
            StartPoint = currentPoint;
            EndPoint = endPoint;
            AccelerationRamp = accelerationRamp;
            SpeedLimit = speedLimit;
        }
    }
}
