using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class GeometricSectionTrackDocumentBuilderTests
{
    private const double Tolerance = 1e-6;

    [Theory]
    [InlineData(0.0)]
    [InlineData(3.0)]
    [InlineData(6.0)]
    [InlineData(12.0)]
    public void Builder_EvaluateFrameAtDistance_PositionMatchesDirectGeneratedCurve(double distance)
    {
        var section = new GeometricSection(length: 12.0, curvature: 0.08, roll: -0.2);
        IParamCurve directCurve = section.GenerateCurve();
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildDocument(section);
        var evaluator = new TrackEvaluator(document);

        TrackEvaluationPoint point = new TrackEvaluator().EvaluateAtDistance(document, distance);
        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
        double expectedT = distance / section.Length;

        Assert.Same(document.Segments[0], point.Segment);
        AssertDoubleNear(expectedT, point.LocalT);
        AssertDoubleNear(distance, frame.Distance);
        AssertVectorNear(directCurve.Evaluate(expectedT), frame.Position);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(4.0)]
    [InlineData(8.0)]
    [InlineData(10.0)]
    public void Builder_EvaluateFrameAtDistance_TangentMatchesDirectGeneratedCurve(double distance)
    {
        var section = new GeometricSection(length: 10.0, curvature: -0.12, roll: 0.15);
        IParamCurve directCurve = section.GenerateCurve();
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildDocument(section);
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(distance);
        double expectedT = distance / section.Length;

        AssertVectorNear(directCurve.Tangent(expectedT).Normalized(), frame.Tangent);
    }

    [Fact]
    public void Builder_EvaluateFrameAtDistance_ProducesOrthonormalTrackFrameBasis()
    {
        var section = new GeometricSection(length: 14.0, curvature: 0.05, roll: 0.4);
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildDocument(section);
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(7.0);

        AssertUnit(frame.Tangent);
        AssertUnit(frame.Normal);
        AssertUnit(frame.Binormal);
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal));
        AssertVectorNear(Vector3d.Cross(frame.Tangent, frame.Normal), frame.Binormal);
    }

    [Fact]
    public void Builder_UsesGeometricSectionRollAsSegmentRollRadians()
    {
        const double rollRadians = 0.35;
        var section = new GeometricSection(length: 8.0, roll: rollRadians);
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildDocument(section);
        var evaluator = new TrackEvaluator(document);

        ExportTrackFrame frame = evaluator.EvaluateFrameAtDistance(4.0);

        Assert.Equal(rollRadians, document.Segments[0].RollRadians, 10);
        AssertVectorNear(Vector3d.UnitX, frame.Tangent);
        AssertVectorNear(
            new Vector3d(0.0, System.Math.Cos(rollRadians), System.Math.Sin(rollRadians)),
            frame.Normal);
        AssertVectorNear(
            new Vector3d(0.0, -System.Math.Sin(rollRadians), System.Math.Cos(rollRadians)),
            frame.Binormal);
    }

    [Fact]
    public void Builder_EvaluateAtDistance_BoundsAndEndpointBehaviorAreDeterministic()
    {
        var section = new GeometricSection(length: 10.0, curvature: -0.1, roll: 0.2);
        IParamCurve directCurve = section.GenerateCurve();
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildDocument(section);
        var evaluator = new TrackEvaluator(document);
        TrackSegment segment = document.Segments[0];

        TrackEvaluationPoint belowStart = new TrackEvaluator().EvaluateAtDistance(document, -1.0);
        TrackEvaluationPoint atEndpoint = new TrackEvaluator().EvaluateAtDistance(document, section.Length);
        TrackEvaluationPoint beyondEndpoint = new TrackEvaluator().EvaluateAtDistance(document, section.Length + 20.0);
        ExportTrackFrame belowStartFrame = evaluator.EvaluateFrameAtDistance(-1.0);
        ExportTrackFrame endpointA = evaluator.EvaluateFrameAtDistance(section.Length);
        ExportTrackFrame endpointB = evaluator.EvaluateFrameAtDistance(section.Length + 20.0);

        Assert.Same(segment, belowStart.Segment);
        Assert.Same(segment, atEndpoint.Segment);
        Assert.Same(segment, beyondEndpoint.Segment);
        AssertDoubleNear(0.0, belowStart.LocalT);
        AssertDoubleNear(1.0, atEndpoint.LocalT);
        AssertDoubleNear(1.0, beyondEndpoint.LocalT);

        AssertVectorNear(directCurve.Evaluate(0.0), belowStartFrame.Position);
        AssertVectorNear(directCurve.Evaluate(1.0), endpointA.Position);
        AssertVectorNear(endpointA.Position, endpointB.Position);
        AssertVectorNear(endpointA.Tangent, endpointB.Tangent);
        AssertDoubleNear(section.Length, endpointA.Distance);
        AssertDoubleNear(section.Length, endpointB.Distance);

        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.EvaluateFrameAtDistance(double.NaN));
    }

    private static void AssertUnit(Vector3d vector)
    {
        AssertDoubleNear(1.0, vector.Length);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
