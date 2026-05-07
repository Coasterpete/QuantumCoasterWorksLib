using System;
using System.Collections.Generic;

namespace Quantum.Track.Internal
{
    internal sealed class TrainCarBodySampler
    {
        private readonly TrackEvaluator _evaluator;

        public TrainCarBodySampler(TrackEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public IReadOnlyList<TrainCarTransform> SampleBodies(
            double leadDistance,
            double carSpacing,
            int carCount)
        {
            if (double.IsNaN(leadDistance) || double.IsInfinity(leadDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leadDistance),
                    leadDistance,
                    "Lead distance must be finite.");
            }

            if (double.IsNaN(carSpacing) || double.IsInfinity(carSpacing) || carSpacing < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carSpacing),
                    carSpacing,
                    "Car spacing must be finite and non-negative.");
            }

            if (carCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carCount),
                    carCount,
                    "Car count must be non-negative.");
            }

            double totalLength = _evaluator.GetBoundTrackTotalLength();
            ValidateDistanceInRange(leadDistance, totalLength, "Lead car distance is out of range.");

            var transforms = new List<TrainCarTransform>(carCount);

            for (int i = 0; i < carCount; i++)
            {
                double distance = leadDistance - (i * carSpacing);
                ValidateDistanceInRange(
                    distance,
                    totalLength,
                    $"Computed distance for car {i} is out of range.");

                TrackFrame frame = _evaluator.EvaluateFrameAtDistance(distance);
                transforms.Add(new TrainCarTransform(i, distance, frame, frame.ToMatrix4x4()));
            }

            return transforms;
        }

        private static void ValidateDistanceInRange(double distance, double maxDistance, string message)
        {
            if (distance < 0.0 || distance > maxDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance),
                    distance,
                    $"{message} Valid range is [0.0, {maxDistance}].");
            }
        }
    }
}
