using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace GeometryCNC.GCode
{
    public class Parser
    {
        private readonly string[] _lines;

        private readonly double _precision;

        public Parser(string gcodes, double precision = 0.01)
        {
            _precision = precision;
            _lines = gcodes.Split('\n');
        }

        public ToolPath GetToolPath()
        {
            var result = new ToolPath();

            var machineState = new MachineState();
            foreach (var line in _lines)
            {
                var lineWithoutComments = Regex.Replace(line, "([;%].*|[(][^)]*[)])", "").Trim();
                var rawTokens = lineWithoutComments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var tokenStack = filter(rawTokens);
                var lastTokenCount = 0;

                if (!tokenStack.Any())
                    continue;

                while (lastTokenCount != tokenStack.Count)
                {
                    lastTokenCount = tokenStack.Count;
                    update(machineState, tokenStack);
                }

                tryAddMove(machineState, result);

                if (tokenStack.Any())
                    throw new NotImplementedException("Tokens not consumed");
            }

            return result;
        }

        private void tryAddMove(MachineState state, ToolPath result)
        {
            var lastPosition = state.CurrentPosition;
            if (state.BufferX.HasValue || state.BufferY.HasValue || state.BufferZ.HasValue)
            {
                if (state.MotionInstructionBuffer == MotionInstruction.Homing)
                {
                    //TODO homing arguments
                    //state.CurrentPosition = new Point3D(lastPosition.X, lastPosition.Y, 0);
                }
                else if (state.MotionInstructionBuffer == MotionInstruction.Nop)
                {
                    var p = state.CurrentPosition;
                    p.X = getPosition(state, p.X, state.BufferX);
                    p.Y = getPosition(state, p.Y, state.BufferY);
                    p.Z = getPosition(state, p.Z, state.BufferZ);
                    state.CurrentPosition = p;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (state.CurrentPosition != lastPosition)
            {
                generateMovement(state, lastPosition, result);
            }

            state.BufferX = null;
            state.BufferY = null;
            state.BufferZ = null;
            state.BufferI = null;
            state.BufferJ = null;
            state.BufferR = null;

            state.MachineInstructionBuffer = MachineInstruction.Nop;
            state.MotionInstructionBuffer = MotionInstruction.Nop;
        }

        private void generateMovement(MachineState state, Point3D lastPosition, ToolPath result)
        {
            var startPosition = lastPosition;
            var endPosition = state.CurrentPosition;
            switch (state.MotionMode)
            {
                case MotionMode.IsLinear:
                case MotionMode.IsLinearRapid:
                    //for now aproximate by a simple line
                    result.AddLine(endPosition, state);
                    break;

                case MotionMode.IsCircularCW:
                    addCircularApproximation(result, state, startPosition, endPosition, clockwise: true);
                    break;

                case MotionMode.IsCircularCCW:
                    addCircularApproximation(result, state, startPosition, endPosition, clockwise: false);
                    break;


                default:
                    throw new NotImplementedException();
            }
        }

        private void addCircularApproximation(ToolPath result, MachineState state, Point3D startPosition, Point3D endPosition, bool clockwise)
        {
            if (state.PlaneSelectionMode != PlaneSelectionMode.XY)
                throw new NotImplementedException();

            //if (state.DistanceMode != DistanceMode.Relative)
            //TODO check specification properly regarding IJK behaviour - it seems inconsistent

            Point3D centerPoint;
            if (state.BufferI.HasValue)
            {
                var i = state.BufferI.Value;
                var j = state.BufferJ.Value;
                centerPoint = new Point3D(startPosition.X + i, startPosition.Y + j, startPosition.Z); //TODO this changes with selected plane

            }
            else
            {
                var inputRadius = state.BufferR.Value;
                // Negative R is g-code-alese for "I want a circle with more than 180 degrees of travel" (go figure!), 
                // even though it is advised against ever generating such circles in a single line of g-code. By 
                // inverting the sign of h_x2_div_d the center of the circles is placed on the opposite side of the line of
                // travel and thus we get the unadvisably long arcs as prescribed.

                var x1 = startPosition.X;
                var y1 = startPosition.Y;
                var x2 = endPosition.X;
                var y2 = endPosition.Y;
                var q = Math.Sqrt(Math.Pow((x2 - x1), 2) + Math.Pow((y2 - y1), 2));

                var y3 = (y1 + y2) / 2;

                var x3 = (x1 + x2) / 2;

                var basex = Math.Sqrt(Math.Pow(inputRadius, 2) - Math.Pow((q / 2), 2)) * (y1 - y2) / q; //calculate once
                var basey = Math.Sqrt(Math.Pow(inputRadius, 2) - Math.Pow((q / 2), 2)) * (x2 - x1) / q; //calculate once

                var centerx1 = x3 + basex; //center x of circle 1
                var centery1 = y3 + basey; //center y of circle 1
                var centerx2 = x3 - basex; //center x of circle 2
                var centery2 = y3 - basey; //center y of circle 2

                //TODO negative radius and center selection
                centerPoint = new Point3D(centerx1, centery1, startPosition.Z);
            }

            var radius = (startPosition - centerPoint).Length;
            var radius2 = (endPosition - centerPoint).Length;
            if (Math.Abs(radius - radius2) > _precision)
                throw new NotSupportedException("Invalid arc detected");

            var totalC = (startPosition - endPosition).Length;
            var totalAngle = 2 * Math.Asin(totalC / 2 / radius);
            var stepAngle = 2 * Math.Acos(1 - _precision / radius);

            var initialAngle = Math.Atan2(startPosition.Y - centerPoint.Y, startPosition.X - centerPoint.X); //REALLY, the coordinates are reversed
            var finalAngle = Math.Atan2(endPosition.Y - centerPoint.Y, endPosition.X - centerPoint.X);

            var pi2 = Math.PI * 2;
            initialAngle = (initialAngle + pi2) % pi2;
            finalAngle = (finalAngle + pi2) % pi2;

            double direction;
            if (clockwise)
            {
                direction = -1.0;
                if (initialAngle < finalAngle)
                    //rotating clockwise == subtract steps from initial angle
                    initialAngle += pi2;
            }
            else
            {
                direction = 1.0;
                if (initialAngle > finalAngle)
                    //rotating ccw == add steps to initial angle
                    initialAngle -= pi2;
            }


            totalAngle = Math.Abs(finalAngle - initialAngle);



            var segmentCount = Math.Ceiling(totalAngle / stepAngle);

            var lastPosition = startPosition;
            for (var segmentIndex = 1; segmentIndex < segmentCount; ++segmentIndex)
            {
                var actualAngle = initialAngle + direction * totalAngle * segmentIndex / segmentCount;
                var x = Math.Cos(actualAngle) * radius;
                var y = Math.Sin(actualAngle) * radius;

                var nextPosition = new Point3D();
                nextPosition.X = centerPoint.X + x;
                nextPosition.Y = centerPoint.Y + y;
                nextPosition.Z = centerPoint.Z; //TODO this changes with plane selection
                result.AddLine(nextPosition, state);

                lastPosition = nextPosition;
            }
            result.AddLine(endPosition, state);
        }

        private double getPosition(MachineState state, double coordinate, double? coordinateBuffer)
        {
            if (!coordinateBuffer.HasValue)
                return coordinate;

            if (state.DistanceMode == DistanceMode.Absolute)
                return coordinateBuffer.Value;

            return coordinate + coordinateBuffer.Value;
        }

        private Stack<string> filter(IEnumerable<string> tokens)
        {
            return new Stack<string>(tokens.Reverse());
        }

        private void update(MachineState state, Stack<string> tokens)
        {
            consumeLineNumbers(state, tokens);

            updateGCodes(state, tokens);
            updateMCodes(state, tokens);
            updateArguments(state, tokens);
        }

        private void updateArguments(MachineState state, Stack<string> tokens)
        {
            if (tokenPeek("T", tokens))
                state.ToolId = tokens.Pop();

            if (tokenPeek("S", tokens))
                updateArgument(ref state.SpindleRPM, tokens);

            if (tokenPeek("X", tokens))
                updateArgument(ref state.BufferX, tokens);

            if (tokenPeek("Y", tokens))
                updateArgument(ref state.BufferY, tokens);

            if (tokenPeek("Z", tokens))
                updateArgument(ref state.BufferZ, tokens);

            if (tokenPeek("R", tokens))
                updateArgument(ref state.BufferR, tokens);

            if (tokenPeek("F", tokens))
                updateArgument(ref state.FeedRate, tokens);

            if (tokenPeek("I", tokens))
                updateArgument(ref state.BufferI, tokens);

            if (tokenPeek("J", tokens))
                updateArgument(ref state.BufferJ, tokens);
        }

        private void updateGCodes(MachineState state, Stack<string> tokens)
        {
            if (tokenPeek("G", tokens))
            {
                var instruction = MotionInstruction.Nop;


                var result = update(ref state.MotionMode, tokens) ||
                update(ref state.DistanceMode, tokens) ||
                update(ref state.FeedRateMode, tokens) ||
                update(ref state.PlaneSelectionMode, tokens) ||
                update(ref state.UnitMode, tokens) ||
                update(ref state.CoordinateSystemSelection, tokens);

                if (result)
                    return;

                update(ref instruction, tokens);
                if (instruction != MotionInstruction.Nop)
                {
                    if (state.MotionInstructionBuffer != MotionInstruction.Nop)
                        throw new NotImplementedException("Multiple instructions");

                    state.MotionInstructionBuffer = instruction;
                }
            }
        }

        private void updateMCodes(MachineState state, Stack<string> tokens)
        {
            if (tokenPeek("M", tokens))
            {
                var result = update(ref state.CoolantMode, tokens) ||
                update(ref state.SpindleTurningMode, tokens);

                if (result)
                    return;

                var instruction = MachineInstruction.Nop;
                update(ref instruction, tokens);

                if (instruction != MachineInstruction.Nop)
                {
                    if (state.MachineInstructionBuffer != MachineInstruction.Nop)
                        throw new NotImplementedException("Multiple instructions");

                    state.MachineInstructionBuffer = instruction;
                }
            }
        }

        private void consumeLineNumbers(MachineState state, Stack<string> tokens)
        {
            if (tokenPeek("N", tokens))
            {
                tokens.Pop();
            }
        }

        private void updateArgument(ref double field, Stack<string> tokens)
        {
            if (!tokens.Any())
                return;

            if (!double.TryParse(tokens.Peek().Substring(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var code))
                return;

            field = code;
            tokens.Pop();
        }

        private void updateArgument(ref double? field, Stack<string> tokens)
        {
            if (!tokens.Any())
                return;

            if (!double.TryParse(tokens.Peek().Substring(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var code))
                return;

            if (field.HasValue)
                throw new NotImplementedException("Value overrides are not allowed.");

            field = code;
            tokens.Pop();
        }

        private bool update<T>(ref T field, Stack<string> tokens)
        {
            if (!tokens.Any())
                return false;

            if (!int.TryParse(tokens.Peek().Substring(1), out var code))
                return false;

            if (!Enum.IsDefined(typeof(T), code))
                return false;

            field = (T)(object)code;
            tokens.Pop();
            return true;
        }

        private bool tokenPeek(string prefix, Stack<string> tokens)
        {
            return tokens.Any() && tokens.Peek().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
