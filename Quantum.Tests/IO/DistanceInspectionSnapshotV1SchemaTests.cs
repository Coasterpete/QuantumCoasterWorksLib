using System;
using System.IO;
using System.Text.Json;

namespace Quantum.Tests;

public sealed class DistanceInspectionSnapshotV1SchemaTests
{
    private const string SchemaFileName = "distance-inspection-snapshot-v1.schema.json";

    [Fact]
    public void SchemaFile_LoadsAsValidJson()
    {
        using JsonDocument schema = LoadSchemaDocument();

        Assert.Equal(JsonValueKind.Object, schema.RootElement.ValueKind);
    }

    [Fact]
    public void SchemaFile_ContainsExpectedContractConst()
    {
        using JsonDocument schema = LoadSchemaDocument();

        JsonElement properties = GetRequiredProperty(schema.RootElement, "properties");
        JsonElement contract = GetRequiredProperty(properties, "contract");

        Assert.Equal("quantum.distance_inspection_snapshot", GetRequiredProperty(contract, "const").GetString());
    }

    [Fact]
    public void SchemaFile_ContainsVersionConstOne()
    {
        using JsonDocument schema = LoadSchemaDocument();

        JsonElement properties = GetRequiredProperty(schema.RootElement, "properties");
        JsonElement version = GetRequiredProperty(properties, "version");

        Assert.Equal(1, GetRequiredProperty(version, "const").GetInt32());
    }

    [Fact]
    public void SchemaFile_DocumentsSectionsChannelsAndChannelValues()
    {
        using JsonDocument schema = LoadSchemaDocument();

        JsonElement properties = GetRequiredProperty(schema.RootElement, "properties");
        JsonElement sections = GetRequiredProperty(properties, "sections");
        Assert.Equal("array", GetRequiredProperty(sections, "type").GetString());

        JsonElement sectionProperties = GetDefinitionProperties(schema.RootElement, "distanceInspectionSection");
        JsonElement channels = GetRequiredProperty(sectionProperties, "channels");
        JsonElement channelValues = GetRequiredProperty(sectionProperties, "channelValues");

        Assert.Equal("array", GetRequiredProperty(channels, "type").GetString());
        Assert.Equal("string", GetRequiredProperty(GetRequiredProperty(channels, "items"), "type").GetString());
        Assert.Equal("array", GetRequiredProperty(channelValues, "type").GetString());

        JsonElement channelValueProperties = GetDefinitionProperties(schema.RootElement, "distanceInspectionChannelValue");
        Assert.Equal("string", GetRequiredProperty(GetRequiredProperty(channelValueProperties, "channel"), "type").GetString());
        Assert.Equal("number", GetRequiredProperty(GetRequiredProperty(channelValueProperties, "value"), "type").GetString());
    }

    private static JsonDocument LoadSchemaDocument()
    {
        return JsonDocument.Parse(File.ReadAllText(FindSchemaPath()));
    }

    private static string FindSchemaPath()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "docs", "contracts", SchemaFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Schema file '{SchemaFileName}' was not found from '{AppContext.BaseDirectory}'.");
    }

    private static JsonElement GetDefinitionProperties(JsonElement schemaRoot, string definitionName)
    {
        JsonElement definitions = GetRequiredProperty(schemaRoot, "$defs");
        JsonElement definition = GetRequiredProperty(definitions, definitionName);
        return GetRequiredProperty(definition, "properties");
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName)
    {
        Assert.True(
            element.TryGetProperty(propertyName, out JsonElement property),
            $"Expected JSON property '{propertyName}'.");

        return property;
    }
}
