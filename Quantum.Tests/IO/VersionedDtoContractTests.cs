using System;
using Quantum.IO.BankingProfile.V1;
using Quantum.IO.ContinuousRollDiagnostics.V1;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.DistanceInspection.V1;
using Quantum.IO.MeshExport.V1;
using Quantum.IO.TrackFrameContinuity.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.IO.TransportedFrameComparison.V1;

namespace Quantum.Tests;

public sealed class VersionedDtoContractTests
{
    [Fact]
    public void ContractConstants_MatchStableVersionedDtoIdentities()
    {
        AssertContractIdentity(
            "quantum.train_pose",
            1,
            TrainPoseExportV1Dto.ContractName,
            TrainPoseExportV1Dto.ContractVersion,
            new TrainPoseExportV1Dto().Contract,
            new TrainPoseExportV1Dto().Version);
        AssertContractIdentity(
            "quantum.continuous_roll_diagnostics",
            1,
            ContinuousRollDiagnosticsExportV1Dto.ContractName,
            ContinuousRollDiagnosticsExportV1Dto.ContractVersion,
            new ContinuousRollDiagnosticsExportV1Dto().Contract,
            new ContinuousRollDiagnosticsExportV1Dto().Version);
        AssertContractIdentity(
            "quantum.banking_profile_diagnostics",
            1,
            BankingProfileDiagnosticsExportV1Dto.ContractName,
            BankingProfileDiagnosticsExportV1Dto.ContractVersion,
            new BankingProfileDiagnosticsExportV1Dto().Contract,
            new BankingProfileDiagnosticsExportV1Dto().Version);
        AssertContractIdentity(
            "quantum.debug_viewport_snapshot",
            1,
            DebugViewportSnapshotV1Dto.ContractName,
            DebugViewportSnapshotV1Dto.ContractVersion,
            new DebugViewportSnapshotV1Dto().Contract,
            new DebugViewportSnapshotV1Dto().Version);
        AssertContractIdentity(
            "quantum.distance_inspection_snapshot",
            1,
            DistanceInspectionSnapshotV1Dto.ContractName,
            DistanceInspectionSnapshotV1Dto.ContractVersion,
            new DistanceInspectionSnapshotV1Dto().Contract,
            new DistanceInspectionSnapshotV1Dto().Version);
        AssertContractIdentity(
            "quantum.track_frame_continuity_diagnostics",
            1,
            TrackFrameContinuityDiagnosticsExportV1Dto.ContractName,
            TrackFrameContinuityDiagnosticsExportV1Dto.ContractVersion,
            new TrackFrameContinuityDiagnosticsExportV1Dto().Contract,
            new TrackFrameContinuityDiagnosticsExportV1Dto().Version);
        AssertContractIdentity(
            "quantum.transported_frame_comparison_diagnostics",
            1,
            TransportedFrameComparisonDiagnosticsExportV1Dto.ContractName,
            TransportedFrameComparisonDiagnosticsExportV1Dto.ContractVersion,
            new TransportedFrameComparisonDiagnosticsExportV1Dto().Contract,
            new TransportedFrameComparisonDiagnosticsExportV1Dto().Version);
        AssertContractIdentity(
            "quantum.mesh_export",
            1,
            MeshExportV1Dto.ContractName,
            MeshExportV1Dto.ContractVersion,
            new MeshExportV1Dto().Contract,
            new MeshExportV1Dto().Version);
    }

