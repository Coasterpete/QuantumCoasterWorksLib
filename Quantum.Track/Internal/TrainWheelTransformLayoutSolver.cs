using System.Collections.Generic;

namespace Quantum.Track.Internal
{
    internal sealed class TrainWheelTransformLayoutSolver
    {
        public WheelTransform[] BuildWheelTransforms(BogieTransform bogie, TrainWheelLayout wheelLayout)
        {
            int wheelCount = wheelLayout.WheelCountPerBogie;
            int axleCount = (wheelCount + 1) / 2;
            double centeredAxleOffset = (axleCount - 1) * 0.5;
            double sideOffsetMagnitude = wheelLayout.WheelWidth * 0.5;
            var wheels = new WheelTransform[wheelCount];

            for (int i = 0; i < wheelCount; i++)
            {
                int axleIndex = i / 2;
                double localOffsetX = (axleIndex - centeredAxleOffset) * wheelLayout.AxleSpacing;
                double localOffsetY = (i % 2 == 0 ? -1.0 : 1.0) * sideOffsetMagnitude;
                const double localOffsetZ = 0.0;

                wheels[i] = new WheelTransform(
                    bogie.CarIndex,
                    bogie.BogieIndex,
                    i,
                    localOffsetX,
                    localOffsetY,
                    localOffsetZ,
                    bogie.Frame,
                    bogie.Matrix);
            }

            return wheels;
        }

        public IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> AttachWheels(
            IReadOnlyList<TrainCarWithBogiesTransform> carsWithBogies,
            TrainWheelLayout wheelLayout)
        {
            var transforms = new List<TrainCarWithBogiesAndWheelsTransform>(carsWithBogies.Count);

            for (int i = 0; i < carsWithBogies.Count; i++)
            {
                TrainCarWithBogiesTransform carWithBogies = carsWithBogies[i];
                var frontBogie = new TrainBogieWithWheelsTransform(
                    carWithBogies.FrontBogie,
                    BuildWheelTransforms(carWithBogies.FrontBogie, wheelLayout));
                var rearBogie = new TrainBogieWithWheelsTransform(
                    carWithBogies.RearBogie,
                    BuildWheelTransforms(carWithBogies.RearBogie, wheelLayout));

                transforms.Add(new TrainCarWithBogiesAndWheelsTransform(
                    carWithBogies.Body,
                    frontBogie,
                    rearBogie));
            }

            return transforms;
        }
    }
}
