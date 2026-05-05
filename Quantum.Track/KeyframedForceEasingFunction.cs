using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public sealed class KeyframedForceEasingFunction : IForceEasingFunction
    {
        private readonly List<(double t, double value)> _points;

        public KeyframedForceEasingFunction(List<(double t, double value)> points)
        {
            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(points),
                    points.Count,
                    "At least two keyframed points are required.");
            }

            _points = new List<(double t, double value)>(points);
            _points.Sort((left, right) => left.t.CompareTo(right.t));

            for (int i = 0; i < _points.Count; i++)
            {
                double currentT = _points[i].t;

                if (double.IsNaN(currentT) || double.IsInfinity(currentT) || currentT < 0.0 || currentT > 1.0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(points),
                        currentT,
                        "Point t values must be finite and within [0, 1].");
                }

                if (i > 0 && currentT <= _points[i - 1].t)
                {
                    throw new ArgumentException(
                        "Point t values must be strictly increasing.",
                        nameof(points));
                }
            }
        }

        public double Evaluate(double t)
        {
            double clampedT = t;

            if (clampedT < 0.0)
            {
                clampedT = 0.0;
            }
            else if (clampedT > 1.0)
            {
                clampedT = 1.0;
            }
            int lastIndex = _points.Count - 1;

            if (clampedT <= _points[0].t)
            {
                return _points[0].value;
            }

            if (clampedT >= _points[lastIndex].t)
            {
                return _points[lastIndex].value;
            }

            for (int i = 1; i <= lastIndex; i++)
            {
                (double t, double value) upper = _points[i];

                if (clampedT <= upper.t)
                {
                    (double t, double value) lower = _points[i - 1];
                    double normalizedSegmentT = (clampedT - lower.t) / (upper.t - lower.t);
                    return lower.value + ((upper.value - lower.value) * normalizedSegmentT);
                }
            }

            return _points[lastIndex].value;
        }
    }
}
