using Quantum.Math;
using Quantum.Physics;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SystemMath = System.Math;

namespace Quantum.Tests;

public sealed class DiagnosticTrackFixtureTests
{
    private const double DistanceTolerance = 1e-7;
    private const double AxisTolerance = 1e-6;
    private const double CurvatureTolerance = 1e-6;
    private const double RadiusTolerance = 1e-6;

    public static IEnumerable<object[]> FixtureData
    {
        get
        {
            foreach (DiagnosticTrackFixture fixture in DiagnosticTrackFixtures.All())
            {
                yield return new object[] { fixture };
            }
        }
    }

    [Fact]
    public void DiagnosticTrackFixtures_All_CoversRequestedFixtureSet()
    {
        IReadOnlyList<DiagnosticTrackFixture> fixtures = DiagnosticTrackFixtures.All();

        Assert.Equal(
            new[]
            {
                DiagnosticTrackFixtures.StraightHorizontalName,
                DiagnosticTrackFixtures.NearVerticalTangentSequenceName,
                DiagnosticTrackFixtures.CrestHillName,
                DiagnosticTrackFixtures.ConstantRadiusTurnName,
                DiagnosticTrackFixtures.SimpleBankedTurnName,
                DiagnosticTrackFixtures.QuarterLoopLikeName
            },
            fixtures.Select(fixture => fixture.Name).ToArray());
    }

    [Theory]
    [MemberData(nameof(FixtureData))]
    public void DiagnosticTrackFixtures_ProduceFiniteDistanceSamplesAndFrames(DiagnosticTrackFixture fixture)
    {
        var evaluator = new TrackEvaluator(fixture.Document);
        double totalLength = fixture.Document.TotalLength;

        Assert.True(IsFinite(totalLength), $"{fixture.Name} total length should be finite.");
        Assert.True(totalLength > 0.0, $"{fixture.Name} total length should be positive.");
        Assert.True(fixture.SampleDistances.Count >= 2, $"{fixture.Name} should have reusable samples.");

        double previousDistance = double.NegativeInfinity;
        for (int i = 0; i < fixture.SampleDistances.Count; i++)
        {
            double distance = fixture.SampleDistances[i];

            Assert.True(IsFinite(distance), $"{fixture.Name} sample {i} distance should be finite.");
            Assert.InRange(distance, 0.0, totalLength);
            Assert.True(distance >= previousDistance, $"{fixture.Name} sample distances should be monotonic.");
            previousDistance = distance;
        }

        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(fixture.SampleDistances);

        Assert.Equal(fixture.SampleDistances.Count, frames.Length);
        for (int i = 0; i < frames.Length; i++)
        {
            AssertFiniteFrame(fixture.Name, i, frames[i]);
            AssertNear(fixture.SampleDistances[i], frames[i].Distance, DistanceTolerance);
        }
    }

    [Theory]
    [MemberData(nameof(FixtureData))]
    public void DiagnosticTrackFixtures_ProduceFiniteCurvatureAndRadiusDiagnostics(DiagnosticTrackFixture fixture)
    {
        var adapter = new TrackPhysicsAdapter();
        bool sawNonZeroCurvature = false;

        for (int i = 0; i < fixture.CurvatureProbeDistances.Count; i++)
        {
            double distance = fixture.CurvatureProbeDistances[i];
            bool success = adapter.TryGetCurvatureAtDistance(fixture.Document, distance, out double curvature);

            Assert.True(success, $"{fixture.Name} curvature probe {i} should be evaluable.");
            Assert.True(IsFinite(curvature), $"{fixture.Name} curvature probe {i} should be finite.");
            Assert.InRange(curvature, 0.0, double.MaxValue);

            if (fixture.ExpectedConstantCurvature.HasValue)
            {
                AssertNear(fixture.ExpectedConstantCurvature.Value, curvature, CurvatureTolerance);
            }

            if (curvature > CurvatureTolerance)
            {
                sawNonZeroCurvature = true;
                double radius = 1.0 / curvature;

                Assert.True(IsFinite(radius), $"{fixture.Name} radius probe {i} should be finite.");
                Assert.InRange(radius, 0.0, double.MaxValue);

                if (fixture.ExpectedConstantRadius.HasValue)
                {
                    AssertNear(fixture.ExpectedConstantRadius.Value, radius, RadiusTolerance);
                }
            }
        }

        Assert.Equal(fixture.ExpectNonZeroCurvature, sawNonZeroCurvature);
    }

