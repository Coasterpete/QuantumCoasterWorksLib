using Quantum.Debug;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class CanonicalTransportedFrameTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void ScalarAndBatchFrames_AreEqualAtEveryStation()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.QuarterLoopLike();
        var evaluator = new TrackEvaluator(fixture.Document);
        double[] distances = { fixture.Document.TotalLength * 0.73, 0.0, fixture.Document.TotalLength * 0.31 };

        ExportTrackFrame[] batch = evaluator.EvaluateFramesAtDistances(distances);

        for (int i = 0; i < distances.Length; i++)
        {
            AssertFrameNear(evaluator.EvaluateFrameAtDistance(distances[i]), batch[i]);
        }
    }

    [Fact]
    public void SparseAndDenseQueries_ReturnTheSameFrameAtSharedStation()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.CrestHill();
        var evaluator = new TrackEvaluator(fixture.Document);
        double target = fixture.Document.TotalLength * 0.373;

        ExportTrackFrame sparse = evaluator.EvaluateFramesAtDistances(new[] { target })[0];
        ExportTrackFrame dense = evaluator.EvaluateFramesAtDistances(new[]
        {
            0.0,
            fixture.Document.TotalLength * 0.1,
            fixture.Document.TotalLength * 0.2,
            target,
            fixture.Document.TotalLength * 0.6,
            fixture.Document.TotalLength
        })[3];

        AssertFrameNear(sparse, dense);
    }

    [Fact]
    public void QueryOrder_DoesNotChangeFrames()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.QuarterLoopLike();
        var evaluator = new TrackEvaluator(fixture.Document);
        double a = fixture.Document.TotalLength * 0.2;
        double b = fixture.Document.TotalLength * 0.55;
        double c = fixture.Document.TotalLength * 0.9;

        ExportTrackFrame[] forward = evaluator.EvaluateFramesAtDistances(new[] { a, b, c });
        ExportTrackFrame[] shuffled = evaluator.EvaluateFramesAtDistances(new[] { c, a, b });

        AssertFrameNear(forward[0], shuffled[1]);
        AssertFrameNear(forward[1], shuffled[2]);
        AssertFrameNear(forward[2], shuffled[0]);
    }

    [Fact]
    public void DuplicateDistances_AreDeterministicAndPreserveInputOrder()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.CrestHill();
        var evaluator = new TrackEvaluator(fixture.Document);
        double repeated = fixture.Document.TotalLength * 0.47;
        double other = fixture.Document.TotalLength * 0.12;

        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(
            new[] { repeated, other, repeated, repeated });

        AssertFrameNear(frames[0], frames[2]);
        AssertFrameNear(frames[0], frames[3]);
        AssertFrameNear(evaluator.EvaluateFrameAtDistance(other), frames[1]);
    }

    [Fact]
    public void SmoothSegmentBoundary_PreservesFrameContinuity()
    {
        TrackDocument document = CreateSmoothTwoSegmentDocument();
        var evaluator = new TrackEvaluator(document);
        double boundary = document.Segments[0].Length;
        double epsilon = 1e-4;

        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(
            new[] { boundary - epsilon, boundary, boundary + epsilon });

        Assert.True(Vector3d.Dot(frames[0].Tangent, frames[1].Tangent) > 0.9999);
        Assert.True(Vector3d.Dot(frames[1].Tangent, frames[2].Tangent) > 0.9999);
        Assert.True(Vector3d.Dot(frames[0].Normal, frames[1].Normal) > 0.9999);
        Assert.True(Vector3d.Dot(frames[1].Normal, frames[2].Normal) > 0.9999);
        Assert.True(Vector3d.Dot(frames[0].Binormal, frames[1].Binormal) > 0.9999);
        Assert.True(Vector3d.Dot(frames[1].Binormal, frames[2].Binormal) > 0.9999);
    }

    [Fact]
    public void QuarterLoopLikeFrames_RemainContinuousFiniteAndRightHanded()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.QuarterLoopLike();
        var evaluator = new TrackEvaluator(fixture.Document);
        var distances = new double[65];
        for (int i = 0; i < distances.Length; i++)
        {
            distances[i] = fixture.Document.TotalLength * i / (distances.Length - 1.0);
        }

        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(distances);

        for (int i = 0; i < frames.Length; i++)
        {
            AssertValidFrame(frames[i]);
            if (i > 0)
            {
                Assert.True(Vector3d.Dot(frames[i - 1].Normal, frames[i].Normal) > 0.0);
                Assert.True(Vector3d.Dot(frames[i - 1].Binormal, frames[i].Binormal) > 0.0);
            }
        }
    }

    [Fact]
    public void PositiveSegmentRoll_PreservesExpectedSignAndHandedness()
    {
        var document = new TrackDocument(new[]
        {
            new StraightSegment(length: 10.0, rollRadians: System.Math.PI * 0.5)
        });
        ExportTrackFrame frame = new TrackEvaluator(document).EvaluateFrameAtDistance(5.0);

        AssertVectorNear(Vector3d.UnitX, frame.Tangent);
        AssertVectorNear(Vector3d.UnitZ, frame.Normal);
        AssertVectorNear(new Vector3d(0.0, -1.0, 0.0), frame.Binormal);
        AssertValidFrame(frame);
    }

    [Fact]
    public void BankingAndPhysicsConsumers_AgreeWithCanonicalEvaluatorAtStation()
    {
        const double roll = 0.35;
        var document = new TrackDocument(new[]
        {
            new StraightSegment(length: 12.0, rollRadians: roll)
        });
        var evaluator = new TrackEvaluator(document);
        var profile = new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, roll, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(12.0, roll, BankingProfileInterpolationMode.Constant)
        });
        const double distance = 7.25;

        ExportTrackFrame canonical = evaluator.EvaluateFrameAtDistance(distance);
        ExportTrackFrame banked = BankingProfileSampler.SampleFramesAtDistances(
            document,
            evaluator,
            profile,
            new[] { distance })[0];
        ExportTrackFrame physics = new TrackPhysicsAdapter(evaluator).GetFrameAtDistance(document, distance);

        AssertFrameNear(canonical, banked);
        Assert.InRange(System.Math.Abs(canonical.Distance - physics.Distance), 0.0, Tolerance);
        AssertVectorNear(canonical.Position, physics.Position);
        AssertVectorNear(canonical.Tangent, physics.Tangent);
        AssertVectorNear(canonical.Normal, physics.Normal);
        AssertVectorNear(canonical.Binormal, physics.Binormal);
    }

    [Fact]
    public void BankingBatch_PreservesUnorderedAndDuplicateStations()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.QuarterLoopLike();
        double a = fixture.Document.TotalLength * 0.25;
        double b = fixture.Document.TotalLength * 0.7;
        var profile = new BankingProfile(new[]
        {
            new BankingProfileKey(0.0, 0.2, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(fixture.Document.TotalLength, 0.2, BankingProfileInterpolationMode.Constant)
        });

        ExportTrackFrame[] frames = BankingProfileSampler.SampleFramesAtDistances(
            fixture.Document,
            profile,
            new[] { b, a, b });
        ExportTrackFrame[] reversed = BankingProfileSampler.SampleFramesAtDistances(
            fixture.Document,
            profile,
            new[] { a, b });

        AssertFrameNear(frames[0], frames[2]);
        AssertFrameNear(frames[0], reversed[1]);
        AssertFrameNear(frames[1], reversed[0]);
    }

    [Fact]
    public void TrainBodiesAndBogies_MatchCanonicalFramesAtTheirStations()
    {
        DiagnosticTrackFixture fixture = DiagnosticTrackFixtures.CrestHill();
        var evaluator = new TrackEvaluator(fixture.Document);
        var provider = new TrainCarTransformProvider(evaluator);
        double leadDistance = fixture.Document.TotalLength - 3.0;

        IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
            leadDistance,
            carCount: 3,
            carSpacing: 4.0,
            bogieSpacing: 2.0);

        foreach (TrainCarWithBogiesTransform car in cars)
        {
            AssertFrameNear(evaluator.EvaluateFrameAtDistance(car.Body.Distance), car.Body.Frame);
            AssertFrameNear(evaluator.EvaluateFrameAtDistance(car.FrontBogie.Distance), car.FrontBogie.Frame);
            AssertFrameNear(evaluator.EvaluateFrameAtDistance(car.RearBogie.Distance), car.RearBogie.Frame);
        }
    }

    [Fact]
    public void DefaultTransportDensity_ImplicitAndExplicitRuntimeOptionsMatch()
    {
        TrackDocument document = CreateThreeDimensionalDensityDocument();
        double[] distances = BuildUniformDistances(document.TotalLength, sampleCount: 21);

        ExportTrackFrame[] implicitDefault = new TrackEvaluator(
                new CompiledTrackRuntime(document))
            .EvaluateFramesAtDistances(distances);
        ExportTrackFrame[] explicitDefault = new TrackEvaluator(
                new CompiledTrackRuntime(document, TrackSamplingOptions.Default))
            .EvaluateFramesAtDistances(distances);

        for (int i = 0; i < distances.Length; i++)
        {
            AssertFrameNear(implicitDefault[i], explicitDefault[i]);
        }
    }

    [Fact]
    public void TransportSampleDensity_PreservesCenterlineAndConvergesOrientation()
    {
        TrackDocument document = CreateThreeDimensionalDensityDocument();
        double[] distances = BuildUniformDistances(document.TotalLength, sampleCount: 31);

        ExportTrackFrame[] sparse = SampleWithTransportSamples(
            document,
            transportSamplesPerSegment: 2,
            distances);
        ExportTrackFrame[] medium = SampleWithTransportSamples(
            document,
            transportSamplesPerSegment: 16,
            distances);
        ExportTrackFrame[] dense = SampleWithTransportSamples(
            document,
            transportSamplesPerSegment: 128,
            distances);

        AssertCenterlineSamplesNear(sparse, dense);
        AssertCenterlineSamplesNear(medium, dense);

        double sparseToDense = MaxNormalOrBinormalAngleDeltaRadians(sparse, dense);
        double mediumToDense = MaxNormalOrBinormalAngleDeltaRadians(medium, dense);

        Assert.True(
            sparseToDense > 1e-4,
            $"Expected sparse transport density to produce a measurable orientation delta, got {sparseToDense:R} radians.");
        Assert.True(
            mediumToDense < sparseToDense,
            $"Expected denser transport sampling to converge: sparse={sparseToDense:R}, medium={mediumToDense:R}.");
        Assert.InRange(mediumToDense, 0.0, 0.05);
    }

    private static TrackDocument CreateSmoothTwoSegmentDocument()
    {
        var first = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(3.0, 2.0, 0.0),
            new Vector3d(7.0, 4.0, 1.0),
            new Vector3d(10.0, 5.0, 2.0));
        var second = new CubicBezierCurve(
            new Vector3d(10.0, 5.0, 2.0),
            new Vector3d(13.0, 6.0, 3.0),
            new Vector3d(17.0, 5.0, 5.0),
            new Vector3d(20.0, 3.0, 7.0));

        return new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(new ArcLengthLUT(first).TotalLength, spline: first),
            new CurvedSegment(new ArcLengthLUT(second).TotalLength, spline: second)
        });
    }

    private static TrackDocument CreateThreeDimensionalDensityDocument()
    {
        var curve = new CubicBezierCurve(
            new Vector3d(0.0, 0.0, 0.0),
            new Vector3d(12.0, -4.0, 18.0),
            new Vector3d(-6.0, 22.0, 30.0),
            new Vector3d(28.0, 16.0, 44.0));

        return new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(
                new ArcLengthLUT(curve).TotalLength,
                id: "transport-density-3d",
                spline: curve)
        });
    }

    private static double[] BuildUniformDistances(double totalLength, int sampleCount)
    {
        var distances = new double[sampleCount];
        for (int i = 0; i < distances.Length; i++)
        {
            distances[i] = totalLength * i / (sampleCount - 1.0);
        }

        distances[distances.Length - 1] = totalLength;
        return distances;
    }

    private static ExportTrackFrame[] SampleWithTransportSamples(
        TrackDocument document,
        int transportSamplesPerSegment,
        IReadOnlyList<double> distances)
    {
        var options = new TrackSamplingOptions(
            TrackSamplingOptions.Default.ArcLengthSamples,
            TrackSamplingOptions.Default.ArcLengthTolerance,
            transportSamplesPerSegment);
        var evaluator = new TrackEvaluator(new CompiledTrackRuntime(document, options));
        return evaluator.EvaluateFramesAtDistances(distances);
    }

    private static void AssertCenterlineSamplesNear(
        IReadOnlyList<ExportTrackFrame> expected,
        IReadOnlyList<ExportTrackFrame> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertNear(expected[i].Distance, actual[i].Distance);
            AssertVectorNear(expected[i].Position, actual[i].Position);
            AssertVectorNear(expected[i].Tangent, actual[i].Tangent);
        }
    }

    private static double MaxNormalOrBinormalAngleDeltaRadians(
        IReadOnlyList<ExportTrackFrame> expected,
        IReadOnlyList<ExportTrackFrame> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        double max = 0.0;

        for (int i = 0; i < expected.Count; i++)
        {
            max = System.Math.Max(max, AngleDeltaRadians(expected[i].Normal, actual[i].Normal));
            max = System.Math.Max(max, AngleDeltaRadians(expected[i].Binormal, actual[i].Binormal));
        }

        return max;
    }

    private static double AngleDeltaRadians(Vector3d expected, Vector3d actual)
    {
        double dot = Vector3d.Dot(expected.Normalized(), actual.Normalized());
        return System.Math.Acos(Clamp(dot, -1.0, 1.0));
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static void AssertValidFrame(ExportTrackFrame frame)
    {
        Assert.True(IsFinite(frame.Position));
        Assert.True(IsFinite(frame.Tangent));
        Assert.True(IsFinite(frame.Normal));
        Assert.True(IsFinite(frame.Binormal));
        AssertNear(1.0, frame.Tangent.Length);
        AssertNear(1.0, frame.Normal.Length);
        AssertNear(1.0, frame.Binormal.Length);
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal));
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal));
        AssertNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal));
        AssertVectorNear(Vector3d.Cross(frame.Tangent, frame.Normal), frame.Binormal);
    }

    private static void AssertFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertNear(expected.X, actual.X);
        AssertNear(expected.Y, actual.Y);
        AssertNear(expected.Z, actual.Z);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }

    private static bool IsFinite(Vector3d vector)
    {
        return IsFinite(vector.X) && IsFinite(vector.Y) && IsFinite(vector.Z);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
