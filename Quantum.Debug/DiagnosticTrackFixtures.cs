using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using SystemMath = System.Math;

namespace Quantum.Debug
{
    public sealed class DiagnosticTrackFixture
    {
        public DiagnosticTrackFixture(
            string name,
            TrackDocument document,
            IReadOnlyList<double> sampleDistances,
            IReadOnlyList<double> curvatureProbeDistances,
            double? expectedConstantCurvature = null,
            double? expectedConstantRadius = null,
            bool expectNonZeroCurvature = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Document = document ?? throw new ArgumentNullException(nameof(document));
            SampleDistances = sampleDistances ?? throw new ArgumentNullException(nameof(sampleDistances));
            CurvatureProbeDistances = curvatureProbeDistances ?? throw new ArgumentNullException(nameof(curvatureProbeDistances));
            ExpectedConstantCurvature = expectedConstantCurvature;
            ExpectedConstantRadius = expectedConstantRadius;
            ExpectNonZeroCurvature = expectNonZeroCurvature;
        }

        public string Name { get; }

        public TrackDocument Document { get; }

        public IReadOnlyList<double> SampleDistances { get; }

        public IReadOnlyList<double> CurvatureProbeDistances { get; }

        public double? ExpectedConstantCurvature { get; }

        public double? ExpectedConstantRadius { get; }

        public bool ExpectNonZeroCurvature { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    public static class DiagnosticTrackFixtures
    {
        public const string StraightHorizontalName = "straight-horizontal";
        public const string NearVerticalTangentSequenceName = "near-vertical-tangent-sequence";
        public const string CrestHillName = "crest-hill";
        public const string ConstantRadiusTurnName = "constant-radius-turn";
        public const string SimpleBankedTurnName = "simple-banked-turn";
        public const string QuarterLoopLikeName = "quarter-loop-like";

        private const int DefaultSampleCount = 17;
        private const double DegreesToRadians = SystemMath.PI / 180.0;

        public static IReadOnlyList<DiagnosticTrackFixture> All()
        {
            return new[]
            {
                StraightHorizontal(),
                NearVerticalTangentSequence(),
                CrestHill(),
                ConstantRadiusTurn(),
                SimpleBankedTurn(),
                QuarterLoopLike()
            };
        }

        public static DiagnosticTrackFixture StraightHorizontal()
        {
            const double length = 40.0;
            var document = new TrackDocument(new[]
            {
                new StraightSegment(
                    length: length,
                    id: StraightHorizontalName,
                    spline: new LineCurve(Vector3d.Zero, new Vector3d(length, 0.0, 0.0)),
                    rollRadians: 0.0)
            });

            return CreateFixture(
                StraightHorizontalName,
                document,
                expectedConstantCurvature: 0.0,
                expectedConstantRadius: null,
                expectNonZeroCurvature: false);
        }

        public static DiagnosticTrackFixture NearVerticalTangentSequence()
        {
            const double length = 32.0;
            Vector3d direction = Normalize(new Vector3d(0.08, 1.0, 0.04));
            var document = new TrackDocument(new[]
            {
                new StraightSegment(
                    length: length,
                    id: NearVerticalTangentSequenceName,
                    spline: new LineCurve(Vector3d.Zero, direction * length),
                    rollRadians: 0.0)
            });

            return CreateFixture(
                NearVerticalTangentSequenceName,
                document,
                expectedConstantCurvature: 0.0,
                expectedConstantRadius: null,
                expectNonZeroCurvature: false);
        }

        public static DiagnosticTrackFixture CrestHill()
        {
            var spline = new CubicBezierCurve(
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(14.0, 20.0, 0.0),
                new Vector3d(40.0, 20.0, 0.0),
                new Vector3d(54.0, 0.0, 0.0));
            double length = new ArcLengthLUT(spline).TotalLength;
            var document = new TrackDocument(new[]
            {
                new CurvedSegment(
                    length: length,
                    id: CrestHillName,
                    spline: spline,
                    rollRadians: 0.0)
            });

            return CreateFixture(
                CrestHillName,
                document,
                expectedConstantCurvature: null,
                expectedConstantRadius: null,
                expectNonZeroCurvature: true);
        }

        public static DiagnosticTrackFixture ConstantRadiusTurn()
        {
            const double radius = 24.0;
            const double angleRadians = SystemMath.PI * 0.5;
            double length = radius * angleRadians;
            var document = new TrackDocument(new[]
            {
                new CurvedSegment(
                    length: length,
                    id: ConstantRadiusTurnName,
                    spline: new HorizontalArcCurve(radius, angleRadians),
                    rollRadians: 0.0)
            });

            return CreateFixture(
                ConstantRadiusTurnName,
                document,
                expectedConstantCurvature: 1.0 / radius,
                expectedConstantRadius: radius,
                expectNonZeroCurvature: true);
        }

        public static DiagnosticTrackFixture SimpleBankedTurn()
        {
            const double radius = 28.0;
            const double angleRadians = SystemMath.PI * 0.5;
            const double rollRadians = 25.0 * DegreesToRadians;
            double length = radius * angleRadians;
            var document = new TrackDocument(new[]
            {
                new CurvedSegment(
                    length: length,
                    id: SimpleBankedTurnName,
                    spline: new HorizontalArcCurve(radius, angleRadians),
                    rollRadians: rollRadians)
            });

            return CreateFixture(
                SimpleBankedTurnName,
                document,
                expectedConstantCurvature: 1.0 / radius,
                expectedConstantRadius: radius,
                expectNonZeroCurvature: true);
        }

        public static DiagnosticTrackFixture QuarterLoopLike()
        {
            const double radius = 18.0;
            double length = radius * SystemMath.PI * 0.5;
            var document = GeometricSectionTrackDocumentBuilder.BuildDocument(
                new GeometricSection(length, curvature: 1.0 / radius, roll: 0.0),
                segmentId: QuarterLoopLikeName);

            return CreateFixture(
                QuarterLoopLikeName,
                document,
                expectedConstantCurvature: 1.0 / radius,
                expectedConstantRadius: radius,
                expectNonZeroCurvature: true);
        }

        private static DiagnosticTrackFixture CreateFixture(
            string name,
            TrackDocument document,
            double? expectedConstantCurvature,
            double? expectedConstantRadius,
            bool expectNonZeroCurvature)
        {
            double totalLength = document.TotalLength;
            return new DiagnosticTrackFixture(
                name,
                document,
                BuildUniformDistances(totalLength, DefaultSampleCount),
                BuildCurvatureProbeDistances(totalLength),
                expectedConstantCurvature,
                expectedConstantRadius,
                expectNonZeroCurvature);
        }

        private static double[] BuildUniformDistances(double totalLength, int sampleCount)
        {
            if (!IsFinite(totalLength) || totalLength <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalLength), totalLength, "Fixture length must be finite and greater than zero.");
            }

            if (sampleCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), sampleCount, "Fixture sample count must be at least two.");
            }

