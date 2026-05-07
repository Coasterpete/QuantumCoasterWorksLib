using System;
using System.Collections.Generic;

namespace Quantum.Track.Internal
{
    internal sealed class TrainPoseAssembler
    {
        public IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> AssembleArticulatedWithWheels(
            IReadOnlyList<ArticulatedTrainCarTransform> articulatedCars,
            IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> carsWithWheels)
        {
            if (articulatedCars.Count != carsWithWheels.Count)
            {
                throw new InvalidOperationException("Articulated and wheel evaluations returned mismatched car counts.");
            }

            var transforms = new List<ArticulatedTrainCarWithWheelsTransform>(articulatedCars.Count);

            for (int i = 0; i < articulatedCars.Count; i++)
            {
                transforms.Add(new ArticulatedTrainCarWithWheelsTransform(
                    articulatedCars[i],
                    carsWithWheels[i].FrontBogie,
                    carsWithWheels[i].RearBogie));
            }

            return transforms;
        }

        public TrainPoseResult BuildPoseResult(
            double leadDistance,
            TrainConsistDefinition definition,
            IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars)
        {
            var snapshot = new ArticulatedTrainCarWithWheelsTransform[cars.Count];

            for (int i = 0; i < cars.Count; i++)
            {
                snapshot[i] = cars[i];
            }

            return new TrainPoseResult(leadDistance, definition, snapshot);
        }
    }
}
