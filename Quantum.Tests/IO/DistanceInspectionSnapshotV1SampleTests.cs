using System.Text.Json;
using Quantum.Debug;
using Quantum.IO.DistanceInspection.V1;

namespace Quantum.Tests;

public sealed class DistanceInspectionSnapshotV1SampleTests
{
    private const string SampleFileName = "distance-inspection-snapshot-v1.sample.json";

    [Fact]
    public void SampleFile_LoadsAsValidJson()
    {
        using JsonDocument sample = LoadSampleDocument();

        Assert.Equal(JsonValueKind.Object, sample.RootElement.ValueKind);
    }

    [Fact]
    public void SampleFile_DeserializesWithExpectedContractIdentity()
    {
        DistanceInspectionSnapshotV1Dto dto =
            DistanceInspectionSnapshotV1Json.Deserialize(LoadSampleJson());

        Assert.Equal("quantum.distance_inspection_snapshot", dto.Contract);
        Assert.Equal(1, dto.Version);
    }

    [Fact]
    public void SampleFile_IncludesSectionsAndChannelValues()
    {
        DistanceInspectionSnapshotV1Dto dto =
            DistanceInspectionSnapshotV1Json.Deserialize(LoadSampleJson());

        Assert.NotEmpty(dto.Sections);
        Assert.Contains(dto.Sections, section => section.ChannelValues.Length > 0);
    }

    [Fact]
    public void SampleFile_MatchesDeterministicDebugCommandSampleSerialization()
    {
        string sample = NormalizeLineEndings(LoadSampleJson()).TrimEnd();
        string expected = NormalizeLineEndings(
            DistanceInspectionSnapshotV1Json.Serialize(
                DistanceInspectionJsonCommand.BuildSample(),
                indented: true)).TrimEnd();

        Assert.Equal(expected, sample);
    }

    private static JsonDocument LoadSampleDocument()
    {
        return JsonDocument.Parse(LoadSampleJson());
    }

    private static string LoadSampleJson()
    {
        return File.ReadAllText(FindSamplePath());
    }

    private static string FindSamplePath()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "docs", "contracts", SampleFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Sample file '{SampleFileName}' was not found from '{AppContext.BaseDirectory}'.");
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
    }
}
