using Quantum.Math;
using Quantum.Splines;

namespace Quantum.Track
{
    public sealed class GeometricSection : TrackSection
    {
        public GeometricSection(
            double length,
            double? curvature = null,
            double? roll = null)
        {
            Length = length;
            Curvature = curvature;
            Roll = roll;
        }

        public double Length { get; }

        public double? Curvature { get; }

        public double? Roll { get; }

        public IParamCurve GenerateCurve()
        {
            double length = IsFinite(Length) ? Length : 0.0;
            double curvature = Curvature ?? 0.0;

            if (!IsFinite(curvature) || System.Math.Abs(curvature) <= MathUtil.Epsilon)
            {
                return new LineCurve(Vector3d.Zero, new Vector3d(length, 0.0, 0.0));
            }

            return new ConstantCurvatureArcCurve(length, curvature);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class ConstantCurvatureArcCurve : IParamCurve, IParamCurveCurvature
        {
            private readonly double _curvature;
            private readonly double _angle;
            private readonly double _radius;

            public ConstantCurvatureArcCurve(double length, double curvature)
            {
                _curvature = curvature;
                _angle = length * curvature;
                _radius = 1.0 / curvature;
            }

            public Vector3d Evaluate(double t)
            {
                double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
                double theta = _angle * clampedT;

                return new Vector3d(
                    _radius * System.Math.Sin(theta),
                    _radius * (1.0 - System.Math.Cos(theta)),
                    0.0);
            }

            public Vector3d Tangent(double t)
            {
                double clampedT = MathUtil.Clamp(t, 0.0, 1.0);
                double theta = _angle * clampedT;

                return new Vector3d(
                    System.Math.Cos(theta),
                    System.Math.Sin(theta),
                    0.0);
            }

            public bool TryGetCurvature(double t, out double curvature)
            {
                curvature = _curvature;
                return true;
            }
        }
    }
}
