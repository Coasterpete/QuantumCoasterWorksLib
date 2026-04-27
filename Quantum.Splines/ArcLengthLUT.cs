using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Lookup table for approximating arc-length parameterization.
    /// </summary>
    public class ArcLengthLUT
    {
        private readonly List<double> _tValues = new List<double>();
        private readonly List<double> _sValues = new List<double>();

        public double TotalLength { get; private set; }

        public ArcLengthLUT(IParamCurve curve, int samples = 100)
        {
            Build(curve, samples);
        }

        private void Build(IParamCurve curve, int samples)
        {
            _tValues.Clear();
            _sValues.Clear();

            Vector3d prev = curve.Evaluate(0.0);
            double s = 0.0;

            _tValues.Add(0.0);
            _sValues.Add(0.0);

            for (int i = 1; i <= samples; i++)
            {
                double t = (double)i / samples;
                Vector3d p = curve.Evaluate(t);

                s += (p - prev).Length;
                prev = p;

                _tValues.Add(t);
                _sValues.Add(s);
            }

            TotalLength = s;
        }

        public double MapS2T(double s)
        {
            if (s <= 0.0) return 0.0;
            if (s >= TotalLength) return 1.0;

            for (int i = 1; i < _sValues.Count; i++)
            {
                if (_sValues[i] >= s)
                {
                    double s0 = _sValues[i - 1];
                    double s1 = _sValues[i];
                    double t0 = _tValues[i - 1];
                    double t1 = _tValues[i];

                    double denom = s1 - s0;
                    if (System.Math.Abs(denom) <= MathUtil.Epsilon)
                        return t0;

                    double alpha = (s - s0) / denom;
                    return MathUtil.Lerp(t0, t1, alpha);
                }
            }

            return 1.0;
        }
    }
}
