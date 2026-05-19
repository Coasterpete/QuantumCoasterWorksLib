using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class GeometricSectionCompositeTrackDocumentBuilderTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void BuildZeroRollCompositeDocument_UsesSingleCompositeCurveSegment()
    {
        GeometricSection[] sections = CreateZeroRollSections();

        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(
            sections,
            segmentId: "composite-centerline",
            forceSegmentReference: "force-sections");

        TrackSegment segment = Assert.Single(document.Segments);
        CompositeSectionCurve curve = Assert.IsType<CompositeSectionCurve>(segment.Spline);

        Assert.Equal("composite-centerline", segment.Id);
        Assert.Equal("force-sections", segment.ForceSegmentReference);
        Assert.Equal(0.0, segment.RollRadians, 10);
        AssertDoubleNear(12.0, segment.Length);
        AssertDoubleNear(curve.TotalLength, segment.Length);
        Assert.Equal(sections.Length, document.Sections.Count);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(2.5)]
    [InlineData(5.0)]
    [InlineData(6.25)]
    [InlineData(8.0)]
    [InlineData(10.0)]
    [InlineData(12.0)]
    public void BuildZeroRollCompositeDocument_TrackEvaluatorPositionMatchesAssembledCurve(double distance)
    {
        GeometricSection[] sections = CreateZeroRollSections();
        CompositeSectionCurve expectedCurve = SectionCurveAssembler.Assemble(sections);
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(sections);
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);

        AssertDoubleNear(distance, frame.Distance);
        AssertVectorNear(expectedCurve.Evaluate(distance), frame.Position);
        AssertVectorNear(expectedCurve.Tangent(distance).Normalized(), frame.Tangent);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(8.0)]
    public void BuildZeroRollCompositeDocument_PositionIsContinuousAcrossSectionBoundaries(double boundaryDistance)
    {
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(
            CreateZeroRollSections());
        var evaluator = new TrackEvaluator(document);
        const double epsilon = 1e-7;

        Vector3d before = evaluator.EvaluateFrameAtDistance(boundaryDistance - epsilon).Position;
        Vector3d at = evaluator.EvaluateFrameAtDistance(boundaryDistance).Position;
        Vector3d after = evaluator.EvaluateFrameAtDistance(boundaryDistance + epsilon).Position;

        AssertVectorNear(before, at, tolerance: 1e-5);
        AssertVectorNear(at, after, tolerance: 1e-5);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(8.0)]
    public void BuildZeroRollCompositeDocument_TangentAtTouchingBoundariesIsDeterministic(double boundaryDistance)
    {
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(
            CreateZeroRollSections());
        var evaluator = new TrackEvaluator(document);
        var curve = Assert.IsType<CompositeSectionCurve>(document.Segments[0].Spline);
        const double epsilon = 1e-8;

        ExportTrackFrame before = evaluator.EvaluateFrameAtDistance(boundaryDistance - epsilon);
        ExportTrackFrame at = evaluator.EvaluateFrameAtDistance(boundaryDistance);
        ExportTrackFrame after = evaluator.EvaluateFrameAtDistance(boundaryDistance + epsilon);
        ExportTrackFrame repeatedAt = evaluator.EvaluateFrameAtDistance(boundaryDistance);

        AssertVectorNear(curve.Tangent(boundaryDistance).Normalized(), at.Tangent);
        AssertVectorNear(at.Tangent, repeatedAt.Tangent);
        AssertVectorNear(before.Tangent, at.Tangent, tolerance: 1e-6);
        AssertVectorNear(at.Tangent, after.Tangent, tolerance: 1e-6);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(8.0)]
    [InlineData(12.0)]
    public void BuildZeroRollCompositeDocument_TrackFrameBasisRemainsOrthonormal(double distance)
    {
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(
            CreateZeroRollSections());
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);

        AssertOrthonormal(frame);
    }

    [Fact]
    public void BuildZeroRollCompositeDocument_FinalEndpointClampsDeterministically()
    {
        GeometricSection[] sections = CreateZeroRollSections();
        CompositeSectionCurve expectedCurve = SectionCurveAssembler.Assemble(sections);
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(sections);
        var unboundEvaluator = new TrackEvaluator();
        var boundEvaluator = new TrackEvaluator(document);

        TrackEvaluationPoint endpointPoint = unboundEvaluator.EvaluateAtDistance(document, expectedCurve.TotalLength);
        TrackEvaluationPoint beyondEndpointPoint = unboundEvaluator.EvaluateAtDistance(document, expectedCurve.TotalLength + 25.0);
        ExportTrackFrame endpointA = boundEvaluator.EvaluateFrameAtDistance(expectedCurve.TotalLength);
        ExportTrackFrame endpointB = boundEvaluator.EvaluateFrameAtDistance(expectedCurve.TotalLength + 25.0);

        Assert.Same(document.Segments[0], endpointPoint.Segment);
        Assert.Same(document.Segments[0], beyondEndpointPoint.Segment);
        AssertDoubleNear(1.0, endpointPoint.LocalT);
        AssertDoubleNear(1.0, beyondEndpointPoint.LocalT);
        AssertDoubleNear(expectedCurve.TotalLength, endpointA.Distance);
        AssertDoubleNear(expectedCurve.TotalLength, endpointB.Distance);
        AssertVectorNear(expectedCurve.Evaluate(expectedCurve.TotalLength), endpointA.Position);
        AssertVectorNear(endpointA.Position, endpointB.Position);
        AssertVectorNear(endpointA.Tangent, endpointB.Tangent);
        AssertOrthonormal(endpointB);
    }

    [Fact]
    public void BuildZeroRollCompositeDocument_NonZeroRollSectionIsRejected()
    {
        var sections = new[]
        {
            new GeometricSection(length: 5.0),
            new GeometricSection(length: 3.0, roll: 0.1)
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(sections));
    }

    [Fact]
    public void TrackEvaluator_DoesNotAutomaticallyUseTrackDocumentSections()
    {
        TrackDocument document = new TrackDocument(
            segments: null,
            sections: CreateZeroRollSections());
        var evaluator = new TrackEvaluator();

        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.EvaluateAtDistance(document, 0.0));
    }

    private static GeometricSection[] CreateZeroRollSections()
    {
        return new[]
        {
            new GeometricSection(length: 5.0, curvature: 0.12, roll: 0.0),
            new GeometricSection(length: 3.0),
            new GeometricSection(length: 4.0, curvature: -0.08, roll: 0.0)
        };
    }

    private static void AssertOrthonormal(ExportTrackFrame frame)
    {
        AssertUnit(frame.Tangent);
        AssertUnit(frame.Normal);
        AssertUnit(frame.Binormal);
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal));
        AssertVectorNear(Vector3d.Cross(frame.Tangent, frame.Normal), frame.Binormal);
    }

    private static void AssertUnit(Vector3d vector)
    {
        AssertDoubleNear(1.0, vector.Length);
    }

    private static void AssertVectorNear(
        Vector3d expected,
        Vector3d actual,
        double tolerance = Tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
