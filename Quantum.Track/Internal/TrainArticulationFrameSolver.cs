using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.Track.Internal
{
    internal sealed class TrainArticulationFrameSolver
    {
        public IReadOnlyList<ArticulatedTrainCarTransform> SolveArticulatedBodies(
            IReadOnlyList<TrainCarWithBogiesTransform> carsWithBogies)
        {
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
    }
}
