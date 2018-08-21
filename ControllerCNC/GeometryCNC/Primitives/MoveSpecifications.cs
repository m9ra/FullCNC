using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryCNC.Primitives
{
    public enum DistanceMode
    {
        Absolute = 90,
        Relative = 91,
    }

    public enum MotionMode
    {
        IsLinearRapid = 0,
        IsLinear = 1,
        IsCircularCW = 2,
        IsCircularCCW = 3,

        G38_2 = 382, G80 = 80, G81 = 81, G82 = 82, G83 = 83, G84 = 84, G85 = 85, G86 = 86, G87 = 87, G88 = 88, G89 = 89
    };

    public enum MotionInstruction
    {
        Nop = -1,
        G4 = 4, G10 = 10, Homing = 28, SecondaryHoming = 30, G53 = 53, G92 = 92, G92_1 = 921, G92_2 = 922, G92_3 = 923
    }

    public enum MachineInstruction
    {
        Nop = -1,
        ToolChange = 6,
        CompulsoryStop = 0, OptionalStop = 1, EndOfProgram = 2, EndOfProgramModern = 30, M60 = 60
    }

    public enum PlaneSelectionMode
    {
        XY = 17, G18 = 18, G19 = 19
    }

    public enum FeedRateMode
    {
        G93 = 93, G94 = 94
    }

    public enum UnitMode
    {
        Inches = 20, Millimeters = 21
    }

    public enum CutterRadiusCompensation
    {

    }

    public enum ToolLengthOffset
    {

    }

    public enum ReturnedModeInCannedCycles
    {

    }

    public enum CoordinateSystemSelection
    {
        WCS_1 = 54, G55 = 55, G56 = 56, G57 = 57, G58 = 58, G59 = 59, G59_1 = 591, G59_2 = 592, G59_3 = 593
    }

    public enum PathControlMode
    {

    }

    public enum SpindleTurningMode
    {
        SpindleOnCW = 3, SpindleOnCCW = 4, SpindleStop = 5
    }

    public enum CoolantMode
    {
        CoolantOnMist = 7, CoolantOnFlood = 8, CoolantOff = 9
    }
}
