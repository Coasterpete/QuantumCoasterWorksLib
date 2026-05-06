using System.Collections.Generic;

namespace Quantum.Track
{
    public sealed class TrainPoseResult
    {
        public TrainPoseResult(
            double leadDistance,
            TrainConsistDefinition definition,
            ArticulatedTrainCarWithWheelsTransform[] cars)
        {
            LeadDistance = leadDistance;
            Definition = definition;
            Cars = cars;
        }

        public double LeadDistance { get; }

        public TrainConsistDefinition Definition { get; }

        public ArticulatedTrainCarWithWheelsTransform[] Cars { get; }

        public IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> CarsReadOnly => Cars;
    }
}