    [Theory]
    [MemberData(nameof(FixtureData))]
    public void DiagnosticTrackFixtures_WorkWithExistingFrameDiagnostics(DiagnosticTrackFixture fixture)
    {
        var evaluator = new TrackEvaluator(fixture.Document);
        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(fixture.SampleDistances);

        TrackFrameSmoothnessReport smoothnessReport = TrackFrameSmoothnessDiagnostics.Analyze(
            frames,
            fixture.SampleDistances);
        TrackFrameContinuityReport continuityReport = TrackFrameContinuityDiagnostics.Analyze(
            frames,
            fixture.SampleDistances,
            TrackFrameContinuityThresholds.UniformDegrees(181.0));

        Assert.Equal(fixture.SampleDistances.Count - 1, smoothnessReport.IntervalCount);
        Assert.Equal(fixture.SampleDistances.Count - 1, continuityReport.IntervalCount);
        Assert.False(continuityReport.HasDiscontinuities, continuityReport.ToDiagnosticString());
        Assert.Contains("Frame continuity:", continuityReport.ToDiagnosticString());

        AssertFiniteSummary(fixture.Name, smoothnessReport.TangentAngleDelta);
        AssertFiniteSummary(fixture.Name, smoothnessReport.NormalAngleDelta);
        AssertFiniteSummary(fixture.Name, smoothnessReport.BinormalAngleDelta);
        AssertFiniteSummary(fixture.Name, smoothnessReport.FrameAngleDelta);
        AssertFiniteSummary(fixture.Name, smoothnessReport.FrameTwistDelta);
        AssertFiniteCurvatureSummary(fixture.Name, smoothnessReport.CurvatureEstimate);
        AssertFiniteCurvatureSummary(fixture.Name, smoothnessReport.CurvatureEstimateDelta);
        AssertFiniteSummary(fixture.Name, continuityReport.MatrixOrientationAngleDelta);
    }

    [Fact]
    public void DiagnosticTrackFixtures_ExposeExpectedGeometryCharacteristics()
    {
        IReadOnlyDictionary<string, DiagnosticTrackFixture> fixtures =
            DiagnosticTrackFixtures.All().ToDictionary(fixture => fixture.Name);

        ExportTrackFrame nearVerticalMid = SampleAtFraction(
            fixtures[DiagnosticTrackFixtures.NearVerticalTangentSequenceName],
            0.5);
        Assert.True(
            Vector3d.Dot(nearVerticalMid.Tangent, Vector3d.UnitY) > 0.99,
            "Near-vertical tangent fixture should exercise the evaluator reference-up fallback region.");

        ExportTrackFrame[] crestFrames = SampleFrames(fixtures[DiagnosticTrackFixtures.CrestHillName]);
        double crestMaxY = crestFrames.Max(frame => frame.Position.Y);
        Assert.True(crestMaxY > 10.0, "Crest fixture should have meaningful elevation.");
        Assert.True(crestFrames[1].Tangent.Y > 0.0, "Crest fixture should climb near the start.");
        Assert.True(crestFrames[crestFrames.Length - 2].Tangent.Y < 0.0, "Crest fixture should descend near the end.");

        ExportTrackFrame constantTurnEnd = SampleAtFraction(
            fixtures[DiagnosticTrackFixtures.ConstantRadiusTurnName],
            1.0);
        Assert.True(constantTurnEnd.Position.Z > 20.0, "Constant-radius turn should move laterally in the horizontal plane.");
        AssertNear(0.0, constantTurnEnd.Position.Y, AxisTolerance);

        ExportTrackFrame bankedMid = SampleAtFraction(
            fixtures[DiagnosticTrackFixtures.SimpleBankedTurnName],
            0.5);
        Assert.True(
            SystemMath.Abs(Vector3d.Dot(bankedMid.Normal, Vector3d.UnitY)) < 0.98,
            "Banked turn should rotate the frame normal away from world up.");

        ExportTrackFrame loopEnd = SampleAtFraction(
            fixtures[DiagnosticTrackFixtures.QuarterLoopLikeName],
            1.0);
        Assert.True(
            Vector3d.Dot(loopEnd.Tangent, Vector3d.UnitY) > 0.99,
            "Quarter-loop-like fixture should end with a vertical tangent.");
    }

