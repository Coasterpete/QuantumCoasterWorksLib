using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Samples opt-in heartline/rider-reference points from existing track frames.
    /// </summary>
    /// <remarks>
    /// Heartline sampling uses centerline station distance. It does not create a
    /// heartline arc-length domain and does not change default evaluator or train
    /// placement behavior.
    /// </remarks>
    public static class HeartlineSampler
    {
        public static HeartlineFrame SampleAtDistance(
            TrackEvaluator evaluator,
            HeartlineOffset offset,
            double distance)
        {
            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            TrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
            return CreateHeartlineFrame(frame, offset);
        }

        public static HeartlineFrame[] SampleAtDistances(
            TrackEvaluator evaluator,
            HeartlineOffset offset,
            IReadOnlyList<double> distances)
        {
            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            if (distances.Count == 0)
            {
                return Array.Empty<HeartlineFrame>();
            }

            TrackFrame[] frames = evaluator.EvaluateFramesAtDistances(distances);
            return CreateHeartlineFrames(frames, offset);
        }

        public static HeartlineFrame[] SampleAtDistances(
            TrackEvaluator evaluator,
            BankingProfile bankingProfile,
            HeartlineOffset offset,
            IReadOnlyList<double> distances)
        {
            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (bankingProfile is null)
            {
                throw new ArgumentNullException(nameof(bankingProfile));
            }

            if (distances is null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            if (distances.Count == 0)
            {
                return Array.Empty<HeartlineFrame>();
            }

            TrackFrame[] frames = BankingProfileSampler.SampleFramesAtDistances(
                evaluator,
                bankingProfile,
                distances);
            return CreateHeartlineFrames(frames, offset);
        }

        private static HeartlineFrame[] CreateHeartlineFrames(
            IReadOnlyList<TrackFrame> frames,
            HeartlineOffset offset)
        {
            var heartlineFrames = new HeartlineFrame[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                heartlineFrames[i] = CreateHeartlineFrame(frames[i], offset);
            }

            return heartlineFrames;
        }

        private static HeartlineFrame CreateHeartlineFrame(
            TrackFrame frame,
            HeartlineOffset offset)
        {
            Vector3d position = frame.Position +
                (frame.Normal * offset.NormalOffsetMeters) +
                (frame.Binormal * offset.LateralOffsetMeters);

            return new HeartlineFrame(
                frame.Distance,
                frame.Position,
                position,
                frame.Tangent,
                frame.Normal,
                frame.Binormal);
        }
    }
}
