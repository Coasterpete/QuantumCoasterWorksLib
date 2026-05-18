using System;
using System.Collections.Generic;
using GShark.Geometry;
using Quantum.Math;
using GSharkNurbsCurve = GShark.Geometry.NurbsCurve;

namespace Quantum.Splines
{
    /// <summary>
    /// IParamCurve adapter backed by G-Shark NURBS evaluation.
    /// </summary>
    public sealed class GSharkNurbsCurveAdapter : IParamCurve
    {
        private readonly GSharkNurbsCurve _curve;

        public GSharkNurbsCurveAdapter(
            List<Vector3d> controlPoints,
            List<double> weights,
            int degree)
        {
            if (controlPoints == null)
                throw new ArgumentNullException(nameof(controlPoints));

            if (weights == null)
                throw new ArgumentNullException(nameof(weights));

            if (degree < 1)
                throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be at least 1.");

            if (controlPoints.Count < degree + 1)
                throw new ArgumentException("Insufficient control points for given degree.", nameof(controlPoints));

            if (weights.Count != controlPoints.Count)
                throw new ArgumentException("Weight count must match control point count.", nameof(weights));

            ValidateControlPoints(controlPoints);
            ValidateWeights(weights);

            _curve = new GSharkNurbsCurve(
                GSharkVector3dConversions.ToGSharkPoint3List(controlPoints),
                new List<double>(weights),
                degree);
        }

        public Vector3d Evaluate(double t)
        {
            t = System.Math.Clamp(t, 0.0, 1.0);

            Vector3d point = _curve.PointAt(t).ToQuantumVector3d();
            EnsureFinite(point, t, "position");
            return point;
        }

        public Vector3d Tangent(double t)
        {
            t = System.Math.Clamp(t, 0.0, 1.0);

            Vector3d tangent = _curve.TangentAt(t).ToQuantumVector3d();
            EnsureFinite(tangent, t, "tangent");

            if (tangent.Length <= MathUtil.Epsilon)
            {
                throw new InvalidOperationException(
                    $"Unable to compute G-Shark NURBS tangent at t={t:0.######}: derivative magnitude is near zero.");
            }

            return tangent.Normalized();
        }

        private static void ValidateControlPoints(IReadOnlyList<Vector3d> controlPoints)
        {
            for (int i = 0; i < controlPoints.Count; i++)
            {
                Vector3d point = controlPoints[i];
                if (double.IsNaN(point.X) || double.IsInfinity(point.X) ||
                    double.IsNaN(point.Y) || double.IsInfinity(point.Y) ||
                    double.IsNaN(point.Z) || double.IsInfinity(point.Z))
                {
                    throw new ArgumentException(
                        $"Control point at index {i} must have finite X/Y/Z components.",
                        nameof(controlPoints));
                }
            }
        }

        private static void ValidateWeights(IReadOnlyList<double> weights)
        {
            for (int i = 0; i < weights.Count; i++)
            {
                double weight = weights[i];
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0.0)
                {
                    throw new ArgumentException(
                        $"Weight at index {i} must be a positive finite value.",
                        nameof(weights));
                }
            }
        }

        private static void EnsureFinite(Vector3d value, double t, string quantityName)
        {
            if (double.IsNaN(value.X) || double.IsInfinity(value.X) ||
                double.IsNaN(value.Y) || double.IsInfinity(value.Y) ||
                double.IsNaN(value.Z) || double.IsInfinity(value.Z))
            {
                throw new InvalidOperationException(
                    $"Unable to evaluate G-Shark NURBS {quantityName} at t={t:0.######}: non-finite result.");
            }
        }
    }
}