    private static ExportTrackFrame[] SampleFrames(DiagnosticTrackFixture fixture)
    {
        var evaluator = new TrackEvaluator(fixture.Document);
        return evaluator.EvaluateFramesAtDistances(fixture.SampleDistances);
    }

    private static ExportTrackFrame SampleAtFraction(DiagnosticTrackFixture fixture, double fraction)
    {
        var evaluator = new TrackEvaluator(fixture.Document);
        double distance = fixture.Document.TotalLength * fraction;
        return evaluator.EvaluateFrameAtDistance(distance);
    }

    private static void AssertFiniteFrame(string fixtureName, int sampleIndex, ExportTrackFrame frame)
    {
        Assert.True(IsFinite(frame.Distance), $"{fixtureName} frame {sampleIndex} distance should be finite.");
        AssertFiniteVector(fixtureName, sampleIndex, nameof(frame.Position), frame.Position);
        AssertFiniteVector(fixtureName, sampleIndex, nameof(frame.Tangent), frame.Tangent);
        AssertFiniteVector(fixtureName, sampleIndex, nameof(frame.Normal), frame.Normal);
        AssertFiniteVector(fixtureName, sampleIndex, nameof(frame.Binormal), frame.Binormal);

        AssertNear(1.0, frame.Tangent.Length, AxisTolerance);
        AssertNear(1.0, frame.Normal.Length, AxisTolerance);
        AssertNear(1.0, frame.Binormal.Length, AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal), AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal), AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal), AxisTolerance);

        Vector3d expectedBinormal = Vector3d.Cross(frame.Tangent, frame.Normal).Normalized();
        Assert.True(
            Vector3d.Dot(expectedBinormal, frame.Binormal) > 1.0 - AxisTolerance,
            $"{fixtureName} frame {sampleIndex} should preserve the TrackFrame handedness convention.");
    }

    private static void AssertFiniteVector(string fixtureName, int sampleIndex, string label, Vector3d value)
    {
        Assert.True(
            IsFinite(value),
            $"{fixtureName} frame {sampleIndex} {label} should contain finite components.");
    }

    private static void AssertFiniteSummary(string fixtureName, TrackFrameSmoothnessMetricSummary summary)
    {
        Assert.True(IsFinite(summary.MaxAbsoluteRadians), $"{fixtureName} max angle should be finite.");
        Assert.True(IsFinite(summary.AverageAbsoluteRadians), $"{fixtureName} average angle should be finite.");
        Assert.InRange(summary.MaxAbsoluteRadians, 0.0, double.MaxValue);
        Assert.InRange(summary.AverageAbsoluteRadians, 0.0, double.MaxValue);
    }

    private static void AssertFiniteCurvatureSummary(string fixtureName, TrackFrameCurvatureMetricSummary summary)
    {
        Assert.True(IsFinite(summary.MaxAbsolute), $"{fixtureName} max curvature summary should be finite.");
        Assert.True(IsFinite(summary.AverageAbsolute), $"{fixtureName} average curvature summary should be finite.");
        Assert.InRange(summary.MaxAbsolute, 0.0, double.MaxValue);
        Assert.InRange(summary.AverageAbsolute, 0.0, double.MaxValue);
    }

    private static bool IsFinite(Vector3d value)
    {
        return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static void AssertNear(double expected, double actual, double tolerance)
    {
        Assert.InRange(SystemMath.Abs(expected - actual), 0.0, tolerance);
    }
}
