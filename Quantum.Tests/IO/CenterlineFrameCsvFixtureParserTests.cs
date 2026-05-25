using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.Fixtures.Csv;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class CenterlineFrameCsvFixtureParserTests
{
    private const string FixtureFileName = "Milestone5.synthetic.centerline_frames.csv";

    [Fact]
    public void ParseFile_IsDeterministicForSyntheticFixture()
    {
        string path = GetFixturePath();

        CenterlineFrameCsvFixture first = CenterlineFrameCsvFixtureParser.ParseFile(path, FixtureFileName);
        CenterlineFrameCsvFixture second = CenterlineFrameCsvFixtureParser.ParseFile(path, FixtureFileName);

        Assert.Equal(first.SourceFixtureName, second.SourceFixtureName);
        Assert.Equal(first.Units, second.Units);
        Assert.Equal(first.Frames.Count, second.Frames.Count);

        for (int i = 0; i < first.Frames.Count; i++)
        {
            AssertFramesEqual(first.Frames[i], second.Frames[i]);
        }
    }

    [Fact]
    public void ParseFile_PreservesRowOrderAndDistanceWhenMappedToDebugViewportSnapshot()
    {
        CenterlineFrameCsvFixture fixture = CenterlineFrameCsvFixtureParser.ParseFile(GetFixturePath(), FixtureFileName);

        DebugViewportSnapshotV1Dto dto = DebugViewportSnapshotV1Mapper.Export(new DebugViewportSnapshotV1Source
        {
            Units = fixture.Units,
            SourceFixtureName = fixture.SourceFixtureName,
            SampledFrames = fixture.Frames
        });
        string json = DebugViewportSnapshotV1Json.Serialize(dto);
        DebugViewportSnapshotV1Dto roundtrip = DebugViewportSnapshotV1Json.Deserialize(json);

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, roundtrip.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, roundtrip.Version);
        Assert.Equal("meters", roundtrip.Metadata.Units);
        Assert.Equal(FixtureFileName, roundtrip.Metadata.SourceFixtureName);
        Assert.Equal(3, roundtrip.Metadata.SampleCount);

        Assert.Equal(0.0, roundtrip.Frames[0].Distance);
        Assert.Equal(3.25, roundtrip.Frames[1].Distance);
        Assert.Equal(8.75, roundtrip.Frames[2].Distance);
        Assert.Equal(0.0, roundtrip.Frames[0].Position.X);
        Assert.Equal(3.25, roundtrip.Frames[1].Position.X);
        Assert.Equal(8.75, roundtrip.Frames[2].Position.X);

        Assert.Equal(3, roundtrip.CenterlinePoints.Length);
        Assert.Equal(roundtrip.Frames[2].Distance, roundtrip.CenterlinePoints[2].Distance);
        Assert.Equal(roundtrip.Frames[2].Position.Z, roundtrip.CenterlinePoints[2].Position.Z);
    }

    [Fact]
    public void Parse_UsesInvariantCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            CenterlineFrameCsvFixture fixture = CenterlineFrameCsvFixtureParser.Parse(CreateCsv(
                "1.5,1.5,0,0,1,0,0,0,1,0,0,0,1"));

            ExportTrackFrame frame = Assert.Single(fixture.Frames);
            Assert.Equal(1.5, frame.Distance);
            Assert.Equal(1.5, frame.Position.X);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Parse_RejectsInvalidHeader()
    {
        string csv = CenterlineFrameCsvFixtureParser.RequiredHeader.Replace("distanceMeters", "stationMeters", StringComparison.Ordinal) +
            Environment.NewLine +
            "0,0,0,0,1,0,0,0,1,0,0,0,1";

        FormatException ex = Assert.Throws<FormatException>(() => CenterlineFrameCsvFixtureParser.Parse(csv));

        Assert.Contains("header", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsNonFiniteValues()
    {
        string csv = CreateCsv("0,0,0,0,NaN,0,0,0,1,0,0,0,1");

        FormatException ex = Assert.Throws<FormatException>(() => CenterlineFrameCsvFixtureParser.Parse(csv));

        Assert.Contains("finite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsDecreasingDistance()
    {
        string csv = CreateCsv(
            "2,2,0,0,1,0,0,0,1,0,0,0,1",
            "1,1,0,0,1,0,0,0,1,0,0,0,1");

        FormatException ex = Assert.Throws<FormatException>(() => CenterlineFrameCsvFixtureParser.Parse(csv));

        Assert.Contains("monotonically", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuantumIoAndDebug_DoNotReferenceFrontendOrRendererAssemblies()
    {
        Assembly[] assemblies =
        {
            typeof(CenterlineFrameCsvFixtureParser).Assembly,
            typeof(DebugViewportSnapshotV1SampleCommand).Assembly
        };

        string[] forbiddenPrefixes =
        {
            "UnityEngine",
            "UnityEditor",
            "Unreal",
            "Avalonia",
            "Silk.NET",
            "OpenTK",
            "Veldrid"
        };

        foreach (Assembly assembly in assemblies)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                foreach (string forbiddenPrefix in forbiddenPrefixes)
                {
                    Assert.False(
                        reference.Name != null &&
                        reference.Name.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase),
                        $"{assembly.GetName().Name} must not reference frontend or renderer assembly '{reference.Name}'.");
                }
            }
        }
    }

    private static string GetFixturePath()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", FixtureFileName);
        Assert.True(File.Exists(path), $"Synthetic CSV fixture file was not found at '{path}'.");
        return path;
    }

    private static string CreateCsv(params string[] rows)
    {
        return CenterlineFrameCsvFixtureParser.RequiredHeader +
            Environment.NewLine +
            string.Join(Environment.NewLine, rows);
    }

    private static void AssertFramesEqual(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        Assert.Equal(expected.Distance, actual.Distance);
        Assert.Equal(expected.Position.X, actual.Position.X);
        Assert.Equal(expected.Position.Y, actual.Position.Y);
        Assert.Equal(expected.Position.Z, actual.Position.Z);
        Assert.Equal(expected.Tangent.X, actual.Tangent.X);
        Assert.Equal(expected.Tangent.Y, actual.Tangent.Y);
        Assert.Equal(expected.Tangent.Z, actual.Tangent.Z);
        Assert.Equal(expected.Normal.X, actual.Normal.X);
        Assert.Equal(expected.Normal.Y, actual.Normal.Y);
        Assert.Equal(expected.Normal.Z, actual.Normal.Z);
        Assert.Equal(expected.Binormal.X, actual.Binormal.X);
        Assert.Equal(expected.Binormal.Y, actual.Binormal.Y);
        Assert.Equal(expected.Binormal.Z, actual.Binormal.Z);
    }
}