    [Fact]
    public void DtoDefaults_MatchCurrentValidityExpectations()
    {
        var trainPose = new TrainPoseExportV1Dto();
        Assert.True(TrainPoseExportV1Validator.TryValidate(trainPose, out var trainPoseDiagnostics));
        Assert.Empty(trainPoseDiagnostics);
        Assert.Empty(trainPose.Cars);
        Assert.NotNull(trainPose.Definition);
        Assert.Null(trainPose.Definition.WheelLayout);

        var continuousRoll = new ContinuousRollDiagnosticsExportV1Dto();
        Assert.Empty(continuousRoll.Samples);
        Assert.False(continuousRoll.WrapHandlingEnabled);

        var bankingProfile = new BankingProfileDiagnosticsExportV1Dto();
        Assert.True(bankingProfile.BackendOnly);
        Assert.Equal("meters,radians", bankingProfile.Metadata.Units);
        Assert.Equal("meters", bankingProfile.Metadata.DistanceUnit);
        Assert.Equal("radians,degrees", bankingProfile.Metadata.RollAngleUnits);
        Assert.Equal("radians_per_meter", bankingProfile.Metadata.RollSlopeUnit);
        Assert.Empty(bankingProfile.Samples);

        var debugViewport = new DebugViewportSnapshotV1Dto();
        Assert.True(DebugViewportSnapshotV1Validator.TryValidate(debugViewport, out var debugViewportDiagnostics));
        Assert.Empty(debugViewportDiagnostics);
        Assert.Empty(debugViewport.CenterlinePoints);
        Assert.Empty(debugViewport.Frames);
        Assert.Empty(debugViewport.Lines);
        Assert.Empty(debugViewport.Boxes);
        Assert.Null(debugViewport.TrainPose);

        var distanceInspection = new DistanceInspectionSnapshotV1Dto();
        Assert.Empty(distanceInspection.Sections);

        var trackFrameContinuity = new TrackFrameContinuityDiagnosticsExportV1Dto();
        Assert.True(trackFrameContinuity.BackendOnly);
        Assert.Equal("meters", trackFrameContinuity.Metadata.Units);
        Assert.Empty(trackFrameContinuity.Samples);
        Assert.Empty(trackFrameContinuity.Intervals);
        Assert.Empty(trackFrameContinuity.Issues);
        Assert.Equal(string.Empty, trackFrameContinuity.DiagnosticText);

        var transportedFrameComparison = new TransportedFrameComparisonDiagnosticsExportV1Dto();
        Assert.True(transportedFrameComparison.BackendOnly);
        Assert.Equal("meters", transportedFrameComparison.Metadata.Units);
        Assert.Empty(transportedFrameComparison.Metadata.FixtureNames);
        Assert.Empty(transportedFrameComparison.Reports);

        var mesh = new MeshExportV1Dto();
        Assert.False(MeshExportV1Validator.TryValidate(mesh, out var meshDiagnostics));
        Assert.Contains(
            meshDiagnostics,
            d => d.Code == MeshExportV1ValidationCode.EmptyMeshCollection &&
                 d.Path == "meshes");
        Assert.DoesNotContain(
            meshDiagnostics,
            d => d.Code == MeshExportV1ValidationCode.InvalidContract ||
                 d.Code == MeshExportV1ValidationCode.InvalidVersion);
    }

