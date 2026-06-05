using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Track.Internal;

namespace Quantum.Track
{
    /// <summary>
    /// Computes per-car frames and transform matrices from a lead-car distance.
    /// </summary>
    /// <remarks>
    /// <c>Evaluate*</c> is the preferred naming direction for deterministic
    /// station-distance train transform computation. Existing <c>Get*</c>
    /// methods remain as compatibility and convenience APIs for callers that
    /// already depend on them.
    /// </remarks>
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
        /// Compatibility/convenience API that computes per-car body frames and
        /// transform matrices from a lead-car station distance.
        /// </summary>
        /// <remarks>
        /// New deterministic station-distance train transform code should prefer
        /// <see cref="EvaluateCarTransforms(double, double, int)"/>. This method
        /// remains available for existing callers and has the same sampling
        /// semantics.
        /// </remarks>
        public IReadOnlyList<TrainCarTransform> GetCarTransforms(
            double leadDistance,
            double carSpacing,
            int carCount)
        {
            return _bodySampler.SampleBodies(leadDistance, carSpacing, carCount);
        }

        /// <summary>
        /// Compatibility/convenience API that computes per-car body frames and
        /// transform matrices using an explicit <see cref="BankingProfile"/> as
        /// the roll source.
        /// </summary>
        /// <remarks>
        /// New deterministic station-distance train transform code should prefer
        /// <see cref="EvaluateCarTransforms(double, double, int, BankingProfile)"/>.
        /// This method remains available for existing callers and has the same
        /// profile-backed sampling semantics.
        /// </remarks>
        public IReadOnlyList<TrainCarTransform> GetCarTransforms(
            double leadDistance,
            double carSpacing,
            int carCount,
            BankingProfile bankingProfile)
        {
            if (bankingProfile == null)
            {
                throw new ArgumentNullException(nameof(bankingProfile));
            }

            return SampleProfileBackedBodies(
                leadDistance,
                carSpacing,
                carCount,
                bankingProfile);
        }

        /// <summary>
        /// Preferred API for evaluating per-car body frames and transform
        /// matrices from a lead-car station distance.
        /// </summary>
        /// <remarks>
        /// This method keeps deterministic station-distance computation under the
        /// <c>Evaluate*</c> naming convention and delegates to the compatibility
        /// <see cref="GetCarTransforms(double, double, int)"/> implementation.
        /// </remarks>
        public IReadOnlyList<TrainCarTransform> EvaluateCarTransforms(
            double leadDistance,
            double carSpacing,
            int carCount)
        {
            return GetCarTransforms(leadDistance, carSpacing, carCount);
        }

        /// <summary>
        /// Preferred API for evaluating per-car body frames and transform
        /// matrices from a lead-car station distance using an explicit
        /// <see cref="BankingProfile"/> as the roll source.
        /// </summary>
        /// <remarks>
        /// This method keeps deterministic station-distance computation under the
        /// <c>Evaluate*</c> naming convention and delegates to the compatibility
        /// <see cref="GetCarTransforms(double, double, int, BankingProfile)"/>
        /// implementation.
        /// </remarks>
        public IReadOnlyList<TrainCarTransform> EvaluateCarTransforms(
            double leadDistance,
            double carSpacing,
            int carCount,
            BankingProfile bankingProfile)
        {
            return GetCarTransforms(leadDistance, carSpacing, carCount, bankingProfile);
        }

        /// <summary>
        /// Evaluates deterministic body and bogie transforms from a lead-car
        /// station distance and consist definition.
        /// </summary>
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

        /// <summary>
        /// Evaluates deterministic body, bogie, and wheel transforms from a
        /// lead-car station distance and consist definition.
        /// </summary>
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

        /// <summary>
        /// Evaluates deterministic articulated body transforms from a lead-car
        /// station distance and consist definition.
        /// </summary>
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

        /// <summary>
        /// Evaluates deterministic articulated body and wheel transforms from a
        /// lead-car station distance and consist definition.
        /// </summary>
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
        /// Preferred public train-pose entrypoint for distance-based coaster train
        /// placement.
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

