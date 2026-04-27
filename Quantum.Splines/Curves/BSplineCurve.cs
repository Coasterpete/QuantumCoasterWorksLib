using Quantum.Math;
using System;
using System.Collections.Generic;

namespace Quantum.Splines
{
    public class BSplineCurve : IParamCurve
    {
        private readonly List<Vector3d> _controlPoints;
        private readonly List<double> _knots;
        private readonly int _degree;

        public BSplineCurve(List<Vector3d> controlPoints, int degree)
        {
            if (controlPoints == null || controlPoints.Count < degree + 1)
                throw new ArgumentException("Insufficient control points for given degree.");

            _controlPoints = controlPoints;
            _degree = degree;
            _knots = GenerateOpenUniformKnots(controlPoints.Count, degree);
        }

        public Vector3d Evaluate(double t)
        {
            // Clamp parameter safely
            t = System.Math.Clamp(t, 0.0, 1.0);

            int span = FindSpan(t);
            double[] basis = ComputeBasisFunctions(span, t);

            Vector3d result = Vector3d.Zero;

            for (int j = 0; j <= _degree; j++)
            {
                int controlIndex = span - _degree + j;
                result += basis[j] * _controlPoints[controlIndex];
            }

            return result;
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
                    $"Unable to compute B-spline tangent at t={t:0.######}: derivative magnitude is near zero.");

            return direction.Normalized();
        }

        private List<double> GenerateOpenUniformKnots(int controlPointCount, int degree)
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

        private int FindSpan(double t)
        {
            int n = _controlPoints.Count - 1;

            if (t >= _knots[n + 1])
                return n;

            if (t <= _knots[_degree])
                return _degree;

            int low = _degree;
            int high = n + 1;
            int mid = (low + high) / 2;

            while (t < _knots[mid] || t >= _knots[mid + 1])
            {
                if (t < _knots[mid])
                    high = mid;
                else
                    low = mid;

                mid = (low + high) / 2;
            }

            return mid;
        }

        private double[] ComputeBasisFunctions(int span, double t)
        {
            int p = _degree;
            double[] N = new double[p + 1];
            double[] left = new double[p + 1];
            double[] right = new double[p + 1];

            N[0] = 1.0;

            for (int j = 1; j <= p; j++)
            {
                left[j] = t - _knots[span + 1 - j];
                right[j] = _knots[span + j] - t;

                double saved = 0.0;

                for (int r = 0; r < j; r++)
                {
                    double denom = right[r + 1] + left[j - r];
                    double temp = denom == 0.0 ? 0.0 : N[r] / denom;

                    N[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }

                N[j] = saved;
            }

            return N;
        }
    }
}
