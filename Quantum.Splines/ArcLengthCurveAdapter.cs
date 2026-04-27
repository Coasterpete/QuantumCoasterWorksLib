using System;
using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Adapts any parametric curve to arc-length evaluation using a lookup table.
    /// </summary>
    public sealed class ArcLengthCurveAdapter : IArcLengthCurve
    {
        private readonly IParamCurve _curve;
        private readonly ArcLengthLUT _lut;

        public ArcLengthCurveAdapter(IParamCurve curve, int samples = 100)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));

            if (samples < 2)
                throw new ArgumentOutOfRangeException(nameof(samples), "Sample count must be at least 2.");

            _curve = curve;
            _lut = new ArcLengthLUT(curve, samples);
        }

        public double Length => _lut.TotalLength;

        public Vector3d Evaluate(double t)
        {
            return _curve.Evaluate(t);
        }

        public Vector3d Tangent(double t)
        {
            return _curve.Tangent(t);
        }

        public Vector3d EvaluateByLength(double s)
        {
            double t = MapLengthToT(s);
            return _curve.Evaluate(t);
        }

        public Vector3d TangentByLength(double s)
        {
            double t = MapLengthToT(s);
            return _curve.Tangent(t);
        }

        private double MapLengthToT(double s)
        {
            if (Length <= MathUtil.Epsilon)
                return 0.0;

            double clampedS = MathUtil.Clamp(s, 0.0, Length);
            return _lut.MapS2T(clampedS);
        }
    }
}