        /// <summary>
        /// Preferred public train-pose entrypoint that explicitly uses a
        /// <see cref="BankingProfile"/> as the roll source.
        /// </summary>
        /// <remarks>
        /// This opt-in overload leaves the default segment-roll path unchanged.
        /// It samples all body and bogie frames as one transported-frame batch so
        /// BankingProfile roll is applied consistently across the evaluated train.
        /// Wheel transforms inherit their bogie frames, and articulated body
        /// transforms are assembled from those same profile-backed frames.
        /// </remarks>
        public TrainPoseResult EvaluateTrainPose(
            double leadDistance,
            TrainConsistDefinition definition,
            BankingProfile bankingProfile)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (bankingProfile == null)
            {
                throw new ArgumentNullException(nameof(bankingProfile));
            }

            TrainWheelLayout? wheelLayout = definition.WheelLayout;
            if (wheelLayout == null)
            {
                throw new InvalidOperationException("Wheel layout is required to evaluate wheel transforms.");
            }

            IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> evaluatedCars =
                EvaluateProfileBackedArticulatedTrainWithWheels(
                    leadDistance,
                    definition,
                    wheelLayout,
                    bankingProfile);

            return _poseAssembler.BuildPoseResult(leadDistance, definition, evaluatedCars);
        }

        /// <summary>
        /// Evaluates deterministic body and bogie transforms from explicit train
        /// spacing values and a lead-car station distance.
        /// </summary>
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

        private IReadOnlyList<TrainCarTransform> SampleProfileBackedBodies(
            double leadDistance,
            double carSpacing,
            int carCount,
            BankingProfile bankingProfile)
        {
            TrackDocument document = ResolveDocumentAndValidateBodyInputs(
                leadDistance,
                carSpacing,
                carCount);
            double[] distances = BuildBodyDistances(
                leadDistance,
                carSpacing,
                carCount,
                document.TotalLength);
            TrackFrame[] frames = SampleProfileFramesInInputOrder(
                document,
                bankingProfile,
                distances);
            var transforms = new List<TrainCarTransform>(carCount);

            for (int i = 0; i < carCount; i++)
            {
                TrackFrame frame = frames[i];
                transforms.Add(new TrainCarTransform(
                    i,
                    distances[i],
                    frame,
                    frame.ToMatrix4x4()));
            }

            return transforms;
        }

        private IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> EvaluateProfileBackedArticulatedTrainWithWheels(
            double leadDistance,
            TrainConsistDefinition definition,
            TrainWheelLayout wheelLayout,
            BankingProfile bankingProfile)
        {
            TrackDocument document = ResolveDocumentAndValidateBodyInputs(
                leadDistance,
                definition.CarSpacing,
                definition.CarCount);
            double totalLength = document.TotalLength;
            double[] bodyDistances = BuildBodyDistances(
                leadDistance,
                definition.CarSpacing,
                definition.CarCount,
                totalLength);
            double bogieHalfSpacing = definition.BogieSpacing * 0.5;
            var sampleDistances = new double[definition.CarCount * 3];

            for (int i = 0; i < definition.CarCount; i++)
            {
                int sampleIndex = i * 3;
                double bodyDistance = bodyDistances[i];
                sampleDistances[sampleIndex] = bodyDistance;

                double frontDistance = bodyDistance + bogieHalfSpacing;
                ValidateDistanceInRange(
                    frontDistance,
                    totalLength,
                    $"Computed front bogie distance for car {i} is out of range.");
                sampleDistances[sampleIndex + 1] = frontDistance;

                double rearDistance = bodyDistance - bogieHalfSpacing;
                ValidateDistanceInRange(
                    rearDistance,
                    totalLength,
                    $"Computed rear bogie distance for car {i} is out of range.");
                sampleDistances[sampleIndex + 2] = rearDistance;
            }

            TrackFrame[] frames = SampleProfileFramesInInputOrder(
                document,
                bankingProfile,
                sampleDistances);
            var carsWithBogies = new List<TrainCarWithBogiesTransform>(definition.CarCount);

            for (int i = 0; i < definition.CarCount; i++)
            {
                int sampleIndex = i * 3;
                TrackFrame bodyFrame = frames[sampleIndex];
                var body = new TrainCarTransform(
                    i,
                    sampleDistances[sampleIndex],
                    bodyFrame,
                    bodyFrame.ToMatrix4x4());

                TrackFrame frontFrame = frames[sampleIndex + 1];
                var frontBogie = new BogieTransform(
                    i,
                    bogieIndex: 0,
                    sampleDistances[sampleIndex + 1],
                    frontFrame,
                    Matrix4x4d.FromMatrix4x4(frontFrame.ToMatrix4x4()));

                TrackFrame rearFrame = frames[sampleIndex + 2];
                var rearBogie = new BogieTransform(
                    i,
                    bogieIndex: 1,
                    sampleDistances[sampleIndex + 2],
                    rearFrame,
                    Matrix4x4d.FromMatrix4x4(rearFrame.ToMatrix4x4()));

                carsWithBogies.Add(new TrainCarWithBogiesTransform(
                    body,
                    frontBogie,
                    rearBogie));
            }

            IReadOnlyList<ArticulatedTrainCarTransform> articulatedCars =
                _articulationSolver.SolveArticulatedBodies(carsWithBogies);
            IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> carsWithWheels =
                _wheelLayoutSolver.AttachWheels(carsWithBogies, wheelLayout);

            return _poseAssembler.AssembleArticulatedWithWheels(articulatedCars, carsWithWheels);
        }

        private TrackFrame[] SampleProfileFramesInInputOrder(
            TrackDocument document,
            BankingProfile bankingProfile,
            IReadOnlyList<double> distances)
        {
            if (distances.Count == 0)
            {
                return Array.Empty<TrackFrame>();
            }

            var sortedSamples = new IndexedDistance[distances.Count];
            for (int i = 0; i < distances.Count; i++)
            {
                sortedSamples[i] = new IndexedDistance(distances[i], i);
            }

            Array.Sort(
                sortedSamples,
                (left, right) =>
                {
                    int distanceComparison = left.Distance.CompareTo(right.Distance);
                    return distanceComparison != 0
                        ? distanceComparison
                        : left.OriginalIndex.CompareTo(right.OriginalIndex);
                });

            var sortedDistances = new double[sortedSamples.Length];
            for (int i = 0; i < sortedSamples.Length; i++)
            {
                sortedDistances[i] = sortedSamples[i].Distance;
            }

            TrackFrame[] sortedFrames = BankingProfileSampler.SampleFramesAtDistances(
                document,
                _evaluator,
                bankingProfile,
                sortedDistances);
            var frames = new TrackFrame[sortedFrames.Length];

            for (int i = 0; i < sortedFrames.Length; i++)
            {
                frames[sortedSamples[i].OriginalIndex] = sortedFrames[i];
            }

            return frames;
        }

        private TrackDocument ResolveDocumentAndValidateBodyInputs(
            double leadDistance,
            double carSpacing,
            int carCount)
        {
            if (double.IsNaN(leadDistance) || double.IsInfinity(leadDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leadDistance),
                    leadDistance,
                    "Lead distance must be finite.");
            }

            if (double.IsNaN(carSpacing) || double.IsInfinity(carSpacing) || carSpacing < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carSpacing),
                    carSpacing,
                    "Car spacing must be finite and non-negative.");
            }

            if (carCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carCount),
                    carCount,
                    "Car count must be non-negative.");
            }

            TrackDocument document = _evaluator.GetBoundTrackDocument();
            ValidateDistanceInRange(
                leadDistance,
                document.TotalLength,
                "Lead car distance is out of range.");
            return document;
        }

        private static double[] BuildBodyDistances(
            double leadDistance,
            double carSpacing,
            int carCount,
            double totalLength)
        {
            var distances = new double[carCount];

            for (int i = 0; i < carCount; i++)
            {
                double distance = leadDistance - (i * carSpacing);
                ValidateDistanceInRange(
                    distance,
                    totalLength,
                    $"Computed distance for car {i} is out of range.");

                distances[i] = distance;
            }

            return distances;
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

        private readonly struct IndexedDistance
        {
            public IndexedDistance(double distance, int originalIndex)
            {
                Distance = distance;
                OriginalIndex = originalIndex;
            }

            public double Distance { get; }

            public int OriginalIndex { get; }
        }
    }
}
