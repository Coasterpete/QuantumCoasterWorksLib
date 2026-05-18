using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Snapshot of a complete evaluated coaster train pose.
    /// </summary>
    /// <remarks>
    /// This is the in-memory handoff from <see cref="TrainCarTransformProvider.EvaluateTrainPose"/>
    /// to debug, Unity, physics, and export adapters.
    /// </remarks>
    public sealed class TrainPoseResult
    {
        private readonly ArticulatedTrainCarWithWheelsTransform[]? _carsSnapshot;
        private readonly IReadOnlyList<ArticulatedTrainCarWithWheelsTransform>? _carsReadOnly;

        /// <summary>
        /// Creates an immutable train-pose snapshot over the evaluated car hierarchy.
        /// </summary>
        public TrainPoseResult(
            double leadDistance,
            TrainConsistDefinition definition,
            ArticulatedTrainCarWithWheelsTransform[] cars)
        {
            LeadDistance = leadDistance;
            Definition = definition;
            _carsSnapshot = CopyArray(cars);
            _carsReadOnly = _carsSnapshot == null ? null : Array.AsReadOnly(_carsSnapshot);
        }

        /// <summary>
        /// Lead-car station distance used to evaluate this pose.
        /// </summary>
        public double LeadDistance { get; }

        /// <summary>
        /// Train consist definition used to evaluate this pose.
        /// </summary>
        public TrainConsistDefinition Definition { get; }

        /// <summary>
        /// Copy of the evaluated car hierarchy.
        /// </summary>
        public ArticulatedTrainCarWithWheelsTransform[] Cars => CopyArray(_carsSnapshot)!;

        /// <summary>
        /// Read-only view of the evaluated car hierarchy.
        /// </summary>
        public IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> CarsReadOnly => _carsReadOnly!;

        private static ArticulatedTrainCarWithWheelsTransform[]? CopyArray(
            ArticulatedTrainCarWithWheelsTransform[]? source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new ArticulatedTrainCarWithWheelsTransform[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
