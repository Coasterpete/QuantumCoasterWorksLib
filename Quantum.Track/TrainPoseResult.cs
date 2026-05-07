using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public sealed class TrainPoseResult
    {
        private readonly ArticulatedTrainCarWithWheelsTransform[]? _carsSnapshot;
        private readonly IReadOnlyList<ArticulatedTrainCarWithWheelsTransform>? _carsReadOnly;

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

        public double LeadDistance { get; }

        public TrainConsistDefinition Definition { get; }

        public ArticulatedTrainCarWithWheelsTransform[] Cars => CopyArray(_carsSnapshot)!;

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
