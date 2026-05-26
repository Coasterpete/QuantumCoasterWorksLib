using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.Fixtures.Csv;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class DebugViewportSnapshotV1FixturePackTests
{
    [Theory]
    [MemberData(
        nameof(DebugViewportSnapshotV1FixtureTestHelper.Milestone7FixtureData),
        MemberType = typeof(DebugViewportSnapshotV1FixtureTestHelper))]
    public void Milestone7Fixture_ParseMapValidateAndRoundtrip_IsDeterministic(
        CenterlineFrameCsvFixtureSpec fixtureSpec)
    {
        CenterlineFrameCsvFixture fixture = DebugViewportSnapshotV1FixtureTestHelper.ParseFixture(fixtureSpec);

        Assert.Equal(fixtureSpec.FileName, fixture.SourceFixtureName);
        Assert.Equal("meters", fixture.Units);
        Assert.Equal(fixtureSpec.ExpectedRowCount, fixture.Frames.Count);
        Assert.Equal(fixtureSpec.ExpectedFirstDistance, fixture.Frames[0].Distance);
        Assert.Equal(fixtureSpec.ExpectedLastDistance, fixture.Frames[fixture.Frames.Count - 1].Distance);
        AssertDistanceOrderPreserved(fixture.Frames);

        DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1FromCsvCommand.BuildSnapshot(fixture);

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, dto.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, dto.Version);
        Assert.Equal("meters", dto.Metadata.Units);
        Assert.Equal(fixtureSpec.FileName, dto.Metadata.SourceFixtureName);
        Assert.Equal(fixtureSpec.ExpectedRowCount, dto.Metadata.SampleCount);
        Assert.Equal(fixtureSpec.ExpectedRowCount, dto.CenterlinePoints.Length);
        Assert.Equal(fixtureSpec.ExpectedRowCount, dto.Frames.Length);
        Assert.Empty(dto.Lines);
        Assert.Empty(dto.Boxes);
        Assert.Null(dto.TrainPose);

        Assert.Equal(fixture.Frames[0].Distance, dto.CenterlinePoints[0].Distance);
        Assert.Equal(fixture.Frames[0].Position.X, dto.CenterlinePoints[0].Position.X);
        Assert.Equal(fixture.Frames[fixture.Frames.Count - 1].Position.Z, dto.Frames[dto.Frames.Length - 1].Position.Z);
        AssertNoValidationDiagnostics(DebugViewportSnapshotV1Validator.Validate(dto));

        string firstJson = DebugViewportSnapshotV1Json.Serialize(dto, indented: true);
        DebugViewportSnapshotV1Dto roundtrip = DebugViewportSnapshotV1Json.Deserialize(firstJson);
        string secondJson = DebugViewportSnapshotV1Json.Serialize(roundtrip, indented: true);

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(dto.Metadata.SourceFixtureName, roundtrip.Metadata.SourceFixtureName);
        Assert.Equal(dto.Metadata.SampleCount, roundtrip.Metadata.SampleCount);
        Assert.Equal(dto.CenterlinePoints.Length, roundtrip.CenterlinePoints.Length);
        Assert.Equal(dto.Frames.Length, roundtrip.Frames.Length);
        AssertNoValidationDiagnostics(DebugViewportSnapshotV1Validator.Validate(roundtrip));
    }

    [Theory]
    [MemberData(
        nameof(DebugViewportSnapshotV1FixtureTestHelper.Milestone7FixtureData),
        MemberType = typeof(DebugViewportSnapshotV1FixtureTestHelper))]
    public void Milestone7Fixture_GeneratedJson_ValidatesThroughCommand(CenterlineFrameCsvFixtureSpec fixtureSpec)
    {
        string tempDirectory = DebugViewportSnapshotV1FixtureTestHelper.CreateTempDirectoryPath(
            nameof(Milestone7Fixture_GeneratedJson_ValidatesThroughCommand));
        string snapshotPath = Path.Combine(
            tempDirectory,
            Path.GetFileNameWithoutExtension(fixtureSpec.FileName) + ".snapshot.json");
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            int generateExitCode = DebugViewportSnapshotV1FromCsvCommand.Run(
                DebugViewportSnapshotV1FixtureTestHelper.GetFixturePath(fixtureSpec),
                snapshotPath);

            Assert.Equal(0, generateExitCode);
            Assert.True(File.Exists(snapshotPath));

            int validateExitCode = DebugViewportSnapshotV1ValidateCommand.Run(snapshotPath, writer);

            Assert.Equal(0, validateExitCode);
        }
        finally
        {
            DebugViewportSnapshotV1FixtureTestHelper.DeleteDirectoryIfPresent(tempDirectory);
        }

        string output = writer.ToString();

        Assert.Contains("sourceFixtureName: " + fixtureSpec.FileName, output);
        Assert.Contains("centerlineCount: " + fixtureSpec.ExpectedRowCount.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("frameCount: " + fixtureSpec.ExpectedRowCount.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("validation: PASS", output);
    }

    private static void AssertDistanceOrderPreserved(IReadOnlyList<ExportTrackFrame> frames)
    {
        double previousDistance = 0.0;
        bool hasPreviousDistance = false;

        foreach (ExportTrackFrame frame in frames)
        {
            Assert.True(frame.Distance >= 0.0);
            if (hasPreviousDistance)
            {
                Assert.True(frame.Distance >= previousDistance);
            }

            previousDistance = frame.Distance;
            hasPreviousDistance = true;
        }
    }

    private static void AssertNoValidationDiagnostics(
        IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
    {
        Assert.True(
            diagnostics.Count == 0,
            "Expected no DebugViewportSnapshotV1 validation diagnostics, got: " +
            string.Join("; ", diagnostics.Select(d => d.Code + " at " + d.Path + ": " + d.Message)));
    }
}