            var distances = new double[sampleCount];
            double interval = totalLength / (sampleCount - 1);
            for (int i = 0; i < sampleCount; i++)
            {
                distances[i] = i * interval;
            }

            distances[sampleCount - 1] = totalLength;
            return distances;
        }

        private static double[] BuildCurvatureProbeDistances(double totalLength)
        {
            if (!IsFinite(totalLength) || totalLength <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalLength), totalLength, "Fixture length must be finite and greater than zero.");
            }

            return new[]
            {
                totalLength * 0.25,
                totalLength * 0.5,
                totalLength * 0.75
            };
        }

        private static Vector3d Normalize(Vector3d vector)
        {
            double length = vector.Length;
            if (length <= 1e-9)
            {
                throw new InvalidOperationException("Fixture vector cannot be normalized.");
            }

            return vector / length;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class HorizontalArcCurve : IArcLengthCurve, IParamCurveCurvature
        {
            private readonly double _radius;
            private readonly double _angleRadians;

            public HorizontalArcCurve(double radius, double angleRadians)
            {
                if (!IsFinite(radius) || radius <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(radius), radius, "Arc radius must be finite and greater than zero.");
                }

                if (!IsFinite(angleRadians) || angleRadians == 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(angleRadians), angleRadians, "Arc angle must be finite and non-zero.");
                }

                _radius = radius;
                _angleRadians = angleRadians;
            }

            public Vector3d Evaluate(double t)
            {
                double theta = _angleRadians * Clamp01(t);
                return new Vector3d(
                    _radius * SystemMath.Sin(theta),
                    0.0,
                    _radius * (1.0 - SystemMath.Cos(theta)));
            }

            public double Length => SystemMath.Abs(_radius * _angleRadians);

            public Vector3d Tangent(double t)
            {
                double theta = _angleRadians * Clamp01(t);
                Vector3d tangent = new Vector3d(
                    SystemMath.Cos(theta),
                    0.0,
                    SystemMath.Sin(theta));

                return Normalize(tangent);
            }

            public Vector3d EvaluateByLength(double s)
            {
                return Evaluate(MapLengthToParameter(s));
            }

            public Vector3d TangentByLength(double s)
            {
                return Tangent(MapLengthToParameter(s));
            }

            public bool TryGetCurvature(double t, out double curvature)
            {
                curvature = 1.0 / _radius;
                return IsFinite(t) && t >= 0.0 && t <= 1.0;
            }

            private double MapLengthToParameter(double s)
            {
                if (Length <= 1e-9)
                {
                    return 0.0;
                }

                return Clamp01(s / Length);
            }

            private static double Clamp01(double value)
            {
                if (value < 0.0)
                {
                    return 0.0;
                }

                if (value > 1.0)
                {
                    return 1.0;
                }

                return value;
            }
        }
    }
}
