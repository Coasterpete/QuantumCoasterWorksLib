using System;
using System.Collections.Generic;
using Quantum.Track.Internal;

namespace Quantum.Track
{
    /// <summary>
    /// Computes per-car frames and transform matrices from a lead-car distance.
    /// </summary>
    public sealed class TrainCarTransformProvider
    {
        private readonly TrackEvaluator _evaluator;
        private readonly TrainCarBodySampler _bodySampler;
        private readonly TrainBogieTransformSolver _bogieSolver;
        private readonly TrainArticulationFrameSolver _articulationSolver;
        private readonly TrainWheelTransformLayoutSolver _wheelLayoutSolver;

        public TrainCarTransformProvider(TrackEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _bodySampler = new TrainCarBodySampler(_evaluator);
            _bogieSolver = new TrainBogieTransformSolver(_evaluator);
            _articulationSolver = new TrainArticulationFrameSolver();
            _wheelLayoutSolver = new TrainWheelTransformLayoutSolver();
        }

        /// <summary>
        /// Computes per-car frames and body transform matrices from a lead-car distance.
        /// </summary>
        public IReadOnlyList<TrainCarTransform> GetCarTransforms(
            double leadDistance,
            double carSpacing,
            int carCount)
        {
            return _bodySampler.SampleBodies(leadDistance, carSpacing, carCount);
        }

        /// <summary>
        /// Alias for <see cref="GetCarTransforms(double, double, int)"/> to align naming with other
        /// <c>Evaluate*</c> provider APIs.
        /// </summary>
        public IReadOnlyList<TrainCarTransform> EvaluateCarTransforms(
            double leadDistance,
            double carSpacing,
            int carCount)
        {
            return GetCarTransforms(leadDistance, carSpacing, carCount);
        }

        public IReadOnlyList<TrainCarWithBogiesTransform> EvaluateTrainWithBogies(
            double leadDistance,
            TrainConsistDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return EvaluateTrainWithBogies(
                leadDistance,
                definition.CarCount,
                definition.CarSpacing,
                definition.BogieSpacing);
        }

        public IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> EvaluateTrainWithBogiesAndWheels(
            double leadDistance,
            TrainConsistDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            TrainWheelLayout? wheelLayout = definition.WheelLayout;
            if (wheelLayout == null)
            {
                throw new InvalidOperationException("Wheel layout is required to evaluate wheel transforms.");
            }

            IReadOnlyList<TrainCarWithBogiesTransform> carsWithBogies = EvaluateTrainWithBogies(
                leadDistance,
                definition);
            return _wheelLayoutSolver.AttachWheels(carsWithBogies, wheelLayout);
        }

        public IReadOnlyList<ArticulatedTrainCarTransform> EvaluateArticulatedTrain(
            double leadDistance,
            TrainConsistDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            IReadOnlyList<TrainCarWithBogiesTransform> carsWithBogies = EvaluateTrainWithBogies(
                leadDistance,
                definition);
            return _articulationSolver.SolveArticulatedBodies(carsWithBogies);
        }

        public IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> EvaluateArticulatedTrainWithWheels(
            double leadDistance,
            TrainConsistDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.WheelLayout == null)
            {
                throw new InvalidOperationException("Wheel layout is required to evaluate wheel transforms.");
            }

            IReadOnlyList<ArticulatedTrainCarTransform> articulatedCars = EvaluateArticulatedTrain(
                leadDistance,
                definition);
            IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> carsWithWheels = EvaluateTrainWithBogiesAndWheels(
                leadDistance,
                definition);

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

        public TrainPoseResult EvaluateTrainPose(
            double leadDistance,
            TrainConsistDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> evaluatedCars = EvaluateArticulatedTrainWithWheels(
                leadDistance,
                definition);
            var cars = new ArticulatedTrainCarWithWheelsTransform[evaluatedCars.Count];

            for (int i = 0; i < evaluatedCars.Count; i++)
            {
                cars[i] = evaluatedCars[i];
            }

            return new TrainPoseResult(leadDistance, definition, cars);
        }

        public IReadOnlyList<TrainCarWithBogiesTransform> EvaluateTrainWithBogies(
            double leadDistance,
            int carCount,
            double carSpacing,
            double bogieSpacing)
        {
            if (double.IsNaN(bogieSpacing) || double.IsInfinity(bogieSpacing) || bogieSpacing < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bogieSpacing),
                    bogieSpacing,
                    "Bogie spacing must be finite and non-negative.");
            }

            IReadOnlyList<TrainCarTransform> bodyTransforms = GetCarTransforms(
                leadDistance,
                carSpacing,
                carCount);

            return _bogieSolver.SolveBogies(bodyTransforms, bogieSpacing);
        }

    }
}
