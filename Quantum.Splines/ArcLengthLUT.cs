using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Splines
{
    /// <summary>
    /// Lookup table for approximating arc-length parameterization.
    /// </summary>
    public class ArcLengthLUT
    {
        public const double DefaultTolerance = 1e-4;

        private const int MaxSubdivisionDepth = 20;

        private readonly List<double> _tValues = new List<double>();
        private readonly List<double> _sValues = new List<double>();
        private Vector3d _lastPoint;

        public double TotalLength { get; private set; }
        public double Tolerance { get; }

        public ArcLengthLUT(
            IParamCurve curve,
            int samples = 100,
            double tolerance = DefaultTolerance)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));

            if (samples < 1)
                throw new ArgumentOutOfRangeException(nameof(samples), "Sample count must be at least 1.");

            if (double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be finite and greater than zero.");

            Tolerance = tolerance;
            Build(curve, samples, tolerance);
        }

        private void Build(IParamCurve curve, int samples, double tolerance)
        {
            _tValues.Clear();
            _sValues.Clear();

            TotalLength = 0.0;
            _lastPoint = curve.Evaluate(0.0);

            _tValues.Add(0.0);
            _sValues.Add(0.0);

            double t0 = 0.0;
            Vector3d p0 = _lastPoint;
            double segmentTolerance = tolerance / samples;

            for (int i = 1; i <= samples; i++)
            {
                double t1 = (double)i / samples;
                Vector3d p1 = curve.Evaluate(t1);

                AppendAdaptiveSegment(
                    curve,
                    t0,
                    p0,
                    t1,
                    p1,
                    lengthTolerance: segmentTolerance,
                    parameterizationTolerance: tolerance,
                    depth: 0);

                t0 = t1;
                p0 = p1;
            }
        }

        public double MapS2T(double s)
        {
            if (TotalLength <= MathUtil.Epsilon) return 0.0;
            if (s <= 0.0) return 0.0;
            if (s >= TotalLength) return 1.0;

            int low = 1;
            int high = _sValues.Count - 1;

            while (low < high)
            {
                int mid = low + ((high - low) / 2);
                if (_sValues[mid] < s)
                    low = mid + 1;
                else
                    high = mid;
            }

            int upperIndex = low;
            int lowerIndex = upperIndex - 1;
            double s0 = _sValues[lowerIndex];
            double s1 = _sValues[upperIndex];
            double t0 = _tValues[lowerIndex];
            double t1 = _tValues[upperIndex];

            double denominator = s1 - s0;
            if (System.Math.Abs(denominator) <= MathUtil.Epsilon)
                return t0;

            double alpha = MathUtil.Clamp((s - s0) / denominator, 0.0, 1.0);
            return MathUtil.Lerp(t0, t1, alpha);
        }

        private void AppendAdaptiveSegment(
            IParamCurve curve,
            double t0,
            Vector3d p0,
            double t1,
            Vector3d p1,
            double lengthTolerance,
            double parameterizationTolerance,
            int depth)
        {
            double tRange = t1 - t0;
            double tQuarter = t0 + (tRange * 0.25);
            double tMid = t0 + (tRange * 0.5);
            double tThreeQuarters = t0 + (tRange * 0.75);

            Vector3d pQuarter = curve.Evaluate(tQuarter);
            Vector3d pMid = curve.Evaluate(tMid);
            Vector3d pThreeQuarters = curve.Evaluate(tThreeQuarters);

            double firstLength = (pQuarter - p0).Length;
            double secondLength = (pMid - pQuarter).Length;
            double thirdLength = (pThreeQuarters - pMid).Length;
            double fourthLength = (p1 - pThreeQuarters).Length;
            double refinedLength = firstLength + secondLength + thirdLength + fourthLength;
            double chordLength = (p1 - p0).Length;

            double lengthError = System.Math.Abs(refinedLength - chordLength);
            double parameterizationError = System.Math.Max(
                System.Math.Abs(firstLength - (refinedLength * 0.25)),
                System.Math.Max(
                    System.Math.Abs((firstLength + secondLength) - (refinedLength * 0.5)),
                    System.Math.Abs((firstLength + secondLength + thirdLength) - (refinedLength * 0.75))));

            if (depth < MaxSubdivisionDepth &&
                (lengthError > lengthTolerance || parameterizationError > parameterizationTolerance))
            {
                double childLengthTolerance = lengthTolerance * 0.5;
                AppendAdaptiveSegment(
                    curve,
                    t0,
                    p0,
                    tMid,
                    pMid,
                    childLengthTolerance,
                    parameterizationTolerance,
                    depth + 1);
                AppendAdaptiveSegment(
                    curve,
                    tMid,
                    pMid,
                    t1,
                    p1,
                    childLengthTolerance,
                    parameterizationTolerance,
                    depth + 1);
                return;
            }

            AppendPoint(tQuarter, pQuarter);
            AppendPoint(tMid, pMid);
            AppendPoint(tThreeQuarters, pThreeQuarters);
            AppendPoint(t1, p1);
        }

        private void AppendPoint(double t, Vector3d point)
        {
            TotalLength += (point - _lastPoint).Length;
            _lastPoint = point;

            _tValues.Add(t);
            _sValues.Add(TotalLength);
        }
    }
}
