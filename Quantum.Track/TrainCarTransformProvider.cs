using System;
using System.Collections.Generic;
using Quantum.Math;
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

        public TrainCarTransformProvider(TrackEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _bodySampler = new TrainCarBodySampler(_evaluator);
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
            var transforms = new List<ArticulatedTrainCarTransform>(carsWithBogies.Count);

            for (int i = 0; i < carsWithBogies.Count; i++)
            {
                TrainCarWithBogiesTransform carWithBogies = carsWithBogies[i];
                TrainCarTransform originalBody = carWithBogies.Body;
                BogieTransform frontBogie = carWithBogies.FrontBogie;
                BogieTransform rearBogie = carWithBogies.RearBogie;

                Vector3d centerPosition = (frontBogie.Frame.Position + rearBogie.Frame.Position) * 0.5;
                Vector3d tangent = ResolveArticulationTangent(frontBogie, rearBogie, originalBody);
                Vector3d averagedNormal = (frontBogie.Frame.Normal + rearBogie.Frame.Normal) * 0.5;

                BuildArticulationBasis(
                    tangent,
                    averagedNormal,
                    originalBody.Frame.Normal,
                    originalBody.Frame.Binormal,
                    out Vector3d normal,
                    out Vector3d binormal);

                var articulatedFrame = new TrackFrame(
                    originalBody.Distance,
                    centerPosition,
                    tangent,
                    normal,
                    binormal);

                transforms.Add(new ArticulatedTrainCarTransform(
                    originalBody,
                    frontBogie,
                    rearBogie,
                    articulatedFrame,
                    Matrix4x4d.FromMatrix4x4(articulatedFrame.ToMatrix4x4()),
                    originalBody.Distance));
            }

            return transforms;
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

            double totalLength = _evaluator.GetBoundTrackTotalLength();
            double bogieHalfSpacing = bogieSpacing * 0.5;
            var transforms = new List<TrainCarWithBogiesTransform>(bodyTransforms.Count);

            for (int i = 0; i < bodyTransforms.Count; i++)
            {
                TrainCarTransform body = bodyTransforms[i];

                double frontDistance = body.Distance + bogieHalfSpacing;
                ValidateDistanceInRange(
                    frontDistance,
                    totalLength,
                    $"Computed front bogie distance for car {body.CarIndex} is out of range.");

                TrackFrame frontFrame = _evaluator.EvaluateFrameAtDistance(frontDistance);
                var frontBogie = new BogieTransform(
                    body.CarIndex,
                    bogieIndex: 0,
                    frontDistance,
                    frontFrame,
                    Matrix4x4d.FromMatrix4x4(frontFrame.ToMatrix4x4()));

                double rearDistance = body.Distance - bogieHalfSpacing;
                ValidateDistanceInRange(
                    rearDistance,
                    totalLength,
                    $"Computed rear bogie distance for car {body.CarIndex} is out of range.");

                TrackFrame rearFrame = _evaluator.EvaluateFrameAtDistance(rearDistance);
                var rearBogie = new BogieTransform(
                    body.CarIndex,
                    bogieIndex: 1,
                    rearDistance,
                    rearFrame,
                    Matrix4x4d.FromMatrix4x4(rearFrame.ToMatrix4x4()));

                transforms.Add(new TrainCarWithBogiesTransform(body, frontBogie, rearBogie));
            }

            return transforms;
        }

        private static Vector3d ResolveArticulationTangent(
            BogieTransform frontBogie,
            BogieTransform rearBogie,
            TrainCarTransform originalBody)
        {
            if (TryNormalize(frontBogie.Frame.Position - rearBogie.Frame.Position, out Vector3d tangent))
            {
                return tangent;
            }

            if (TryNormalize(originalBody.Frame.Tangent, out tangent))
            {
                return tangent;
            }

            if (TryNormalize(frontBogie.Frame.Tangent, out tangent))
            {
                return tangent;
            }

            if (TryNormalize(rearBogie.Frame.Tangent, out tangent))
            {
                return tangent;
            }

            return Vector3d.UnitX;
        }

        private static void BuildArticulationBasis(
            Vector3d tangent,
            Vector3d preferredNormal,
            Vector3d fallbackNormal,
            Vector3d fallbackBinormal,
            out Vector3d normal,
            out Vector3d binormal)
        {
            if (!TryBuildPerpendicularVector(tangent, preferredNormal, out normal) &&
                !TryBuildPerpendicularVector(tangent, fallbackNormal, out normal) &&
                !TryBuildPerpendicularVector(tangent, Vector3d.Cross(fallbackBinormal, tangent), out normal))
            {
                normal = BuildArbitraryPerpendicular(tangent);
            }

            if (!TryNormalize(Vector3d.Cross(tangent, normal), out binormal) &&
                !TryBuildPerpendicularVector(tangent, fallbackBinormal, out binormal))
            {
                binormal = Vector3d.Cross(tangent, BuildArbitraryPerpendicular(tangent)).Normalized();
                if (!IsFiniteVector(binormal) || binormal.LengthSquared <= 0.0)
                {
                    binormal = Vector3d.UnitZ;
                }
            }

            if (!TryNormalize(Vector3d.Cross(binormal, tangent), out normal))
            {
                normal = BuildArbitraryPerpendicular(tangent);
                binormal = Vector3d.Cross(tangent, normal).Normalized();
            }
        }

        private static bool TryBuildPerpendicularVector(
            Vector3d tangent,
            Vector3d reference,
            out Vector3d result)
        {
            if (!IsFiniteVector(reference))
            {
                result = Vector3d.Zero;
                return false;
            }

            Vector3d projected = reference - (Vector3d.Dot(reference, tangent) * tangent);
            return TryNormalize(projected, out result);
        }

        private static Vector3d BuildArbitraryPerpendicular(Vector3d tangent)
        {
            Vector3d axis = System.Math.Abs(tangent.X) < 0.9 ? Vector3d.UnitX : Vector3d.UnitY;
            Vector3d perpendicular = Vector3d.Cross(axis, tangent);

            if (TryNormalize(perpendicular, out Vector3d normalizedPerpendicular))
            {
                return normalizedPerpendicular;
            }

            perpendicular = Vector3d.Cross(Vector3d.UnitZ, tangent);
            if (TryNormalize(perpendicular, out normalizedPerpendicular))
            {
                return normalizedPerpendicular;
            }

            return Vector3d.UnitY;
        }

        private static bool TryNormalize(Vector3d value, out Vector3d normalized)
        {
            if (!IsFiniteVector(value))
            {
                normalized = Vector3d.Zero;
                return false;
            }

            double lengthSquared = value.LengthSquared;
            if (lengthSquared <= (MathUtil.Epsilon * MathUtil.Epsilon))
            {
                normalized = Vector3d.Zero;
                return false;
            }

            double inverseLength = 1.0 / System.Math.Sqrt(lengthSquared);
            normalized = value * inverseLength;
            return true;
        }

        private static bool IsFiniteVector(Vector3d vector)
        {
            return
                !double.IsNaN(vector.X) &&
                !double.IsNaN(vector.Y) &&
                !double.IsNaN(vector.Z) &&
                !double.IsInfinity(vector.X) &&
                !double.IsInfinity(vector.Y) &&
                !double.IsInfinity(vector.Z);
        }

        private static WheelTransform[] BuildWheelTransforms(BogieTransform bogie, TrainWheelLayout wheelLayout)
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
