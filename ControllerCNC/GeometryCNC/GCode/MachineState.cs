using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace GeometryCNC.GCode
{
    public class MachineState
    {
        internal Point3D CurrentPosition;

        internal DistanceMode DistanceMode = DistanceMode.Absolute;

        internal MotionMode MotionMode = MotionMode.IsLinear;

        internal CoolantMode CoolantMode = CoolantMode.CoolantOff;

        internal FeedRateMode FeedRateMode = FeedRateMode.G93;

        internal PlaneSelectionMode PlaneSelectionMode = PlaneSelectionMode.XY;

        internal SpindleTurningMode SpindleTurningMode = SpindleTurningMode.SpindleStop;

        internal CoordinateSystemSelection CoordinateSystemSelection = CoordinateSystemSelection.WCS_1;

        internal UnitMode UnitMode = UnitMode.Millimeters;

        internal string ToolId;

        internal double SpindleRPM;

        internal double FeedRate;

        internal MachineInstruction MachineInstructionBuffer = MachineInstruction.Nop;

        internal MotionInstruction MotionInstructionBuffer = MotionInstruction.Nop;

        internal double? BufferX = null;

        internal double? BufferY = null;

        internal double? BufferZ = null;

        internal double? BufferI = null;

        internal double? BufferJ = null;

        internal double? BufferR = null;
    }
}
