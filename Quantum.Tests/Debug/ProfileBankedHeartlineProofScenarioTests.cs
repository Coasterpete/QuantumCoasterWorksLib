using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class ProfileBankedHeartlineProofScenarioTests
{
    private const double Tolerance = 1e-8;

    [Fact]
    public void CreateDeterministic_AuthorsFiveZeroRollSectionsOverFiftySixMeters()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        TrackAuthoringCompilation compilation = scenario.Compilation;
        string[] expectedIds =
        {
            "profile-banked-heartline-entry",
            "profile-banked-heartline-rise-turn",
            "profile-banked-heartline-elevated-hold",
            "profile-banked-heartline-descend-counterturn",
            "profile-banked-heartline-exit"
        };
        double[] expectedLengths = { 8.0, 16.0, 8.0, 16.0, 8.0 };
        double[] expectedStarts = { 0.0, 8.0, 24.0, 32.0, 48.0 };
        double[] expectedEnds = { 8.0, 24.0, 32.0, 48.0, 56.0 };

        Assert.Same(scenario.Definition, compilation.Definition);
        Assert.Same(scenario.AuthoredBanking, scenario.Definition.Banking);
        Assert.Equal(5, scenario.Definition.Sections.Count);
        Assert.Equal(5, compilation.Document.Segments.Count);
        Assert.Equal(5, compilation.Document.Sections.Count);
        Assert.Equal(5, compilation.ResolvedSections.Count);

        for (int i = 0; i < expectedIds.Length; i++)
        {
            GeometricSectionDefinition authored = scenario.Definition.Sections[i];
            TrackSegment segment = compilation.Document.Segments[i];
            ResolvedSectionInterval<GeometricSectionDefinition> resolved =
                compilation.ResolvedSections[i];

            Assert.Equal(expectedIds[i], authored.Id);
            Assert.Equal(expectedIds[i], segment.Id);
            Assert.Equal(expectedLengths[i], authored.Length);
            Assert.Equal(expectedLengths[i], segment.Length);
            Assert.Equal(0.0, authored.RollRadians);
            Assert.Equal(0.0, segment.RollRadians);
            Assert.Same(authored, resolved.Section);
            Assert.Equal(expectedStarts[i], resolved.StartDistance);
            Assert.Equal(expectedEnds[i], resolved.EndDistance);
            Assert.Equal(expectedLengths[i], resolved.Length);
        }

        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[0]);
        SpatialSectionDefinition riseTurn =
            Assert.IsType<SpatialSectionDefinition>(scenario.Definition.Sections[1]);
        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[2]);
        SpatialSectionDefinition descendCounterturn =
            Assert.IsType<SpatialSectionDefinition>(scenario.Definition.Sections[3]);
        Assert.IsType<StraightSectionDefinition>(scenario.Definition.Sections[4]);
        AssertStraightEndpointRuns(riseTurn);
        AssertStraightEndpointRuns(descendCounterturn);
        Assert.Equal(ProfileBankedHeartlineProofScenario.TotalLength, compilation.TotalLength);
        Assert.Equal(compilation.TotalLength, compilation.Document.TotalLength);
    }

    [Fact]
    public void CreateDeterministic_UsesExpectedExplicitAuthoredBankingKeys()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        IReadOnlyList<BankingProfileKey> authoredKeys = scenario.AuthoredBanking.Keys;
        IReadOnlyList<BankingProfileKey> compiledKeys = scenario.Compilation.BankingProfile.Keys;

        Assert.NotSame(authoredKeys, compiledKeys);
        Assert.Equal(6, authoredKeys.Count);
        AssertKey(
            authoredKeys[0],
            0.0,
            ToRadians(0.0),
            BankingProfileInterpolationMode.Linear);
        AssertKey(
            authoredKeys[1],
            8.0,
            ToRadians(15.0),
            BankingProfileInterpolationMode.SmoothStep);
        AssertKey(
            authoredKeys[2],
            24.0,
            ToRadians(30.0),
            BankingProfileInterpolationMode.Constant);
        AssertKey(
            authoredKeys[3],
            32.0,
            ToRadians(30.0),
            BankingProfileInterpolationMode.SmoothStep);
        AssertKey(
            authoredKeys[4],
            48.0,
            ToRadians(-20.0),
            BankingProfileInterpolationMode.Linear);
        AssertKey(
            authoredKeys[5],
            56.0,
            ToRadians(0.0),
            BankingProfileInterpolationMode.Constant);
        AssertKeysEqual(authoredKeys, compiledKeys);
    }

    [Fact]
    public void CreateDeterministic_SpatialGeometryHasMeaningfulThreeDimensionalExtent()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        var evaluator = new TrackEvaluator(scenario.Compilation.Runtime);
        double minimumY = double.PositiveInfinity;
        double maximumY = double.NegativeInfinity;
        double minimumZ = double.PositiveInfinity;
        double maximumZ = double.NegativeInfinity;

        for (int i = 0; i < scenario.SampleDistances.Count; i++)
        {
            TrackFrame expected = evaluator.EvaluateFrameAtDistance(scenario.SampleDistances[i]);
            TrackFrame actual = scenario.DefaultCenterlineFrames[i];
            AssertTrackFrameNear(expected, actual);

            minimumY = System.Math.Min(minimumY, actual.Position.Y);
            maximumY = System.Math.Max(maximumY, actual.Position.Y);
            minimumZ = System.Math.Min(minimumZ, actual.Position.Z);
            maximumZ = System.Math.Max(maximumZ, actual.Position.Z);
        }

        Assert.True(maximumY - minimumY > 3.0);
        Assert.True(maximumZ - minimumZ > 5.0);
        Assert.True(scenario.DefaultCenterlineFrames[6].Position.Y > 3.0);
        Assert.True(scenario.DefaultCenterlineFrames[^1].Position.Y <
            scenario.DefaultCenterlineFrames[6].Position.Y);
    }

    [Fact]
    public void CreateDeterministic_GeometryContinuityReportHasNoDiagnostics()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();

        Assert.Equal(4, scenario.GeometryContinuity.BoundaryCount);
        Assert.Equal(
            new[] { 8.0, 24.0, 32.0, 48.0 },
            scenario.GeometryContinuity.Boundaries.Select(boundary => boundary.Station));
        Assert.Empty(scenario.GeometryContinuity.Diagnostics);
        Assert.Equal(0, scenario.GeometryContinuity.DiagnosticCount);
        Assert.False(scenario.GeometryContinuity.HasDiagnostics);
    }

    [Fact]
    public void CreateDeterministic_BankingDiagnosticsPassWithExpectedRange()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        TrackAuthoringBankingDiagnosticsReport report = scenario.BankingDiagnostics;

        Assert.Equal(
            TrackAuthoringBankingProfileSourceKind.ExplicitAuthored,
            report.SourceKind);
        Assert.Equal(0.0, report.Coverage.ExpectedStartDistance);
        Assert.Equal(0.0, report.Coverage.ActualStartDistance);
        Assert.Equal(ProfileBankedHeartlineProofScenario.TotalLength, report.Coverage.ExpectedEndDistance);
        Assert.Equal(ProfileBankedHeartlineProofScenario.TotalLength, report.Coverage.ActualEndDistance);
        Assert.True(report.Coverage.StartsAtTrackStart);
        Assert.True(report.Coverage.EndsAtTrackEnd);
        Assert.True(report.Coverage.Passes);
        Assert.Empty(report.Diagnostics);
        Assert.False(report.HasDiagnostics);
        Assert.Empty(report.ContinuousRollWarnings);
        Assert.False(report.ContinuousRollReport.HasWarnings);
        AssertNear(ToRadians(-20.0), report.Summary.MinRollRadians);
        AssertNear(ToRadians(30.0), report.Summary.MaxRollRadians);
        AssertNear(-20.0, report.Summary.MinRollDegrees);
        AssertNear(30.0, report.Summary.MaxRollDegrees);
    }

    [Fact]
    public void CreateDeterministic_SamplesFifteenExactStationsEveryFourMeters()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();

        Assert.Equal(ProfileBankedHeartlineProofScenario.SampleCount, scenario.SampleDistances.Count);
        Assert.Equal(ProfileBankedHeartlineProofScenario.SampleCount, scenario.DefaultCenterlineFrames.Count);
        Assert.Equal(ProfileBankedHeartlineProofScenario.SampleCount, scenario.ProfileBankedFrames.Count);
        Assert.Equal(ProfileBankedHeartlineProofScenario.SampleCount, scenario.DefaultHeartlineFrames.Count);
        Assert.Equal(
            ProfileBankedHeartlineProofScenario.SampleCount,
            scenario.ProfileBankedHeartlineFrames.Count);

        for (int i = 0; i < ProfileBankedHeartlineProofScenario.SampleCount; i++)
        {
            double expectedDistance = i * ProfileBankedHeartlineProofScenario.SampleInterval;
            Assert.Equal(expectedDistance, scenario.SampleDistances[i]);
            Assert.Equal(expectedDistance, scenario.DefaultCenterlineFrames[i].Distance);
            Assert.Equal(expectedDistance, scenario.ProfileBankedFrames[i].Distance);
            Assert.Equal(expectedDistance, scenario.DefaultHeartlineFrames[i].Distance);
            Assert.Equal(expectedDistance, scenario.ProfileBankedHeartlineFrames[i].Distance);
            AssertVectorNear(
                scenario.DefaultCenterlineFrames[i].Position,
                scenario.DefaultHeartlineFrames[i].CenterlinePosition);
            AssertVectorNear(
                scenario.ProfileBankedFrames[i].Position,
                scenario.ProfileBankedHeartlineFrames[i].CenterlinePosition);
            AssertVectorNear(
                scenario.DefaultCenterlineFrames[i].Position,
                scenario.ProfileBankedFrames[i].Position);
        }
    }

    [Fact]
    public void CreateDeterministic_ZeroRollStationsHaveDefaultAndProfileHeartlineParity()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        int parityStationCount = 0;

        for (int i = 0; i < scenario.SampleDistances.Count; i++)
        {
            double rollRadians = BankingProfileSampler.SampleRollRadians(
                scenario.Compilation.BankingProfile,
                scenario.SampleDistances[i]);
            if (System.Math.Abs(rollRadians) > Tolerance)
            {
                continue;
            }

            parityStationCount++;
            AssertHeartlineFrameNear(
                scenario.DefaultHeartlineFrames[i],
                scenario.ProfileBankedHeartlineFrames[i]);
            AssertTrackFrameNear(
                scenario.DefaultCenterlineFrames[i],
                scenario.ProfileBankedFrames[i]);
        }

        Assert.Equal(2, parityStationCount);
    }

    [Fact]
    public void CreateDeterministic_NonzeroRollStationsUseProfileNormalForOffsetDirection()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        int changedStationCount = 0;

        for (int i = 0; i < scenario.SampleDistances.Count; i++)
        {
            double rollRadians = BankingProfileSampler.SampleRollRadians(
                scenario.Compilation.BankingProfile,
                scenario.SampleDistances[i]);
            if (System.Math.Abs(rollRadians) <= Tolerance)
            {
                continue;
            }

            changedStationCount++;
            Vector3d defaultOffset =
                scenario.DefaultHeartlineFrames[i].Position -
                scenario.DefaultHeartlineFrames[i].CenterlinePosition;
            Vector3d profileOffset =
                scenario.ProfileBankedHeartlineFrames[i].Position -
                scenario.ProfileBankedHeartlineFrames[i].CenterlinePosition;
            Vector3d expectedProfileOffset =
                scenario.ProfileBankedFrames[i].Normal *
                scenario.HeartlineOffset.NormalOffsetMeters;

            AssertVectorNear(expectedProfileOffset, profileOffset);
            Assert.True(
                (profileOffset - defaultOffset).Length > 1e-4,
                $"Expected profile-banked offset direction to differ at {scenario.SampleDistances[i]} m.");
        }

        Assert.Equal(ProfileBankedHeartlineProofScenario.SampleCount - 2, changedStationCount);
    }

    [Fact]
    public void CreateDeterministic_ProfileBankedHeartlineTangentInheritsSourceFrameTangent()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();

        for (int i = 0; i < scenario.SampleDistances.Count; i++)
        {
            AssertVectorNear(
                scenario.ProfileBankedFrames[i].Tangent,
                scenario.ProfileBankedHeartlineFrames[i].Tangent);
            AssertVectorNear(
                scenario.ProfileBankedFrames[i].Normal,
                scenario.ProfileBankedHeartlineFrames[i].Normal);
            AssertVectorNear(
                scenario.ProfileBankedFrames[i].Binormal,
                scenario.ProfileBankedHeartlineFrames[i].Binormal);
        }
    }

    [Fact]
    public void CreateDeterministic_RepeatedCreationIsDeterministic()
    {
        ProfileBankedHeartlineProofScenario first =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        ProfileBankedHeartlineProofScenario second =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();

        Assert.Equal(first.SampleDistances, second.SampleDistances);
        AssertKeysEqual(first.AuthoredBanking.Keys, second.AuthoredBanking.Keys);
        AssertKeysEqual(first.Compilation.BankingProfile.Keys, second.Compilation.BankingProfile.Keys);
        Assert.Equal(first.GeometryContinuity.DiagnosticCount, second.GeometryContinuity.DiagnosticCount);
        Assert.Equal(first.BankingDiagnostics.DiagnosticCount, second.BankingDiagnostics.DiagnosticCount);

        for (int i = 0; i < first.SampleDistances.Count; i++)
        {
            AssertTrackFrameNear(first.DefaultCenterlineFrames[i], second.DefaultCenterlineFrames[i]);
            AssertTrackFrameNear(first.ProfileBankedFrames[i], second.ProfileBankedFrames[i]);
            AssertHeartlineFrameNear(first.DefaultHeartlineFrames[i], second.DefaultHeartlineFrames[i]);
            AssertHeartlineFrameNear(
                first.ProfileBankedHeartlineFrames[i],
                second.ProfileBankedHeartlineFrames[i]);
        }
    }

    [Fact]
    public void CreateDeterministic_DoesNotAddDebugCommandOrChangePublicDefaultContracts()
    {
        ProfileBankedHeartlineProofScenario scenario =
            ProfileBankedHeartlineProofScenario.CreateDeterministic();
        var evaluator = new TrackEvaluator(scenario.Compilation.Runtime);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> before = provider.EvaluateCarTransforms(
            leadDistance: 24.0,
            carSpacing: 6.0,
            carCount: 3);
        _ = HeartlineSampler.SampleAtDistances(
            evaluator,
            scenario.Compilation.BankingProfile,
            scenario.HeartlineOffset,
            scenario.SampleDistances);
        IReadOnlyList<TrainCarTransform> after = provider.EvaluateCarTransforms(
            leadDistance: 24.0,
            carSpacing: 6.0,
            carCount: 3);

        Assert.False(DebugCommandParser.TryParse(
            new[] { ProfileBankedHeartlineProofScenario.FixtureName },
            out _));
        Assert.DoesNotContain(
            Enum.GetNames<DebugCommandKind>(),
            name => name.Contains("Heartline", StringComparison.Ordinal));
        Assert.Equal("quantum.debug_viewport_snapshot", DebugViewportSnapshotV1Dto.ContractName);
        Assert.Equal(1, DebugViewportSnapshotV1Dto.ContractVersion);
        Assert.Equal("quantum.train_pose", TrainPoseExportV1Dto.ContractName);
        Assert.Equal(1, TrainPoseExportV1Dto.ContractVersion);

        TrackFrame[] defaultFramesAfterProfileSampling =
            evaluator.EvaluateFramesAtDistances(scenario.SampleDistances);
        for (int i = 0; i < scenario.SampleDistances.Count; i++)
        {
            AssertTrackFrameNear(
                scenario.DefaultCenterlineFrames[i],
                defaultFramesAfterProfileSampling[i]);
        }

        Assert.Equal(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            AssertTrainCarTransformNear(before[i], after[i]);
        }
    }

    private static void AssertKeysEqual(
        IReadOnlyList<BankingProfileKey> expected,
        IReadOnlyList<BankingProfileKey> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertKey(
                actual[i],
                expected[i].Distance,
                expected[i].RollRadians,
                expected[i].InterpolationToNext);
        }
    }

    private static void AssertKey(
        BankingProfileKey key,
        double expectedDistance,
        double expectedRollRadians,
        BankingProfileInterpolationMode expectedMode)
    {
        Assert.Equal(expectedDistance, key.Distance);
        AssertNear(expectedRollRadians, key.RollRadians);
        Assert.Equal(expectedMode, key.InterpolationToNext);
    }

    private static void AssertStraightEndpointRuns(SpatialSectionDefinition section)
    {
        IReadOnlyList<Vector3d> points = section.ControlPoints;
        AssertNear(0.0, Vector3d.Cross(points[1] - points[0], points[2] - points[1]).Length);
        int last = points.Count - 1;
        AssertNear(0.0, Vector3d.Cross(points[last - 1] - points[last - 2], points[last] - points[last - 1]).Length);
    }

    private static void AssertTrainCarTransformNear(TrainCarTransform expected, TrainCarTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        AssertNear(expected.Distance, actual.Distance);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
    }

    private static void AssertTrackFrameNear(TrackFrame expected, TrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertHeartlineFrameNear(HeartlineFrame expected, HeartlineFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.CenterlinePosition, actual.CenterlinePosition);
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

    private static double ToRadians(double degrees)
    {
        return degrees * System.Math.PI / 180.0;
    }
}
