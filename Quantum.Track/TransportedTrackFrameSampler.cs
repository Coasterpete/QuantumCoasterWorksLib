using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Compatibility entrypoint for explicit transported-frame sampling.
    /// Production distance-based evaluation now uses the same canonical transport
    /// history through <see cref="TrackEvaluator"/>.
    /// </summary>
    [Obsolete("Use TrackEvaluator.EvaluateFrameAtDistance/EvaluateFramesAtDistances; canonical evaluation now uses transported frames.")]
    public static class TransportedTrackFrameSampler
    {
        public static TrackFrame[] SampleFramesAtDistances(
            TrackDocument document,
            IReadOnlyList<double> distances)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return SampleFramesAtDistances(document, new TrackEvaluator(document), distances);
        }

        public static TrackFrame[] SampleFramesAtDistances(
            TrackDocument document,
            TrackEvaluator evaluator,
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

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            ValidateOrderedDistances(distances);
            return evaluator.EvaluateTrackFramesAtDistances(document, distances);
        }

        private static void ValidateOrderedDistances(IReadOnlyList<double> distances)
        {
            double previousDistance = double.NegativeInfinity;

            for (int i = 0; i < distances.Count; i++)
            {
                double distance = distances[i];
                if (double.IsNaN(distance) || double.IsInfinity(distance))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(distances),
                        distance,
                        $"Distance at index {i} must be finite.");
                }

                if (distance < previousDistance)
                {
                    throw new ArgumentException(
                        $"Distances must be in non-decreasing station order. Distance at index {i} is less than the previous distance.",
                        nameof(distances));
                }

                previousDistance = distance;
            }
        }
    }
}
