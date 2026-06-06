using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Quantum.Tests;

public sealed class DistanceInspectionSnapshotV1ManifestTests
{
    private const string ManifestFileName = "distance-inspection-snapshot-v1.manifest.json";
    private const string ContractName = "quantum.distance_inspection_snapshot";
    private const string SchemaPath = "docs/contracts/distance-inspection-snapshot-v1.schema.json";
    private const string SamplePath = "docs/contracts/distance-inspection-snapshot-v1.sample.json";
    private const string HandoffPath = "docs/contracts/distance-inspection-json-handoff.md";
    private const string DebugCommand = "dotnet run --project Quantum.Debug -- distance-inspection-json";

    [Fact]
    public void ManifestFile_LoadsAsValidJson()
    {
        using JsonDocument manifest = LoadManifestDocument();

        Assert.Equal(JsonValueKind.Object, manifest.RootElement.ValueKind);
    }

    [Fact]
    public void ManifestFile_ContainsExpectedContractIdentity()
    {
        using JsonDocument manifest = LoadManifestDocument();

        Assert.Equal(ContractName, GetRequiredProperty(manifest.RootElement, "contract").GetString());
        Assert.Equal(1, GetRequiredProperty(manifest.RootElement, "version").GetInt32());
    }

    [Fact]
    public void ManifestFile_ReferencesContractArtifactsAndCommand()
    {
        using JsonDocument manifest = LoadManifestDocument();

        Assert.Equal(SchemaPath, GetRequiredProperty(manifest.RootElement, "schema").GetString());
        Assert.Equal(SamplePath, GetRequiredProperty(manifest.RootElement, "sample").GetString());
        Assert.Equal(HandoffPath, GetRequiredProperty(manifest.RootElement, "handoff").GetString());
        Assert.Equal(DebugCommand, GetRequiredProperty(manifest.RootElement, "debugCommand").GetString());
    }

    [Fact]
    public void ManifestFile_ReferencedDocsContractFilesExist()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument manifest = LoadManifestDocument();

        string[] referencedDocsContractsFiles =
        {
            GetRequiredString(manifest.RootElement, "schema"),
            GetRequiredString(manifest.RootElement, "sample"),
            GetRequiredString(manifest.RootElement, "handoff")
        };

        foreach (string reference in referencedDocsContractsFiles)
        {
            Assert.StartsWith("docs/contracts/", reference, StringComparison.Ordinal);
            Assert.True(
                File.Exists(Path.Combine(new[] { repositoryRoot }.Concat(reference.Split('/')).ToArray())),
                $"Expected referenced docs/contracts file '{reference}' to exist.");
        }
    }

    private static JsonDocument LoadManifestDocument()
    {
        return JsonDocument.Parse(File.ReadAllText(FindManifestPath()));
    }

    private static string FindManifestPath()
    {
        return Path.Combine(FindRepositoryRoot(), "docs", "contracts", ManifestFileName);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "docs", "contracts", ManifestFileName);
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Manifest file '{ManifestFileName}' was not found from '{AppContext.BaseDirectory}'.");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        return GetRequiredProperty(element, propertyName).GetString()
            ?? throw new InvalidOperationException($"Expected JSON property '{propertyName}' to be a string.");
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName)
    {
        Assert.True(
            element.TryGetProperty(propertyName, out JsonElement property),
            $"Expected JSON property '{propertyName}'.");

        return property;
    }
}
