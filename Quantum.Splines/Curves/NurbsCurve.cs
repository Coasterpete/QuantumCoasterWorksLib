using Quantum.Math;
using System;
using System.Collections.Generic;

namespace Quantum.Splines
{
    public sealed class NurbsCurve : IParamCurve
    {
        private readonly List<Vector3d> _controlPoints;
        private readonly List<double> _weights;
        private readonly List<double> _knots;
        private readonly int _degree;
        private readonly double _domainStart;
        private readonly double _domainEnd;

        public NurbsCurve(List<Vector3d> controlPoints, List<double> weights, int degree)
            : this(controlPoints, weights, degree, GenerateOpenUniformKnots(controlPoints?.Count ?? 0, degree))
        {
        }

        public NurbsCurve(
            List<Vector3d> controlPoints,
            List<double> weights,
            int degree,
            List<double> knots)
        {
            if (controlPoints == null)
                throw new ArgumentNullException(nameof(controlPoints));

            if (weights == null)
                throw new ArgumentNullException(nameof(weights));

            if (knots == null)
                throw new ArgumentNullException(nameof(knots));

            if (degree < 1)
                throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be at least 1.");

            if (controlPoints.Count < degree + 1)
                throw new ArgumentException("Insufficient control points for given degree.", nameof(controlPoints));

            if (weights.Count != controlPoints.Count)
                throw new ArgumentException("Weight count must match control point count.", nameof(weights));

            ValidateWeights(weights);
            ValidateKnots(knots, controlPoints.Count, degree);

            _controlPoints = controlPoints;
            _weights = weights;
            _degree = degree;
            _knots = knots;

            _domainStart = _knots[_degree];
            _domainEnd = _knots[_controlPoints.Count];

            if (double.IsNaN(_domainStart) || double.IsInfinity(_domainStart) ||
                double.IsNaN(_domainEnd) || double.IsInfinity(_domainEnd) ||
                _domainEnd - _domainStart <= MathUtil.Epsilon)
            {
                throw new ArgumentException("Knot domain is degenerate for the given degree/control points.", nameof(knots));
            }
        }

        public Vector3d Evaluate(double t)
        {
            // Clamp parameter safely
            t = System.Math.Clamp(t, 0.0, 1.0);
            double u = MapToKnotDomain(t);

            int span = FindSpan(u);
            double[] basis = ComputeBasisFunctions(span, u);

            Vector3d numerator = Vector3d.Zero;
            double denominator = 0.0;

            for (int j = 0; j <= _degree; j++)
            {
                int controlIndex = span - _degree + j;
                double weightedBasis = basis[j] * _weights[controlIndex];
                numerator += weightedBasis * _controlPoints[controlIndex];
                denominator += weightedBasis;
            }

            if (double.IsNaN(denominator) || double.IsInfinity(denominator) ||
                System.Math.Abs(denominator) <= MathUtil.Epsilon)
            {
                throw new InvalidOperationException(
                    $"Unable to evaluate NURBS curve at t={t:0.######}: rational denominator is invalid.");
            }

            return numerator / denominator;
        }

        public Vector3d Tangent(double t)
        {
            t = System.Math.Clamp(t, 0.0, 1.0);

            const double delta = 1e-5;

            double t0;
            double t1;

            if (t <= delta)
            {
                // Forward difference near start.
                t0 = t;
                t1 = System.Math.Min(1.0, t + delta);
            }
            else if (t >= 1.0 - delta)
            {
                // Backward difference near end.
                t0 = System.Math.Max(0.0, t - delta);
                t1 = t;
            }
            else
            {
                // Central difference in the interior.
                t0 = t - delta;
                t1 = t + delta;
            }

            Vector3d p0 = Evaluate(t0);
            Vector3d p1 = Evaluate(t1);
            Vector3d direction = p1 - p0;

            if (direction.Length <= MathUtil.Epsilon)
                throw new InvalidOperationException(
                    $"Unable to compute NURBS tangent at t={t:0.######}: derivative magnitude is near zero.");

            return direction.Normalized();
        }

        private static void ValidateWeights(List<double> weights)
        {
            for (int i = 0; i < weights.Count; i++)
            {
                double w = weights[i];
                if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.0)
                {
                    throw new ArgumentException(
                        $"Weight at index {i} must be a positive finite value.",
                        nameof(weights));
                }
            }
        }

        private static void ValidateKnots(List<double> knots, int controlPointCount, int degree)
        {
            int expectedCount = controlPointCount + degree + 1;
            if (knots.Count != expectedCount)
            {
                throw new ArgumentException(
                    $"Knot count must be {expectedCount} for {controlPointCount} control points and degree {degree}.",
                    nameof(knots));
            }

            for (int i = 0; i < knots.Count; i++)
            {
                double k = knots[i];
                if (double.IsNaN(k) || double.IsInfinity(k))
                    throw new ArgumentException($"Knot at index {i} must be finite.", nameof(knots));

                if (i > 0 && knots[i] < knots[i - 1])
                    throw new ArgumentException("Knot sequence must be nondecreasing.", nameof(knots));
            }
        }

        private double MapToKnotDomain(double t)
        {
            return _domainStart + ((_domainEnd - _domainStart) * t);
        }

        private static List<double> GenerateOpenUniformKnots(int controlPointCount, int degree)
        {
            int knotCount = controlPointCount + degree + 1;
            var knots = new List<double>(knotCount);

            for (int i = 0; i < knotCount; i++)
            {
                if (i <= degree)
                    knots.Add(0.0);
                else if (i >= knotCount - degree - 1)
                    knots.Add(1.0);
                else
                    knots.Add((double)(i - degree) / (knotCount - 2 * degree - 1));
            }

            return knots;
        }

        private int FindSpan(double u)
        {
            int n = _controlPoints.Count - 1;

            if (u >= _knots[n + 1])
                return n;

            if (u <= _knots[_degree])
                return _degree;

            int low = _degree;
            int high = n + 1;
            int mid = (low + high) / 2;

            while (u < _knots[mid] || u >= _knots[mid + 1])
            {
                if (u < _knots[mid])
                    high = mid;
                else
                    low = mid;

                mid = (low + high) / 2;
            }

            return mid;
        }

        private double[] ComputeBasisFunctions(int span, double u)
        {
            int p = _degree;
            double[] n = new double[p + 1];
            double[] left = new double[p + 1];
            double[] right = new double[p + 1];

            n[0] = 1.0;

            for (int j = 1; j <= p; j++)
            {
                left[j] = u - _knots[span + 1 - j];
                right[j] = _knots[span + j] - u;

                double saved = 0.0;

                for (int r = 0; r < j; r++)
                {
                    double denom = right[r + 1] + left[j - r];
                    double temp = denom == 0.0 ? 0.0 : n[r] / denom;

                    n[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }

                n[j] = saved;
            }

            return n;
        }
    }
}
