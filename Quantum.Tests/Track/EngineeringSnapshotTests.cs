using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class EngineeringSnapshotTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Build_ProducesAlignedCanonicalDataAndSourceMetadata()
    {
        TrackAuthoringCompilation compilation = CreateCompilation();
        var request = new EngineeringSnapshotRequest(
            compilationRevision: 42,
            snapshotRevision: 7,
            stationSampleCount: 5);

        EngineeringSnapshot snapshot = EngineeringSnapshotBuilder.Build(compilation, request);

        Assert.Equal(EngineeringSnapshotRevisionMetadata.CurrentContractVersion,
            snapshot.Revision.ContractVersion);
        Assert.Equal(42, snapshot.Revision.CompilationRevision);
        Assert.Equal(7, snapshot.Revision.SnapshotRevision);
        Assert.Equal(5, snapshot.SampleCount);
        Assert.Equal(5, snapshot.Sampling.StationSampleCount);
        Assert.Equal(compilation.Runtime.SamplingOptions.ArcLengthSampleCount,
            snapshot.Sampling.ArcLengthSampleCount);
        Assert.Equal(compilation.Runtime.SamplingOptions.ArcLengthTolerance,
            snapshot.Sampling.ArcLengthTolerance);
        Assert.Equal(compilation.Runtime.SamplingOptions.TransportSamplesPerSegment,
            snapshot.Sampling.TransportSamplesPerSegment);
        Assert.Equal(new[] { 0.0, 2.5, 5.0, 7.5, 10.0 }, snapshot.StationGrid);
        Assert.Equal(snapshot.SampleCount, snapshot.Geometry.Count);
        Assert.Equal(snapshot.SampleCount, snapshot.OrientationFrames.Count);
        Assert.Equal(snapshot.SampleCount, snapshot.BankingRollRadians.Count);

        var evaluator = new TrackEvaluator(compilation.Runtime);
        TrackFrame[] expectedFrames = BankingProfileSampler.SampleFramesAtDistances(
            evaluator,
            compilation.BankingProfile,
            snapshot.StationGrid);
        for (int sampleIndex = 0; sampleIndex < snapshot.SampleCount; sampleIndex++)
        {
            EngineeringGeometrySample geometry = snapshot.Geometry[sampleIndex];
            TrackFrame frame = snapshot.OrientationFrames[sampleIndex];

            Assert.Equal(sampleIndex, geometry.SampleIndex);
            AssertNear(snapshot.StationGrid[sampleIndex], geometry.Station);
            AssertFrameNear(expectedFrames[sampleIndex], frame);
            AssertVectorNear(frame.Position, geometry.Position);
            AssertVectorNear(frame.Tangent, geometry.Tangent);
            Assert.True(geometry.HasCurvature);
            Assert.True(evaluator.TryGetCurvatureAtDistance(
                geometry.Station,
                out double expectedCurvature));
            AssertNear(expectedCurvature, geometry.CurvatureMagnitude!.Value);
            AssertNear(
                BankingProfileSampler.SampleRollRadians(
                    compilation.BankingProfile,
                    geometry.Station),
                snapshot.BankingRollRadians[sampleIndex]);
        }

        Assert.Collection(
            snapshot.ResolvedSections,
            section =>
            {
                Assert.Equal(0, section.SectionIndex);
                Assert.Equal("spatial", section.SectionId);
                AssertNear(0.0, section.StartStation);
                AssertNear(6.0, section.EndStation);
                Assert.False(section.IncludesEndStation);
            },
            section =>
            {
                Assert.Equal(1, section.SectionIndex);
                Assert.Equal("turn", section.SectionId);
                AssertNear(6.0, section.StartStation);
                AssertNear(10.0, section.EndStation);
                Assert.True(section.IncludesEndStation);
            });

        EngineeringSectionBoundaryMetadata boundary = Assert.Single(snapshot.SectionBoundaries);
        Assert.Equal(0, boundary.BoundaryIndex);
        AssertNear(6.0, boundary.Station);
        Assert.Equal(0, boundary.UpstreamSectionIndex);
        Assert.Equal("spatial", boundary.UpstreamSectionId);
        Assert.Equal(1, boundary.DownstreamSectionIndex);
        Assert.Equal("turn", boundary.DownstreamSectionId);

        Assert.Equal(4, snapshot.ControlPoints.Count);
        for (int controlPointIndex = 0;
             controlPointIndex < snapshot.ControlPoints.Count;
             controlPointIndex++)
        {
            EngineeringControlPointMetadata controlPoint =
                snapshot.ControlPoints[controlPointIndex];
            Assert.Equal("spatial", controlPoint.SectionId);
            Assert.Equal(0, controlPoint.SectionIndex);
            Assert.Equal(controlPointIndex, controlPoint.ControlPointIndex);
            AssertVectorNear(
                new Vector3d(controlPointIndex * 2.0, 0.0, 0.0),
                controlPoint.LocalPosition);
            AssertNear(1.0, controlPoint.Weight);
            Assert.Null(controlPoint.AuthoringId);
        }

        Assert.Collection(
            snapshot.ProfileKeys,
            key => AssertProfileKey(key, 0, 0.0, 0.0),
            key => AssertProfileKey(key, 1, 5.0, 0.2),
            key => AssertProfileKey(key, 2, 10.0, -0.1));
    }

    [Fact]
    public void Build_CopiesCollectionsAndDoesNotRetainMutableDocumentState()
    {
        TrackAuthoringCompilation compilation = CreateCompilation();
        EngineeringSnapshot snapshot = EngineeringSnapshotBuilder.Build(
            compilation,
            new EngineeringSnapshotRequest(3, 4, 4));
        double expectedTotalLength = snapshot.TotalLength;
        TrackFrame expectedEndFrame = snapshot.OrientationFrames[^1];

        Assert.Throws<NotSupportedException>(
            () => ((IList<double>)snapshot.StationGrid)[0] = 99.0);
        Assert.Throws<NotSupportedException>(
            () => ((IList<TrackFrame>)snapshot.OrientationFrames).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<EngineeringResolvedSectionMetadata>)snapshot.ResolvedSections).Clear());

        Vector3d positionCopy = snapshot.Geometry[0].Position;
        positionCopy.X = 99.0;
        compilation.Document.Segments.Clear();

        AssertNear(expectedTotalLength, snapshot.TotalLength);
        AssertNear(0.0, snapshot.StationGrid[0]);
        AssertVectorNear(Vector3d.Zero, snapshot.Geometry[0].Position);
        AssertFrameNear(expectedEndFrame, snapshot.OrientationFrames[^1]);
        Assert.Equal(2, snapshot.ResolvedSections.Count);
    }

    [Fact]
    public void Build_IsDeterministicForTheSameCompilationAndRequest()
    {
        TrackAuthoringCompilation compilation = CreateCompilation();
        var request = new EngineeringSnapshotRequest(9, 12, 9);

        EngineeringSnapshot first = EngineeringSnapshotBuilder.Build(compilation, request);
        EngineeringSnapshot second = EngineeringSnapshotBuilder.Build(compilation, request);

        Assert.Equal(first.StationGrid.ToArray(), second.StationGrid.ToArray());
        Assert.Equal(first.BankingRollRadians.ToArray(), second.BankingRollRadians.ToArray());
        Assert.Equal(first.ResolvedSections.ToArray(), second.ResolvedSections.ToArray());
        Assert.Equal(first.SectionBoundaries.ToArray(), second.SectionBoundaries.ToArray());
        Assert.Equal(first.ControlPoints.ToArray(), second.ControlPoints.ToArray());
        Assert.Equal(first.ProfileKeys.ToArray(), second.ProfileKeys.ToArray());

        for (int sampleIndex = 0; sampleIndex < first.SampleCount; sampleIndex++)
        {
            AssertFrameNear(
                first.OrientationFrames[sampleIndex],
                second.OrientationFrames[sampleIndex]);
            AssertVectorNear(
                first.Geometry[sampleIndex].Position,
                second.Geometry[sampleIndex].Position);
            AssertNear(
                first.Geometry[sampleIndex].CurvatureMagnitude!.Value,
                second.Geometry[sampleIndex].CurvatureMagnitude!.Value);
        }
    }

    [Fact]
    public void RequestAndBuilder_RejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EngineeringSnapshotRequest(-1, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EngineeringSnapshotRequest(0, -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EngineeringSnapshotRequest(0, 0, 1));

        TrackAuthoringCompilation compilation = CreateCompilation();
        var request = new EngineeringSnapshotRequest(0, 0, 2);
        Assert.Throws<ArgumentNullException>(
            () => EngineeringSnapshotBuilder.Build(null!, request));
        Assert.Throws<ArgumentNullException>(
            () => EngineeringSnapshotBuilder.Build(compilation, null!));
    }

    private static TrackAuthoringCompilation CreateCompilation()
    {
        var spatial = new SpatialSectionDefinition(
            "spatial",
            6.0,
            new[]
            {
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(2.0, 0.0, 0.0),
                new Vector3d(4.0, 0.0, 0.0),
                new Vector3d(6.0, 0.0, 0.0)
            });
        var turn = new ConstantCurvatureSectionDefinition(
            "turn",
            4.0,
            radius: 20.0,
            rollRadians: -0.1);
        var banking = new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(
                0.0,
                0.0,
                BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(
                5.0,
                0.2,
                BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(10.0, -0.1)
        });

        return TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(
                new GeometricSectionDefinition[] { spatial, turn },
                TrackStartPose.Identity,
                banking));
    }

    private static void AssertProfileKey(
        EngineeringProfileKeyMetadata actual,
        int expectedIndex,
        double expectedStation,
        double expectedRollRadians)
    {
        Assert.Equal(expectedIndex, actual.KeyIndex);
        AssertNear(expectedStation, actual.Station);
        AssertNear(expectedRollRadians, actual.RollRadians);
        Assert.Equal(EngineeringProfileKeySource.AuthoredBanking, actual.Source);
        Assert.Null(actual.AuthoringId);
    }

    private static void AssertFrameNear(TrackFrame expected, TrackFrame actual)
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
}
