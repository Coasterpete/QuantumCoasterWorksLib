using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Compatibility facade for the former generic spline frame sampler.
    /// </summary>
    [Obsolete("Use CurveFrameSampler for generic spline sampling.")]
    public sealed class TrackFrameSampler
    {
        private readonly CurveFrameSampler _sampler;

        public TrackFrameSampler(IArcLengthCurve curve)
            : this(curve, Vector3d.UnitY)
        {
        }

        public TrackFrameSampler(IArcLengthCurve curve, Vector3d referenceNormal)
        {
            _sampler = new CurveFrameSampler(curve, referenceNormal);
        }

        public TrackFrame GetFrameAt(double s)
        {
            return ToCompatibilityFrame(_sampler.GetFrameAt(s));
        }

        public static ArcLengthSample SampleByLength(IArcLengthCurve curve, double s)
        {
            return CurveFrameSampler.SampleByLength(curve, s);
        }

        public static TrackFrame GetFrameAt(IArcLengthCurve curve, double s)
        {
            return ToCompatibilityFrame(CurveFrameSampler.GetFrameAt(curve, s));
        }

        public static TrackFrame SampleFrameByLength(
            IArcLengthCurve curve,
            double s,
            Vector3d referenceUp)
        {
            return ToCompatibilityFrame(
                CurveFrameSampler.SampleFrameByLength(curve, s, referenceUp));
        }

        public static List<TrackFrame> SampleFramesUniform(
            IArcLengthCurve curve,
            double stepLength,
            Vector3d referenceUp)
        {
            List<CurveFrame> curveFrames = CurveFrameSampler.SampleFramesUniform(
                curve,
                stepLength,
                referenceUp);
            var frames = new List<TrackFrame>(curveFrames.Count);

            for (int i = 0; i < curveFrames.Count; i++)
            {
                frames.Add(ToCompatibilityFrame(curveFrames[i]));
            }

            return frames;
        }

        private static TrackFrame ToCompatibilityFrame(CurveFrame frame)
        {
            return new TrackFrame(
                frame.S,
                frame.Position,
                frame.Tangent,
                frame.Normal,
                frame.Binormal);
        }
    }
}
