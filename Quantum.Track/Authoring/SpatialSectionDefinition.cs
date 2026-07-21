using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track.Authoring
{
    /// <summary>
    /// Authoring definition for one local three-dimensional NURBS centerline section.
    /// </summary>
    /// <remarks>
    /// Control points use the section construction frame: positive X is forward,
    /// positive Y is the incoming normal, and positive Z is the incoming binormal.
    /// The local curve must start at the origin with a tangent along positive X.
    /// </remarks>
    public sealed class SpatialSectionDefinition : GeometricSectionDefinition
    {
        private const double MinimumTangentMagnitude = 1e-9;
        private const double StartTangentAlignmentTolerance = 1e-9;

        private readonly IReadOnlyList<Vector3d> _controlPoints;
        private readonly IReadOnlyList<double> _weights;

        public SpatialSectionDefinition(
            string id,
            double length,
            IEnumerable<Vector3d> controlPoints,
            int degree = 3,
            IEnumerable<double>? weights = null,
            double rollRadians = 0.0)
            : base(id, length, rollRadians)
        {
            if (controlPoints is null)
            {
                throw new ArgumentNullException(nameof(controlPoints));
            }

            var copiedControlPoints = new List<Vector3d>(controlPoints);
            if (copiedControlPoints.Count == 0)
            {
                throw new ArgumentException(
                    "Spatial section control points cannot be empty.",
                    nameof(controlPoints));
            }

            ValidateControlPoints(copiedControlPoints);
            ValidateDegree(degree, copiedControlPoints.Count);
            ValidateStartContract(copiedControlPoints);

            var copiedWeights = weights is null
                ? CreateUnitWeights(copiedControlPoints.Count)
                : new List<double>(weights);
            ValidateWeights(copiedWeights, copiedControlPoints.Count);

            _controlPoints = copiedControlPoints.AsReadOnly();
            _weights = copiedWeights.AsReadOnly();
            Degree = degree;
        }

        /// <summary>
        /// Copied local-space NURBS control points.
        /// </summary>
        public IReadOnlyList<Vector3d> ControlPoints => _controlPoints;

        /// <summary>
        /// Copied positive NURBS weights, one per control point.
        /// </summary>
        public IReadOnlyList<double> Weights => _weights;

        /// <summary>
        /// NURBS polynomial degree.
        /// </summary>
        public int Degree { get; }

        public override string TypeId => TrackAuthoringSectionTypeIds.Spatial;

        private static void ValidateControlPoints(IReadOnlyList<Vector3d> controlPoints)
        {
            for (int i = 0; i < controlPoints.Count; i++)
            {
                Vector3d point = controlPoints[i];
                if (!AuthoringValidation.IsFinite(point.X) ||
                    !AuthoringValidation.IsFinite(point.Y) ||
                    !AuthoringValidation.IsFinite(point.Z))
                {
                    throw new ArgumentException(
                        $"Spatial section control point at index {i} must have finite components.",
                        nameof(controlPoints));
                }
            }
        }

        private static void ValidateDegree(int degree, int controlPointCount)
        {
            if (degree < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(degree),
                    degree,
                    "Spatial section degree must be at least 1.");
            }

            if (degree >= controlPointCount)
            {
                throw new ArgumentException(
                    "Spatial section control point count must be at least degree + 1.",
                    "controlPoints");
            }
        }

        private static void ValidateStartContract(IReadOnlyList<Vector3d> controlPoints)
        {
            Vector3d start = controlPoints[0];
            if (start.X != 0.0 || start.Y != 0.0 || start.Z != 0.0)
            {
                throw new ArgumentException(
                    "Spatial section local start point must be the origin.",
                    nameof(controlPoints));
            }

            Vector3d startDerivativeDirection = controlPoints[1] - start;
            double directionLength = startDerivativeDirection.Length;
            if (directionLength <= MinimumTangentMagnitude)
            {
                throw new ArgumentException(
                    "Spatial section local start tangent must have non-zero magnitude and point along positive X.",
                    nameof(controlPoints));
            }

            Vector3d tangent = startDerivativeDirection / directionLength;
            if (tangent.X < 1.0 - StartTangentAlignmentTolerance ||
                System.Math.Abs(tangent.Y) > StartTangentAlignmentTolerance ||
                System.Math.Abs(tangent.Z) > StartTangentAlignmentTolerance)
            {
                throw new ArgumentException(
                    "Spatial section local start tangent must point along positive X.",
                    nameof(controlPoints));
            }
        }

        private static List<double> CreateUnitWeights(int count)
        {
            var unitWeights = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                unitWeights.Add(1.0);
            }

            return unitWeights;
        }

        private static void ValidateWeights(IReadOnlyList<double> weights, int controlPointCount)
        {
            if (weights.Count != controlPointCount)
            {
                throw new ArgumentException(
                    "Spatial section weight count must match control point count.",
                    nameof(weights));
            }

            for (int i = 0; i < weights.Count; i++)
            {
                double weight = weights[i];
                if (!AuthoringValidation.IsFinite(weight) || weight <= 0.0)
                {
                    throw new ArgumentException(
                        $"Spatial section weight at index {i} must be finite and greater than zero.",
                        nameof(weights));
                }
            }
        }
    }
}
