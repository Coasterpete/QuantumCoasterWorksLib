using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Samples BankingProfile roll values and opt-in profile-banked frames.
    /// </summary>
    public static class BankingProfileSampler
    {
        public static double SampleRollRadians(BankingProfile profile, double distance)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (!IsFinite(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "Distance must be finite.");
            }

            IReadOnlyList<BankingProfileKey> keys = profile.Keys;
            BankingProfileKey firstKey = keys[0];
            if (distance <= firstKey.Distance)
            {
                return firstKey.RollRadians;
            }

            BankingProfileKey lastKey = keys[keys.Count - 1];
            if (distance >= lastKey.Distance)
            {
                return lastKey.RollRadians;
            }

            for (int i = 0; i < keys.Count - 1; i++)
            {
                BankingProfileKey left = keys[i];
                BankingProfileKey right = keys[i + 1];
                if (distance > right.Distance)
                {
                    continue;
                }

                if (distance == right.Distance)
                {
                    return right.RollRadians;
                }

                double t = (distance - left.Distance) / (right.Distance - left.Distance);
                return Interpolate(left.RollRadians, right.RollRadians, t, left.InterpolationToNext);
            }

            return lastKey.RollRadians;
        }

        public static TrackFrame[] SampleFramesAtDistances(
            TrackDocument document,
            BankingProfile profile,
            IReadOnlyList<double> distances)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return SampleFramesAtDistances(document, new TrackEvaluator(document), profile, distances);
        }

        public static TrackFrame[] SampleFramesAtDistances(
            TrackDocument document,
            TrackEvaluator evaluator,
            BankingProfile profile,
            IReadOnlyList<double> distances)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            return TransportedTrackFrameSampler.SampleFramesAtDistances(
                document,
                evaluator,
                distances,
                (index, _) => SampleRollRadians(profile, distances[index]));
        }

        private static double Interpolate(
            double left,
            double right,
            double t,
            BankingProfileInterpolationMode mode)
        {
            switch (mode)
            {
                case BankingProfileInterpolationMode.Constant:
                    return left;

                case BankingProfileInterpolationMode.Linear:
                    return Lerp(left, right, t);

                case BankingProfileInterpolationMode.SmoothStep:
                    double smoothT = t * t * (3.0 - (2.0 * t));
                    return Lerp(left, right, smoothT);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unsupported banking profile interpolation mode.");
            }
        }

        private static double Lerp(double left, double right, double t)
        {
            return left + ((right - left) * t);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
