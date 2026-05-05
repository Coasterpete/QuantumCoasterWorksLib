using System;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Renderer-agnostic line segment for debug visualization.
    /// </summary>
    public readonly struct DebugLineSegment
    {
        public DebugLineSegment(Vector3d start, Vector3d end, TrackFrameAxisType axisType)
        {
            ValidateFinite(start, nameof(start));
            ValidateFinite(end, nameof(end));

            Start = start;
            End = end;
            AxisType = axisType;
        }

        public Vector3d Start { get; }

        public Vector3d End { get; }

        public TrackFrameAxisType AxisType { get; }

        private static void ValidateFinite(Vector3d vector, string paramName)
        {
            if (double.IsNaN(vector.X) ||
                double.IsNaN(vector.Y) ||
                double.IsNaN(vector.Z) ||
                double.IsInfinity(vector.X) ||
                double.IsInfinity(vector.Y) ||
                double.IsInfinity(vector.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, "Vector must contain finite components.");
            }
        }
    }
}
