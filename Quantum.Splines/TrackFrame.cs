using System;
using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Orthonormal support-layer frame sampled along a curve or track segment.
    /// </summary>
    [Obsolete("Use CurveFrame for generic spline sampling or Quantum.Track.TrackFrame for coaster-facing APIs.")]
    public readonly struct TrackFrame : ITrackFrameBasis
    {
        /// <summary>
        /// Support-layer arc-length coordinate supplied by the producer. When this
        /// frame is produced from TrackEvaluator compatibility overloads, this value
        /// is segment-local distance rather than public global station distance.
        /// </summary>
        public double S { get; }

        public Vector3d Position { get; }

        public Vector3d Tangent { get; }

        public Vector3d Normal { get; }

        public Vector3d Binormal { get; }

        // Backward-compatible aliases for existing integrations.
        public Vector3d Right => Binormal;
        public Vector3d Up => Normal;

        public TrackFrame(double s, Vector3d position, Vector3d tangent, Vector3d normal, Vector3d binormal)
        {
            S = s;
            Position = position;
            Tangent = tangent;
            Normal = normal;
            Binormal = binormal;
        }

        internal TrackFrame(CurveFrame frame)
            : this(frame.S, frame.Position, frame.Tangent, frame.Normal, frame.Binormal)
        {
        }

        public override string ToString()
        {
            return $"s={S}, Pos={Position}, Tan={Tangent}, N={Normal}, B={Binormal}";
        }
    }
}
