using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Quantum.Track;
using Xunit;
using ExportTrackFrame = Quantum.Track.TrackFrame;
using SplineTrackFrame = Quantum.Splines.TrackFrame;

namespace Quantum.Tests;

public sealed class CanonicalTrackSamplingTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void NonlinearParameterization_EqualStationIncrementsProduceEqualGeometricTravel()
    {
        var curve = new PowerCurve();
        var document = new TrackDocument(new[]
        {
            new CurvedSegment(length: 1.0, spline: curve)
        });
        var evaluator = new TrackEvaluator(document);

        double[] distances = { 0.0, 0.25, 0.5, 0.75, 1.0 };
        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(distances);

        for (int i = 0; i < distances.Length; i++)
        {
            AssertNear(distances[i], frames[i].Position.X);
            AssertNear(0.0, frames[i].Position.Y);
            AssertNear(0.0, frames[i].Position.Z);
        }

        for (int i = 1; i < frames.Length; i++)
        {
            double travel = (frames[i].Position - frames[i - 1].Position).Length;
            AssertNear(0.25, travel);
        }
    }

    [Fact]
    public void ScalarAndBatchSampling_PreserveUnorderedDuplicateDistances()
    {
        TrackDocument document = CreateTwoSegmentDocument();
        var evaluator = new TrackEvaluator(document);
        double[] distances = { 3.0, 0.25, 3.0, 2.0, -5.0, 99.0, 0.25 };

        TrackEvaluationPoint[] points = new TrackEvaluator().EvaluateAtDistances(document, distances);
        ExportTrackFrame[] frames = evaluator.EvaluateFramesAtDistances(distances);

        Assert.Equal(distances.Length, points.Length);
        Assert.Equal(distances.Length, frames.Length);

        for (int i = 0; i < distances.Length; i++)
        {
            TrackEvaluationPoint scalarPoint = new TrackEvaluator().EvaluateAtDistance(document, distances[i]);
            ExportTrackFrame scalarFrame = evaluator.EvaluateFrameAtDistance(distances[i]);

            Assert.Same(scalarPoint.Segment, points[i].Segment);
            AssertNear(scalarPoint.LocalT, points[i].LocalT);
            AssertVectorNear(scalarFrame.Position, frames[i].Position);
            AssertVectorNear(scalarFrame.Tangent, frames[i].Tangent);
        }

        AssertVectorNear(frames[0].Position, frames[2].Position);
        AssertVectorNear(frames[1].Position, frames[6].Position);
    }

    [Theory]
    [InlineData(-1.0, 0, 0.0)]
    [InlineData(0.0, 0, 0.0)]
    [InlineData(2.0, 1, 0.0)]
    [InlineData(5.0, 1, 1.0)]
    [InlineData(7.0, 1, 1.0)]
    public void SegmentBoundariesAndClamping_UseMeasuredStationLengths(
        double distance,
        int expectedSegmentIndex,
        double expectedLocalT)
    {
        TrackDocument document = CreateTwoSegmentDocument();
        var evaluator = new TrackEvaluator();

        TrackEvaluationPoint point = evaluator.EvaluateAtDistance(document, distance);

        Assert.Same(document.Segments[expectedSegmentIndex], point.Segment);
        AssertNear(expectedLocalT, point.LocalT);
    }

    [Fact]
    public void ExactLineCurve_PointTangentAndTransformUseGeometricDistance()
    {
        var curve = new LineCurve(
            new Vector3d(1.0, 2.0, 3.0),
            new Vector3d(4.0, 6.0, 3.0));
        var document = new TrackDocument(new[]
        {
            new StraightSegment(length: curve.Length, spline: curve)
        });
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(2.0);
        Transform3d transform = new TrackEvaluator().EvaluateTransformAtDistance(document, 2.0);

        AssertVectorNear(new Vector3d(2.2, 3.6, 3.0), frame.Position, 1e-12);
        AssertVectorNear(new Vector3d(0.6, 0.8, 0.0), frame.Tangent, 1e-12);
        AssertVectorNear(frame.Position, transform.Position, 1e-12);
    }

    [Fact]
    public void CurvatureSampling_UsesCanonicalDistanceToParameterMapping()
    {
        var curve = new PowerCurvatureCurve();
        var document = new TrackDocument(new[]
        {
            new CurvedSegment(length: 1.0, spline: curve)
        });
        var adapter = new TrackPhysicsAdapter();

        bool success = adapter.TryGetCurvatureAtDistance(document, 0.25, out double curvature);

        Assert.True(success);
        AssertNear(0.5, curvature);
    }

    [Fact]
    public void SupportFrameDistance_IsSegmentLocalGeometricDistance()
    {
        TrackDocument document = CreateTwoSegmentDocument();
        var evaluator = new TrackEvaluator();

        SplineTrackFrame frame = evaluator.EvaluateSplineFrameAtDistance(document, 3.25);

        AssertNear(1.25, frame.S);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDeclaredLength_IsRejectedBeforeSampling(double length)
    {
        var document = new TrackDocument(new[] { new StraightSegment(length) });

        Assert.Throws<InvalidOperationException>(() =>
            new TrackEvaluator().EvaluateAtDistances(document, new[] { 0.0, 1.0 }));
    }

    [Fact]
    public void InvalidRoll_IsRejectedBeforeSampling()
    {
        var document = new TrackDocument(new[]
        {
            new StraightSegment(1.0),
            new StraightSegment(1.0, rollRadians: double.NaN)
        });

        Assert.Throws<InvalidOperationException>(() =>
            new TrackEvaluator().EvaluateFramesAtDistances(document, new[] { 0.25, 1.25 }));
    }

    [Fact]
    public void InvalidTangent_IsRejectedBeforeSampling()
    {
        var document = new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(1.0),
            new CurvedSegment(1.0, spline: new InvalidTangentCurve())
        });

        Assert.Throws<InvalidOperationException>(() =>
            new TrackEvaluator().EvaluateAtDistances(document, new[] { 0.25, 1.25 }));
    }

    [Fact]
    public void NullSegment_IsRejectedBeforeSampling()
    {
        var document = new TrackDocument();
        document.Segments.Add(new StraightSegment(1.0));
        document.Segments.Add(null!);

        Assert.Throws<InvalidOperationException>(() =>
            new TrackEvaluator().EvaluateAtDistances(document, new[] { 0.25, 0.75 }));
    }

    [Fact]
    public void MismatchedDeclaredAndMeasuredLengths_AreRejected()
    {
        var document = new TrackDocument(new[]
        {
            new StraightSegment(
                length: 1.0,
                spline: new LineCurve(Vector3d.Zero, new Vector3d(2.0, 0.0, 0.0)))
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new TrackEvaluator().EvaluateAtDistance(document, 0.5));

        Assert.Contains("does not match measured geometric length", ex.Message);
    }

    private static TrackDocument CreateTwoSegmentDocument()
    {
        var nonlinear = new PowerCurve(scale: 2.0);
        var line = new LineCurve(
            new Vector3d(10.0, 0.0, 0.0),
            new Vector3d(13.0, 0.0, 0.0));

        return new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(length: 2.0, spline: nonlinear),
            new StraightSegment(length: line.Length, spline: line)
        });
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertVectorNear(expected, actual, Tolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }

    private class PowerCurve : IParamCurve
    {
        private readonly double _scale;

        public PowerCurve(double scale = 1.0)
        {
            _scale = scale;
        }

        public Vector3d Evaluate(double t)
        {
            return new Vector3d(_scale * t * t, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }

    private sealed class PowerCurvatureCurve : PowerCurve, IParamCurveCurvature
    {
        public bool TryGetCurvature(double t, out double curvature)
        {
            curvature = t;
            return true;
        }
    }

    private sealed class InvalidTangentCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            return new Vector3d(t, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.Zero;
        }
    }
}