    [Fact]
    public void Serialize_DefaultDtos_MatchesStableIndentedContracts()
    {
        AssertStableJson(
            new TrainPoseExportV1Dto(),
            TrainPoseExportV1Json.Serialize,
            TrainPoseExportV1Json.Deserialize,
            """
            {
              "contract": "quantum.train_pose",
              "version": 1,
              "leadDistance": 0,
              "definition": {
                "carCount": 0,
                "carSpacing": 0,
                "carGeometry": {
                  "length": 0,
                  "width": 0,
                  "height": 0
                },
                "bogieLayout": {
                  "bogieSpacing": 0
                },
                "wheelLayout": null
              },
              "cars": []
            }
            """);
        AssertStableJson(
            new ContinuousRollDiagnosticsExportV1Dto(),
            ContinuousRollDiagnosticsExportV1Json.Serialize,
            ContinuousRollDiagnosticsExportV1Json.Deserialize,
            """
            {
              "contract": "quantum.continuous_roll_diagnostics",
              "version": 1,
              "sampleCount": 0,
              "maxRollRateRadiansPerMeter": 0,
              "averageRollRateRadiansPerMeter": 0,
              "wrapHandlingEnabled": false,
              "warningCount": 0,
              "samples": []
            }
            """);
        AssertStableJson(
            new BankingProfileDiagnosticsExportV1Dto(),
            BankingProfileDiagnosticsExportV1Json.Serialize,
            BankingProfileDiagnosticsExportV1Json.Deserialize,
            """
            {
              "contract": "quantum.banking_profile_diagnostics",
              "version": 1,
              "backendOnly": true,
              "metadata": {
                "units": "meters,radians",
                "sourceName": null,
                "profileKeyCount": 0,
                "distanceUnit": "meters",
                "rollAngleUnits": "radians,degrees",
                "rollSlopeUnit": "radians_per_meter"
              },
              "summaryMetrics": {
                "sampleCount": 0,
                "minRollRadians": 0,
                "maxRollRadians": 0,
                "minRollDegrees": 0,
                "maxRollDegrees": 0,
                "maxAbsoluteRollSlopeRadPerMeter": 0
              },
              "samples": []
            }
            """);
        AssertStableJson(
            new DebugViewportSnapshotV1Dto(),
            DebugViewportSnapshotV1Json.Serialize,
            DebugViewportSnapshotV1Json.Deserialize,
            """
            {
              "contract": "quantum.debug_viewport_snapshot",
              "version": 1,
              "metadata": {
                "units": "meters",
                "sourceFixtureName": null,
                "sampleCount": 0
              },
              "centerlinePoints": [],
              "frames": [],
              "lines": [],
              "boxes": [],
              "trainPose": null
            }
            """);
        AssertStableJson(
            new DistanceInspectionSnapshotV1Dto(),
            DistanceInspectionSnapshotV1Json.Serialize,
            DistanceInspectionSnapshotV1Json.Deserialize,
            """
            {
              "contract": "quantum.distance_inspection_snapshot",
              "version": 1,
              "distance": 0,
              "sections": []
            }
            """);
        AssertStableJson(
            new TrackFrameContinuityDiagnosticsExportV1Dto(),
            TrackFrameContinuityDiagnosticsExportV1Json.Serialize,
            TrackFrameContinuityDiagnosticsExportV1Json.Deserialize,
            """
            {
              "contract": "quantum.track_frame_continuity_diagnostics",
              "version": 1,
              "backendOnly": true,
              "metadata": {
                "units": "meters",
                "sourceName": null,
                "trackLength": 0
              },
              "thresholdsDegrees": {
                "tangent": 0,
                "normal": 0,
                "binormal": 0,
                "roll": 0,
                "matrixOrientation": 0
              },
              "summaryStatistics": {
                "sampleCount": 0,
                "intervalCount": 0,
                "issueCount": 0,
                "hasIssues": false,
                "tangentDegrees": {
                  "maxAbsolute": 0,
                  "averageAbsolute": 0
                },
                "normalDegrees": {
                  "maxAbsolute": 0,
                  "averageAbsolute": 0
                },
                "binormalDegrees": {
                  "maxAbsolute": 0,
                  "averageAbsolute": 0
                },
                "rollDegrees": {
                  "maxAbsolute": 0,
                  "averageAbsolute": 0
                },
                "matrixOrientationDegrees": {
                  "maxAbsolute": 0,
                  "averageAbsolute": 0
                }
              },
              "samples": [],
              "intervals": [],
              "issues": [],
              "diagnosticText": ""
            }
            """);
        AssertStableJson(
            new TransportedFrameComparisonDiagnosticsExportV1Dto(),
            TransportedFrameComparisonDiagnosticsExportV1Json.Serialize,
            TransportedFrameComparisonDiagnosticsExportV1Json.Deserialize,
            """
            {
              "contract": "quantum.transported_frame_comparison_diagnostics",
              "version": 1,
              "backendOnly": true,
              "metadata": {
                "units": "meters",
                "sourceName": null,
                "reportCount": 0,
                "fixtureNames": []
              },
              "reports": []
            }
            """);
        AssertStableJson(
            new MeshExportV1Dto(),
            MeshExportV1Json.Serialize,
            MeshExportV1Json.Deserialize,
            """
            {
              "contract": "quantum.mesh_export",
              "version": 1,
              "meshes": []
            }
            """);
    }

    private static void AssertContractIdentity(
        string expectedName,
        int expectedVersion,
        string actualName,
        int actualVersion,
        string defaultName,
        int defaultVersion)
    {
        Assert.Equal(expectedName, actualName);
        Assert.Equal(expectedVersion, actualVersion);
        Assert.Equal(actualName, defaultName);
        Assert.Equal(actualVersion, defaultVersion);
    }

    private static void AssertStableJson<TDto>(
        TDto dto,
        Func<TDto, bool, string> serialize,
        Func<string, TDto> deserialize,
        string expectedJson)
        where TDto : class
    {
        string expected = NormalizeLineEndings(expectedJson).TrimEnd();
        string first = NormalizeLineEndings(serialize(dto, true)).TrimEnd();
        string second = NormalizeLineEndings(serialize(dto, true)).TrimEnd();
        string roundtrip = NormalizeLineEndings(serialize(deserialize(first), true)).TrimEnd();

        Assert.Equal(expected, first);
        Assert.Equal(first, second);
        Assert.Equal(first, roundtrip);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
    }
}
