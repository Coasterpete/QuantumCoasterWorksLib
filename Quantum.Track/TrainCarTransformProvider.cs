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
        private readonly TrainPoseAssembler _poseAssembler;

        public TrainCarTransformProvider(TrackEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _bodySampler = new TrainCarBodySampler(_evaluator);
            _bogieSolver = new TrainBogieTransformSolver(_evaluator);
            _articulationSolver = new TrainArticulationFrameSolver();
            _wheelLayoutSolver = new TrainWheelTransformLayoutSolver();
            _poseAssembler = new TrainPoseAssembler();
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

            IReadOnlyList<TrainCarWithBogiesTransform> carsWithBogies = EvaluateTrainWithBogies(
                leadDistance,
                definition);
            IReadOnlyList<ArticulatedTrainCarTransform> articulatedCars = _articulationSolver.SolveArticulatedBodies(carsWithBogies);
            IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> carsWithWheels = _wheelLayoutSolver.AttachWheels(
                carsWithBogies,
                definition.WheelLayout);
            return _poseAssembler.AssembleArticulatedWithWheels(articulatedCars, carsWithWheels);
        }

        /// <summary>
        /// Public train-pose entrypoint for distance-based coaster train placement.
        /// </summary>
        /// <remarks>
        /// This method is the stable backend boundary for consumers that need a
        /// complete train pose snapshot. It samples the bound <see cref="TrackEvaluator"/>
        /// using the lead-car station distance and preserves the existing body, bogie,
        /// wheel, and articulated pose semantics.
        /// </remarks>
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
            return _poseAssembler.BuildPoseResult(leadDistance, definition, evaluatedCars);
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
