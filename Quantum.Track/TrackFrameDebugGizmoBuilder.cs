using System;
using Quantum.Math;

namespace Quantum.Track
{
    public static class TrackFrameDebugGizmoBuilder
    {
        public static DebugLineSegment[] BuildAxes(TrackFrame frame, double axisLength)
        {
            ValidateAxisLength(axisLength);

            Vector3d origin = frame.Position;

            return new[]
            {
                new DebugLineSegment(origin, origin + (frame.Tangent * axisLength), TrackFrameAxisType.Tangent),
                new DebugLineSegment(origin, origin + (frame.Normal * axisLength), TrackFrameAxisType.Normal),
                new DebugLineSegment(origin, origin + (frame.Binormal * axisLength), TrackFrameAxisType.Binormal)
            };
        }

        public static DebugLineSegment[] BuildAxesAtDistance(TrackEvaluator evaluator, double distance, double axisLength)
        {
            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            TrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
            return BuildAxes(frame, axisLength);
        }

        private static void ValidateAxisLength(double axisLength)
        {
            if (double.IsNaN(axisLength) || double.IsInfinity(axisLength) || axisLength <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(axisLength),
                    axisLength,
                    "Axis length must be finite and greater than zero.");
            }
        }
    }
}
