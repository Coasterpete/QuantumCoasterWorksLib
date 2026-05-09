using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track.Internal
{
    internal sealed class TrainBogieTransformSolver
    {
        private readonly TrackEvaluator _evaluator;

        public TrainBogieTransformSolver(TrackEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public IReadOnlyList<TrainCarWithBogiesTransform> SolveBogies(
            IReadOnlyList<TrainCarTransform> bodies,
            double bogieSpacing)
        {
            double totalLength = _evaluator.GetBoundTrackTotalLength();
            double bogieHalfSpacing = bogieSpacing * 0.5;
            var transforms = new List<TrainCarWithBogiesTransform>(bodies.Count);
            var distances = new double[bodies.Count * 2];

            for (int i = 0; i < bodies.Count; i++)
            {
                TrainCarTransform body = bodies[i];
                int frontIndex = i * 2;

                double frontDistance = body.Distance + bogieHalfSpacing;
                ValidateDistanceInRange(
                    frontDistance,
                    totalLength,
                    $"Computed front bogie distance for car {body.CarIndex} is out of range.");
                distances[frontIndex] = frontDistance;

                double rearDistance = body.Distance - bogieHalfSpacing;
                ValidateDistanceInRange(
                    rearDistance,
                    totalLength,
                    $"Computed rear bogie distance for car {body.CarIndex} is out of range.");
                distances[frontIndex + 1] = rearDistance;
            }

            TrackFrame[] frames = _evaluator.EvaluateFramesAtDistances(distances);

            for (int i = 0; i < bodies.Count; i++)
            {
                TrainCarTransform body = bodies[i];
                int frontIndex = i * 2;
                int rearIndex = frontIndex + 1;

                TrackFrame frontFrame = frames[frontIndex];
                var frontBogie = new BogieTransform(
                    body.CarIndex,
                    bogieIndex: 0,
                    distances[frontIndex],
                    frontFrame,
                    Matrix4x4d.FromMatrix4x4(frontFrame.ToMatrix4x4()));

                TrackFrame rearFrame = frames[rearIndex];
                var rearBogie = new BogieTransform(
                    body.CarIndex,
                    bogieIndex: 1,
                    distances[rearIndex],
                    rearFrame,
                    Matrix4x4d.FromMatrix4x4(rearFrame.ToMatrix4x4()));

                transforms.Add(new TrainCarWithBogiesTransform(body, frontBogie, rearBogie));
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
