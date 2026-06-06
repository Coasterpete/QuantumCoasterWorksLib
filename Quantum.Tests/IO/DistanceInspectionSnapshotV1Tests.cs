using System.Text.Json;
using Quantum.IO.DistanceInspection.V1;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class DistanceInspectionSnapshotV1Tests
{
    [Fact]
    public void Export_ExposesExpectedContractIdentity()
    {
        DistanceInspectionSnapshotV1Dto dto = DistanceInspectionSnapshotV1Mapper.Export(CreateSnapshot());

        Assert.Equal("quantum.distance_inspection_snapshot", DistanceInspectionSnapshotV1Dto.ContractName);
        Assert.Equal(1, DistanceInspectionSnapshotV1Dto.ContractVersion);
        Assert.Equal(DistanceInspectionSnapshotV1Dto.ContractName, dto.Contract);
        Assert.Equal(DistanceInspectionSnapshotV1Dto.ContractVersion, dto.Version);
    }

    [Fact]
    public void Export_PreservesSnapshotDistance()
    {
        DistanceInspectionSnapshotV1Dto dto = DistanceInspectionSnapshotV1Mapper.Export(CreateSnapshot());

        Assert.Equal(4.25, dto.Distance);
    }

    [Fact]
    public void Export_PreservesSectionMetadataAndOrder()
    {
        DistanceInspectionSnapshotV1Dto dto = DistanceInspectionSnapshotV1Mapper.Export(CreateSnapshot());

        Assert.Equal(2, dto.Sections.Length);

        Assert.Equal("Geometry", dto.Sections[0].Kind);
        Assert.Equal("Distance", dto.Sections[0].Domain);
        Assert.Equal(2.0, dto.Sections[0].StartX);
        Assert.Equal(8.0, dto.Sections[0].EndX);
        Assert.Equal("None", dto.Sections[0].Diagnostic);

        Assert.Equal("Force", dto.Sections[1].Kind);
        Assert.Equal("Distance", dto.Sections[1].Domain);
        Assert.Equal(0.0, dto.Sections[1].StartX);
        Assert.Equal(10.0, dto.Sections[1].EndX);
        Assert.Equal("MissingChannel", dto.Sections[1].Diagnostic);
    }

    [Fact]
    public void Export_PreservesChannelsAndChannelValues()
    {
        DistanceInspectionSnapshotV1Dto dto = DistanceInspectionSnapshotV1Mapper.Export(CreateSnapshot());

        Assert.Equal(new[] { "Roll", "Curvature" }, dto.Sections[0].Channels);
        Assert.Equal(new[] { "LongitudinalG", "NormalG", "LateralG" }, dto.Sections[1].Channels);

        Assert.Equal("Roll", dto.Sections[0].ChannelValues[0].Channel);
        Assert.Equal(0.2, dto.Sections[0].ChannelValues[0].Value);
        Assert.Equal("Curvature", dto.Sections[0].ChannelValues[1].Channel);
        Assert.Equal(0.05, dto.Sections[0].ChannelValues[1].Value);

        Assert.Equal("LongitudinalG", dto.Sections[1].ChannelValues[0].Channel);
        Assert.Equal(0.4, dto.Sections[1].ChannelValues[0].Value);
        Assert.Equal("NormalG", dto.Sections[1].ChannelValues[1].Channel);
        Assert.Equal(1.5, dto.Sections[1].ChannelValues[1].Value);
        Assert.Equal("LateralG", dto.Sections[1].ChannelValues[2].Channel);
        Assert.Equal(-0.1, dto.Sections[1].ChannelValues[2].Value);
    }

    [Fact]
    public void Serialize_IsDeterministicAndUsesCamelCaseFieldNames()
    {
        DistanceInspectionSnapshotV1Dto dto = DistanceInspectionSnapshotV1Mapper.Export(CreateSnapshot());

        string first = NormalizeLineEndings(DistanceInspectionSnapshotV1Json.Serialize(dto, indented: true)).TrimEnd();
        string second = NormalizeLineEndings(DistanceInspectionSnapshotV1Json.Serialize(dto, indented: true)).TrimEnd();
        string roundtrip = NormalizeLineEndings(
            DistanceInspectionSnapshotV1Json.Serialize(
                DistanceInspectionSnapshotV1Json.Deserialize(first),
                indented: true)).TrimEnd();

        Assert.Equal(first, second);
        Assert.Equal(first, roundtrip);
        Assert.Contains("\"contract\":", first);
        Assert.Contains("\"startX\":", first);
        Assert.Contains("\"channelValues\":", first);
        Assert.DoesNotContain("\"Contract\":", first);
        Assert.DoesNotContain("\"StartX\":", first);
        Assert.DoesNotContain("\"ChannelValues\":", first);
        Assert.Equal(ExpectedJson(), first);
    }

    [Fact]
    public void Deserialize_RejectsWrongContract()
    {
        const string json = @"{""contract"":""wrong.contract"",""version"":1}";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            DistanceInspectionSnapshotV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string json = @"{""contract"":""quantum.distance_inspection_snapshot"",""version"":1,";

        JsonException ex = Assert.Throws<JsonException>(() =>
            DistanceInspectionSnapshotV1Json.Deserialize(json));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DistanceInspectionSnapshot CreateSnapshot()
    {
        return new DistanceInspectionSnapshot(
            distance: 4.25,
            new[]
            {
                new DistanceSectionInspection(
                    SectionKind.Geometry,
                    SectionDomain.Distance,
                    startX: 2.0,
                    endX: 8.0,
                    new[] { SectionChannel.Roll, SectionChannel.Curvature },
                    new[]
                    {
                        new DistanceSectionChannelInspection(SectionChannel.Roll, 0.2),
                        new DistanceSectionChannelInspection(SectionChannel.Curvature, 0.05)
                    },
                    SectionEvaluationDiagnostic.None),
                new DistanceSectionInspection(
                    SectionKind.Force,
                    SectionDomain.Distance,
                    startX: 0.0,
                    endX: 10.0,
                    new[]
                    {
                        SectionChannel.LongitudinalG,
                        SectionChannel.NormalG,
                        SectionChannel.LateralG
                    },
                    new[]
                    {
                        new DistanceSectionChannelInspection(SectionChannel.LongitudinalG, 0.4),
                        new DistanceSectionChannelInspection(SectionChannel.NormalG, 1.5),
                        new DistanceSectionChannelInspection(SectionChannel.LateralG, -0.1)
                    },
                    SectionEvaluationDiagnostic.MissingChannel)
            });
    }

    private static string ExpectedJson()
    {
        return NormalizeLineEndings(
            """
            {
              "contract": "quantum.distance_inspection_snapshot",
              "version": 1,
              "distance": 4.25,
              "sections": [
                {
                  "kind": "Geometry",
                  "domain": "Distance",
                  "startX": 2,
                  "endX": 8,
                  "diagnostic": "None",
                  "channels": [
                    "Roll",
                    "Curvature"
                  ],
                  "channelValues": [
                    {
                      "channel": "Roll",
                      "value": 0.2
                    },
                    {
                      "channel": "Curvature",
                      "value": 0.05
                    }
                  ]
                },
                {
                  "kind": "Force",
                  "domain": "Distance",
                  "startX": 0,
                  "endX": 10,
                  "diagnostic": "MissingChannel",
                  "channels": [
                    "LongitudinalG",
                    "NormalG",
                    "LateralG"
                  ],
                  "channelValues": [
                    {
                      "channel": "LongitudinalG",
                      "value": 0.4
                    },
                    {
                      "channel": "NormalG",
                      "value": 1.5
                    },
                    {
                      "channel": "LateralG",
                      "value": -0.1
                    }
                  ]
                }
              ]
            }
            """).TrimEnd();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
    }
}
