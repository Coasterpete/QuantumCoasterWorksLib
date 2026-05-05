using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public sealed class KeyframedForceEasingFunction : IForceEasingFunction
    {
        private static readonly IForceEasingFunction LinearSegmentEasing =
            new BuiltInForceEasingFunction(ForceInterpolationMode.Linear);

        private readonly List<(double t, double value)> _points;
        private readonly IReadOnlyList<IForceEasingFunction> _segmentEasings;

        public KeyframedForceEasingFunction(List<(double t, double value)> points)
            : this(points, segmentEasings: null)
        {
        }

        public KeyframedForceEasingFunction(
            List<(double t, double value)> points,
            IReadOnlyList<IForceEasingFunction?>? segmentEasings)
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
                double currentValue = _points[i].value;

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

                if (double.IsNaN(currentValue) || double.IsInfinity(currentValue))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(points),
                        currentValue,
                        "Point values must be finite.");
                }
            }

            int segmentCount = _points.Count - 1;

            if (segmentEasings is null)
            {
                _segmentEasings = CreateLinearSegmentEasings(segmentCount);
                return;
            }

            if (segmentEasings.Count != segmentCount)
            {
                throw new ArgumentException(
                    "Segment easing count must match segment count.",
                    nameof(segmentEasings));
            }

            IForceEasingFunction[] resolvedSegmentEasings = new IForceEasingFunction[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                resolvedSegmentEasings[i] = segmentEasings[i] ?? LinearSegmentEasing;
            }

            _segmentEasings = resolvedSegmentEasings;
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
                    double easedSegmentT = _segmentEasings[i - 1].Evaluate(normalizedSegmentT);
                    return lower.value + ((upper.value - lower.value) * easedSegmentT);
                }
            }

            return _points[lastIndex].value;
        }

        private static IForceEasingFunction[] CreateLinearSegmentEasings(int segmentCount)
        {
            IForceEasingFunction[] segmentEasings = new IForceEasingFunction[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                segmentEasings[i] = LinearSegmentEasing;
            }

            return segmentEasings;
        }
    }
}
