using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class BankingProfileTrainPoseContractTests
{
    private const double DistanceTolerance = 1e-7;
    private const double AxisTolerance = 1e-6;
    private const double MatrixTolerance = 1e-5;

    [Fact]
    public void RuntimePose_ProfileBackedFixture_UsesProfileFramesAcrossFullHierarchy()
    {
        BankingProfileTrainPoseFixture fixture = BankingProfileTrainPoseFixtures.ProfileBackedTrainPose();

        TrainPoseResult pose = fixture.EvaluateTrainPose();
        TrainPoseResult repeatedPose = fixture.EvaluateTrainPose();

        AssertPoseMatchesDefinitionAndProfileFrames(fixture, pose);
        AssertPoseNear(pose, repeatedPose);

        var defaultProvider = new TrainCarTransformProvider(new TrackEvaluator(fixture.Document));
        TrainPoseResult defaultPose = defaultProvider.EvaluateTrainPose(
            fixture.LeadDistance,
            fixture.Definition);

        ArticulatedTrainCarWithWheelsTransform defaultLeadCar = defaultPose.CarsReadOnly[0];
        ArticulatedTrainCarWithWheelsTransform profileLeadCar = pose.CarsReadOnly[0];
        AssertCenterlineSamplePreserved(
            defaultLeadCar.Body.OriginalBody.Frame,
            profileLeadCar.Body.OriginalBody.Frame);
        AssertVectorNotNear(
            defaultLeadCar.Body.OriginalBody.Frame.Normal,
            profileLeadCar.Body.OriginalBody.Frame.Normal);
    }

    [Fact]
    public void TrainPoseExportV1_ProfileBackedFixture_ValidatesAndRoundtripsFullHierarchy()
    {
        BankingProfileTrainPoseFixture fixture = BankingProfileTrainPoseFixtures.ProfileBackedTrainPose();
        TrainPoseResult pose = fixture.EvaluateTrainPose();
        TrainPoseExportV1Dto export = TrainPoseExportV1Mapper.Export(pose);
        TrainPoseExportV1Dto repeatedExport = fixture.ExportTrainPose();

        Assert.Equal(TrainPoseExportV1Dto.ContractName, export.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, export.Version);
        Assert.Equal(fixture.Definition.CarCount, export.Cars.Length);
        AssertExportMatchesRuntimePose(pose, export);

        bool isValid = TrainPoseExportV1Validator.TryValidate(
            export,
            out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            new TrainPoseExportV1ValidationOptions
            {
                ValidateMatrixBottomRow = true
            });

        Assert.True(isValid, FormatTrainPoseDiagnostics(diagnostics));
        Assert.Empty(diagnostics);

        string json = TrainPoseExportV1Json.Serialize(export, indented: true);
        string repeatedJson = TrainPoseExportV1Json.Serialize(repeatedExport, indented: true);
        TrainPoseExportV1Dto roundtrip = TrainPoseExportV1Json.Deserialize(json);
        string roundtripJson = TrainPoseExportV1Json.Serialize(roundtrip, indented: true);

        Assert.Equal(json, repeatedJson);
        Assert.Equal(json, roundtripJson);
    }

    [Fact]
    public void DebugViewportSnapshot_ProfileBackedFixture_NestsInspectableTrainPose()
    {
        BankingProfileTrainPoseFixture fixture = BankingProfileTrainPoseFixtures.ProfileBackedTrainPose();

        DebugViewportSnapshotV1Dto snapshot = fixture.BuildDebugViewportSnapshot();

        Assert.Equal(DebugViewportSnapshotV1Dto.ContractName, snapshot.Contract);
        Assert.Equal(DebugViewportSnapshotV1Dto.ContractVersion, snapshot.Version);
        Assert.Equal("meters", snapshot.Metadata.Units);
        Assert.Equal(BankingProfileTrainPoseFixtures.ProfileBackedTrainPoseName, snapshot.Metadata.SourceFixtureName);
        Assert.Equal(DebugViewportSnapshotV1BankingProfileSampleCommand.CenterlineSampleCount, snapshot.Metadata.SampleCount);
        Assert.Equal(snapshot.Metadata.SampleCount, snapshot.CenterlinePoints.Length);
        Assert.Equal(snapshot.CenterlinePoints.Length, snapshot.Frames.Length);
        Assert.Equal(3, snapshot.Lines.Length);
        Assert.Equal(DebugViewportSnapshotV1BankingProfileSampleCommand.TrainCarCount, snapshot.Boxes.Length);

        Assert.NotNull(snapshot.TrainPose);
        TrainPoseExportV1Dto trainPose = snapshot.TrainPose!;
        Assert.Equal(TrainPoseExportV1Dto.ContractName, trainPose.Contract);
        Assert.Equal(fixture.Definition.CarCount, trainPose.Cars.Length);

        for (int i = 0; i < snapshot.Boxes.Length; i++)
        {
            Assert.Equal("train.body.banking-profile", snapshot.Boxes[i].Role);
            Assert.Equal("bp-car-" + i, snapshot.Boxes[i].Label);
            AssertNear(trainPose.Cars[i].Body.ArticulatedFrame.Distance, snapshot.Boxes[i].Frame.Distance, DistanceTolerance);
            AssertNear(trainPose.Cars[i].Body.ArticulatedFrame.Position.X, snapshot.Boxes[i].Frame.Position.X, AxisTolerance);
            AssertNear(fixture.Definition.CarGeometry.Length, snapshot.Boxes[i].Size.Length, DistanceTolerance);
            AssertNear(fixture.Definition.CarGeometry.Width, snapshot.Boxes[i].Size.Width, DistanceTolerance);
            AssertNear(fixture.Definition.CarGeometry.Height, snapshot.Boxes[i].Size.Height, DistanceTolerance);
        }

        bool isValid = DebugViewportSnapshotV1Validator.TryValidate(
            snapshot,
            out IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid, FormatDebugViewportDiagnostics(diagnostics));
        Assert.Empty(diagnostics);

        string json = DebugViewportSnapshotV1Json.Serialize(snapshot, indented: true);
        DebugViewportSnapshotV1Dto roundtrip = DebugViewportSnapshotV1Json.Deserialize(json);
        string roundtripJson = DebugViewportSnapshotV1Json.Serialize(roundtrip, indented: true);

        Assert.Equal(json, roundtripJson);
    }

    [Fact]
    public void TrackDocument_DoesNotCarryBankingProfileState()
    {
        Assert.Null(typeof(TrackDocument).GetProperty("BankingProfile"));
    }

    private static void AssertPoseMatchesDefinitionAndProfileFrames(
        BankingProfileTrainPoseFixture fixture,
        TrainPoseResult pose)
    {
        TrainConsistDefinition definition = fixture.Definition;
        Assert.Equal(fixture.LeadDistance, pose.LeadDistance);
        Assert.Equal(definition.CarCount, pose.CarsReadOnly.Count);
        Assert.Same(definition, pose.Definition);

        double[] frameDistances = BuildProviderFrameDistances(fixture);
        ExportTrackFrame[] expectedProfileFrames = SampleProfileFramesInProviderOrder(
            fixture,
            frameDistances);

        int frameIndex = 0;
        double bogieHalfSpacing = definition.BogieSpacing * 0.5;
        Assert.NotNull(definition.WheelLayout);
        TrainWheelLayout wheelLayout = definition.WheelLayout!;

        for (int carIndex = 0; carIndex < pose.CarsReadOnly.Count; carIndex++)
        {
            ArticulatedTrainCarWithWheelsTransform car = pose.CarsReadOnly[carIndex];
            double expectedBodyDistance = fixture.LeadDistance - (carIndex * definition.CarSpacing);
            double expectedFrontBogieDistance = expectedBodyDistance + bogieHalfSpacing;
            double expectedRearBogieDistance = expectedBodyDistance - bogieHalfSpacing;

            Assert.Equal(carIndex, car.Body.OriginalBody.CarIndex);
            AssertNear(expectedBodyDistance, car.Body.OriginalBody.Distance, DistanceTolerance);
            AssertTrackFrameNear(expectedProfileFrames[frameIndex++], car.Body.OriginalBody.Frame);
            AssertMatrixMatchesFrame(car.Body.OriginalBody.Frame, car.Body.OriginalBody.Matrix);

            AssertNear(expectedFrontBogieDistance, car.Body.FrontBogie.Distance, DistanceTolerance);
            AssertTrackFrameNear(expectedProfileFrames[frameIndex++], car.Body.FrontBogie.Frame);
            AssertBogieNear(car.Body.FrontBogie, car.FrontBogie.Bogie);
            AssertMatrixMatchesFrame(car.FrontBogie.Bogie.Frame, car.FrontBogie.Bogie.Matrix);

            AssertNear(expectedRearBogieDistance, car.Body.RearBogie.Distance, DistanceTolerance);
            AssertTrackFrameNear(expectedProfileFrames[frameIndex++], car.Body.RearBogie.Frame);
            AssertBogieNear(car.Body.RearBogie, car.RearBogie.Bogie);
            AssertMatrixMatchesFrame(car.RearBogie.Bogie.Frame, car.RearBogie.Bogie.Matrix);

            Vector3d expectedArticulationCenter =
                (car.FrontBogie.Bogie.Frame.Position + car.RearBogie.Bogie.Frame.Position) * 0.5;
            AssertNear(expectedBodyDistance, car.Body.CenterDistance, DistanceTolerance);
            AssertNear(expectedBodyDistance, car.Body.ArticulatedFrame.Distance, DistanceTolerance);
            AssertVectorNear(expectedArticulationCenter, car.Body.ArticulatedFrame.Position, AxisTolerance);
            AssertFrameOrthonormal(car.Body.ArticulatedFrame);
            AssertMatrixMatchesFrame(car.Body.ArticulatedFrame, car.Body.ArticulatedMatrix);

            AssertWheelsMatchBogieAndLayout(car.FrontBogie, wheelLayout);
            AssertWheelsMatchBogieAndLayout(car.RearBogie, wheelLayout);
        }

        Assert.Equal(frameDistances.Length, frameIndex);
    }

    private static double[] BuildProviderFrameDistances(BankingProfileTrainPoseFixture fixture)
    {
        TrainConsistDefinition definition = fixture.Definition;
        double bogieHalfSpacing = definition.BogieSpacing * 0.5;
        var distances = new double[definition.CarCount * 3];

        for (int i = 0; i < definition.CarCount; i++)
        {
            double bodyDistance = fixture.LeadDistance - (i * definition.CarSpacing);
            int sampleIndex = i * 3;
            distances[sampleIndex] = bodyDistance;
            distances[sampleIndex + 1] = bodyDistance + bogieHalfSpacing;
            distances[sampleIndex + 2] = bodyDistance - bogieHalfSpacing;
        }

        return distances;
    }

    private static ExportTrackFrame[] SampleProfileFramesInProviderOrder(
        BankingProfileTrainPoseFixture fixture,
        IReadOnlyList<double> distances)
    {
        IndexedDistance[] sortedSamples = new IndexedDistance[distances.Count];
        for (int i = 0; i < distances.Count; i++)
        {
            sortedSamples[i] = new IndexedDistance(distances[i], i);
        }

        Array.Sort(
            sortedSamples,
            (left, right) =>
            {
                int distanceComparison = left.Distance.CompareTo(right.Distance);
                return distanceComparison != 0
                    ? distanceComparison
                    : left.OriginalIndex.CompareTo(right.OriginalIndex);
            });

        double[] sortedDistances = sortedSamples.Select(sample => sample.Distance).ToArray();
        ExportTrackFrame[] sortedFrames = BankingProfileSampler.SampleFramesAtDistances(
            fixture.Document,
            new TrackEvaluator(fixture.Document),
            fixture.BankingProfile,
            sortedDistances);
        var frames = new ExportTrackFrame[sortedFrames.Length];

        for (int i = 0; i < sortedFrames.Length; i++)
        {
            frames[sortedSamples[i].OriginalIndex] = sortedFrames[i];
        }

        return frames;
    }

    private static void AssertExportMatchesRuntimePose(TrainPoseResult pose, TrainPoseExportV1Dto export)
    {
        AssertNear(pose.LeadDistance, export.LeadDistance, DistanceTolerance);
        Assert.Equal(pose.Definition.CarCount, export.Definition.CarCount);
        AssertNear(pose.Definition.CarSpacing, export.Definition.CarSpacing, DistanceTolerance);
        AssertNear(pose.Definition.CarGeometry.Length, export.Definition.CarGeometry.Length, DistanceTolerance);
        AssertNear(pose.Definition.BogieSpacing, export.Definition.BogieLayout.BogieSpacing, DistanceTolerance);
        Assert.NotNull(export.Definition.WheelLayout);

        for (int i = 0; i < pose.CarsReadOnly.Count; i++)
        {
            AssertExportCarMatchesRuntime(pose.CarsReadOnly[i], export.Cars[i]);
        }
    }

    private static void AssertExportCarMatchesRuntime(
        ArticulatedTrainCarWithWheelsTransform expected,
        ArticulatedTrainCarWithWheelsV1Dto actual)
    {
        AssertExportTrainCarMatchesRuntime(expected.Body.OriginalBody, actual.Body.OriginalBody);
        AssertExportBogieMatchesRuntime(expected.Body.FrontBogie, actual.Body.FrontBogie);
        AssertExportBogieMatchesRuntime(expected.Body.RearBogie, actual.Body.RearBogie);
        AssertExportFrameMatchesRuntime(expected.Body.ArticulatedFrame, actual.Body.ArticulatedFrame);
        AssertExportMatrixMatchesRuntime(expected.Body.ArticulatedMatrix, actual.Body.ArticulatedMatrix);
        AssertDtoMatrixMatchesFrame(actual.Body.ArticulatedFrame, actual.Body.ArticulatedMatrix);
        AssertNear(expected.Body.CenterDistance, actual.Body.CenterDistance, DistanceTolerance);

        AssertExportBogieWithWheelsMatchesRuntime(expected.FrontBogie, actual.FrontBogie);
        AssertExportBogieWithWheelsMatchesRuntime(expected.RearBogie, actual.RearBogie);
    }

    private static void AssertExportTrainCarMatchesRuntime(
        TrainCarTransform expected,
        TrainCarTransformV1Dto actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertExportFrameMatchesRuntime(expected.Frame, actual.Frame);
        AssertExportMatrixMatchesRuntime(expected.Matrix, actual.Matrix);
        AssertDtoMatrixMatchesFrame(actual.Frame, actual.Matrix);
    }

    private static void AssertExportBogieWithWheelsMatchesRuntime(
        TrainBogieWithWheelsTransform expected,
        TrainBogieWithWheelsV1Dto actual)
    {
        AssertExportBogieMatchesRuntime(expected.Bogie, actual.Bogie);
        Assert.Equal(expected.WheelsReadOnly.Count, actual.Wheels.Length);

        for (int i = 0; i < expected.WheelsReadOnly.Count; i++)
        {
            AssertExportWheelMatchesRuntime(expected.WheelsReadOnly[i], actual.Wheels[i]);
        }
    }

    private static void AssertExportBogieMatchesRuntime(BogieTransform expected, BogieTransformV1Dto actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertExportFrameMatchesRuntime(expected.Frame, actual.Frame);
        AssertExportMatrixMatchesRuntime(expected.Matrix, actual.Matrix);
        AssertDtoMatrixMatchesFrame(actual.Frame, actual.Matrix);
    }

    private static void AssertExportWheelMatchesRuntime(WheelTransform expected, WheelTransformV1Dto actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        Assert.Equal(expected.WheelIndex, actual.WheelIndex);
        AssertNear(expected.LocalOffsetX, actual.LocalOffsetX, DistanceTolerance);
        AssertNear(expected.LocalOffsetY, actual.LocalOffsetY, DistanceTolerance);
        AssertNear(expected.LocalOffsetZ, actual.LocalOffsetZ, DistanceTolerance);
        AssertExportFrameMatchesRuntime(expected.Frame, actual.Frame);
        AssertExportMatrixMatchesRuntime(expected.Matrix, actual.Matrix);
        AssertDtoMatrixMatchesFrame(actual.Frame, actual.Matrix);
    }

    private static void AssertExportFrameMatchesRuntime(ExportTrackFrame expected, TrackFrameV1Dto actual)
    {
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertVectorNear(expected.Position, actual.Position, AxisTolerance);
        AssertVectorNear(expected.Tangent, actual.Tangent, AxisTolerance);
        AssertVectorNear(expected.Normal, actual.Normal, AxisTolerance);
        AssertVectorNear(expected.Binormal, actual.Binormal, AxisTolerance);
    }

    private static void AssertExportMatrixMatchesRuntime(Matrix4x4 expected, Matrix4x4V1Dto actual)
    {
        AssertNear(expected.M11, actual.M11, MatrixTolerance);
        AssertNear(expected.M12, actual.M12, MatrixTolerance);
        AssertNear(expected.M13, actual.M13, MatrixTolerance);
        AssertNear(expected.M14, actual.M14, MatrixTolerance);
        AssertNear(expected.M21, actual.M21, MatrixTolerance);
        AssertNear(expected.M22, actual.M22, MatrixTolerance);
        AssertNear(expected.M23, actual.M23, MatrixTolerance);
        AssertNear(expected.M24, actual.M24, MatrixTolerance);
        AssertNear(expected.M31, actual.M31, MatrixTolerance);
        AssertNear(expected.M32, actual.M32, MatrixTolerance);
        AssertNear(expected.M33, actual.M33, MatrixTolerance);
        AssertNear(expected.M34, actual.M34, MatrixTolerance);
        AssertNear(expected.M41, actual.M41, MatrixTolerance);
        AssertNear(expected.M42, actual.M42, MatrixTolerance);
        AssertNear(expected.M43, actual.M43, MatrixTolerance);
        AssertNear(expected.M44, actual.M44, MatrixTolerance);
    }

    private static void AssertExportMatrixMatchesRuntime(Matrix4x4d expected, Matrix4x4V1Dto actual)
    {
        AssertNear(expected.M11, actual.M11, MatrixTolerance);
        AssertNear(expected.M12, actual.M12, MatrixTolerance);
        AssertNear(expected.M13, actual.M13, MatrixTolerance);
        AssertNear(expected.M14, actual.M14, MatrixTolerance);
        AssertNear(expected.M21, actual.M21, MatrixTolerance);
        AssertNear(expected.M22, actual.M22, MatrixTolerance);
        AssertNear(expected.M23, actual.M23, MatrixTolerance);
        AssertNear(expected.M24, actual.M24, MatrixTolerance);
        AssertNear(expected.M31, actual.M31, MatrixTolerance);
        AssertNear(expected.M32, actual.M32, MatrixTolerance);
        AssertNear(expected.M33, actual.M33, MatrixTolerance);
        AssertNear(expected.M34, actual.M34, MatrixTolerance);
        AssertNear(expected.M41, actual.M41, MatrixTolerance);
        AssertNear(expected.M42, actual.M42, MatrixTolerance);
        AssertNear(expected.M43, actual.M43, MatrixTolerance);
        AssertNear(expected.M44, actual.M44, MatrixTolerance);
    }

    private static void AssertDtoMatrixMatchesFrame(TrackFrameV1Dto frame, Matrix4x4V1Dto matrix)
    {
        AssertNear(frame.Tangent.X, matrix.M11, MatrixTolerance);
        AssertNear(frame.Normal.X, matrix.M12, MatrixTolerance);
        AssertNear(frame.Binormal.X, matrix.M13, MatrixTolerance);
        AssertNear(frame.Position.X, matrix.M14, MatrixTolerance);
        AssertNear(frame.Tangent.Y, matrix.M21, MatrixTolerance);
        AssertNear(frame.Normal.Y, matrix.M22, MatrixTolerance);
        AssertNear(frame.Binormal.Y, matrix.M23, MatrixTolerance);
        AssertNear(frame.Position.Y, matrix.M24, MatrixTolerance);
        AssertNear(frame.Tangent.Z, matrix.M31, MatrixTolerance);
        AssertNear(frame.Normal.Z, matrix.M32, MatrixTolerance);
        AssertNear(frame.Binormal.Z, matrix.M33, MatrixTolerance);
        AssertNear(frame.Position.Z, matrix.M34, MatrixTolerance);
        AssertNear(0.0, matrix.M41, MatrixTolerance);
        AssertNear(0.0, matrix.M42, MatrixTolerance);
        AssertNear(0.0, matrix.M43, MatrixTolerance);
        AssertNear(1.0, matrix.M44, MatrixTolerance);
    }

    private static void AssertPoseNear(TrainPoseResult expected, TrainPoseResult actual)
    {
        AssertNear(expected.LeadDistance, actual.LeadDistance, DistanceTolerance);
        Assert.Equal(expected.CarsReadOnly.Count, actual.CarsReadOnly.Count);

        for (int i = 0; i < expected.CarsReadOnly.Count; i++)
        {
            AssertCarNear(expected.CarsReadOnly[i], actual.CarsReadOnly[i]);
        }
    }

    private static void AssertCarNear(
        ArticulatedTrainCarWithWheelsTransform expected,
        ArticulatedTrainCarWithWheelsTransform actual)
    {
        AssertTrainCarNear(expected.Body.OriginalBody, actual.Body.OriginalBody);
        AssertBogieNear(expected.Body.FrontBogie, actual.Body.FrontBogie);
        AssertBogieNear(expected.Body.RearBogie, actual.Body.RearBogie);
        AssertTrackFrameNear(expected.Body.ArticulatedFrame, actual.Body.ArticulatedFrame);
        AssertMatrixNear(expected.Body.ArticulatedMatrix, actual.Body.ArticulatedMatrix);
        AssertBogieWithWheelsNear(expected.FrontBogie, actual.FrontBogie);
        AssertBogieWithWheelsNear(expected.RearBogie, actual.RearBogie);
    }

    private static void AssertBogieWithWheelsNear(
        TrainBogieWithWheelsTransform expected,
        TrainBogieWithWheelsTransform actual)
    {
        AssertBogieNear(expected.Bogie, actual.Bogie);
        Assert.Equal(expected.WheelsReadOnly.Count, actual.WheelsReadOnly.Count);
        for (int i = 0; i < expected.WheelsReadOnly.Count; i++)
        {
            WheelTransform expectedWheel = expected.WheelsReadOnly[i];
            WheelTransform actualWheel = actual.WheelsReadOnly[i];
            Assert.Equal(expectedWheel.CarIndex, actualWheel.CarIndex);
            Assert.Equal(expectedWheel.BogieIndex, actualWheel.BogieIndex);
            Assert.Equal(expectedWheel.WheelIndex, actualWheel.WheelIndex);
            AssertNear(expectedWheel.LocalOffsetX, actualWheel.LocalOffsetX, DistanceTolerance);
            AssertNear(expectedWheel.LocalOffsetY, actualWheel.LocalOffsetY, DistanceTolerance);
            AssertNear(expectedWheel.LocalOffsetZ, actualWheel.LocalOffsetZ, DistanceTolerance);
            AssertTrackFrameNear(expectedWheel.Frame, actualWheel.Frame);
            AssertMatrixNear(expectedWheel.Matrix, actualWheel.Matrix);
        }
    }

    private static void AssertTrainCarNear(TrainCarTransform expected, TrainCarTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertBogieNear(BogieTransform expected, BogieTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertWheelsMatchBogieAndLayout(
        TrainBogieWithWheelsTransform bogieWithWheels,
        TrainWheelLayout wheelLayout)
    {
        BogieTransform bogie = bogieWithWheels.Bogie;
        WheelTransform[] wheels = bogieWithWheels.Wheels;
        int axleCount = (wheelLayout.WheelCountPerBogie + 1) / 2;
        double centeredAxleOffset = (axleCount - 1) * 0.5;
        double sideOffsetMagnitude = wheelLayout.WheelWidth * 0.5;

        Assert.Equal(wheelLayout.WheelCountPerBogie, wheels.Length);

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelTransform wheel = wheels[i];
            int axleIndex = i / 2;
            double expectedLocalOffsetX = (axleIndex - centeredAxleOffset) * wheelLayout.AxleSpacing;
            double expectedLocalOffsetY = (i % 2 == 0 ? -1.0 : 1.0) * sideOffsetMagnitude;

            Assert.Equal(bogie.CarIndex, wheel.CarIndex);
            Assert.Equal(bogie.BogieIndex, wheel.BogieIndex);
            Assert.Equal(i, wheel.WheelIndex);
            AssertNear(expectedLocalOffsetX, wheel.LocalOffsetX, DistanceTolerance);
            AssertNear(expectedLocalOffsetY, wheel.LocalOffsetY, DistanceTolerance);
            AssertNear(0.0, wheel.LocalOffsetZ, DistanceTolerance);
            AssertTrackFrameNear(bogie.Frame, wheel.Frame);
            AssertMatrixNear(bogie.Matrix, wheel.Matrix);
        }
    }

    private static void AssertCenterlineSamplePreserved(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertVectorNear(expected.Position, actual.Position, AxisTolerance);
        AssertVectorNear(expected.Tangent, actual.Tangent, AxisTolerance);
    }

    private static void AssertVectorNotNear(Vector3d expected, Vector3d actual)
    {
        double delta =
            System.Math.Abs(expected.X - actual.X) +
            System.Math.Abs(expected.Y - actual.Y) +
            System.Math.Abs(expected.Z - actual.Z);

        Assert.True(delta > AxisTolerance, $"Expected vectors to differ by more than {AxisTolerance}, but delta was {delta}.");
    }

    private static void AssertTrackFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance, DistanceTolerance);
        AssertVectorNear(expected.Position, actual.Position, AxisTolerance);
        AssertVectorNear(expected.Tangent, actual.Tangent, AxisTolerance);
        AssertVectorNear(expected.Normal, actual.Normal, AxisTolerance);
        AssertVectorNear(expected.Binormal, actual.Binormal, AxisTolerance);
    }

    private static void AssertFrameOrthonormal(ExportTrackFrame frame)
    {
        AssertNear(1.0, frame.Tangent.Length, AxisTolerance);
        AssertNear(1.0, frame.Normal.Length, AxisTolerance);
        AssertNear(1.0, frame.Binormal.Length, AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal), AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal), AxisTolerance);
        AssertNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal), AxisTolerance);

        Vector3d expectedBinormal = Vector3d.Cross(frame.Tangent, frame.Normal).Normalized();
        AssertVectorNear(expectedBinormal, frame.Binormal, AxisTolerance);
    }

    private static void AssertMatrixMatchesFrame(ExportTrackFrame frame, Matrix4x4 matrix)
    {
        AssertMatrixNear(frame.ToMatrix4x4(), matrix);
    }

    private static void AssertMatrixMatchesFrame(ExportTrackFrame frame, Matrix4x4d matrix)
    {
        AssertMatrixNear(Matrix4x4d.FromMatrix4x4(frame.ToMatrix4x4()), matrix);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertNear(expected.M11, actual.M11, MatrixTolerance);
        AssertNear(expected.M12, actual.M12, MatrixTolerance);
        AssertNear(expected.M13, actual.M13, MatrixTolerance);
        AssertNear(expected.M14, actual.M14, MatrixTolerance);
        AssertNear(expected.M21, actual.M21, MatrixTolerance);
        AssertNear(expected.M22, actual.M22, MatrixTolerance);
        AssertNear(expected.M23, actual.M23, MatrixTolerance);
        AssertNear(expected.M24, actual.M24, MatrixTolerance);
        AssertNear(expected.M31, actual.M31, MatrixTolerance);
        AssertNear(expected.M32, actual.M32, MatrixTolerance);
        AssertNear(expected.M33, actual.M33, MatrixTolerance);
        AssertNear(expected.M34, actual.M34, MatrixTolerance);
        AssertNear(expected.M41, actual.M41, MatrixTolerance);
        AssertNear(expected.M42, actual.M42, MatrixTolerance);
        AssertNear(expected.M43, actual.M43, MatrixTolerance);
        AssertNear(expected.M44, actual.M44, MatrixTolerance);
    }

    private static void AssertMatrixNear(Matrix4x4d expected, Matrix4x4d actual)
    {
        AssertNear(expected.M11, actual.M11, MatrixTolerance);
        AssertNear(expected.M12, actual.M12, MatrixTolerance);
        AssertNear(expected.M13, actual.M13, MatrixTolerance);
        AssertNear(expected.M14, actual.M14, MatrixTolerance);
        AssertNear(expected.M21, actual.M21, MatrixTolerance);
        AssertNear(expected.M22, actual.M22, MatrixTolerance);
        AssertNear(expected.M23, actual.M23, MatrixTolerance);
        AssertNear(expected.M24, actual.M24, MatrixTolerance);
        AssertNear(expected.M31, actual.M31, MatrixTolerance);
        AssertNear(expected.M32, actual.M32, MatrixTolerance);
        AssertNear(expected.M33, actual.M33, MatrixTolerance);
        AssertNear(expected.M34, actual.M34, MatrixTolerance);
        AssertNear(expected.M41, actual.M41, MatrixTolerance);
        AssertNear(expected.M42, actual.M42, MatrixTolerance);
        AssertNear(expected.M43, actual.M43, MatrixTolerance);
        AssertNear(expected.M44, actual.M44, MatrixTolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        AssertNear(expected.X, actual.X, tolerance);
        AssertNear(expected.Y, actual.Y, tolerance);
        AssertNear(expected.Z, actual.Z, tolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3dV1Dto actual, double tolerance)
    {
        AssertNear(expected.X, actual.X, tolerance);
        AssertNear(expected.Y, actual.Y, tolerance);
        AssertNear(expected.Z, actual.Z, tolerance);
    }

    private static void AssertVectorNear(Vector3d expected, DebugViewportVector3dV1Dto actual, double tolerance)
    {
        AssertNear(expected.X, actual.X, tolerance);
        AssertNear(expected.Y, actual.Y, tolerance);
        AssertNear(expected.Z, actual.Z, tolerance);
    }

    private static void AssertVectorNear(Vector3dV1Dto expected, Vector3dV1Dto actual, double tolerance)
    {
        AssertNear(expected.X, actual.X, tolerance);
        AssertNear(expected.Y, actual.Y, tolerance);
        AssertNear(expected.Z, actual.Z, tolerance);
    }

    private static void AssertNear(double expected, double actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, tolerance);
    }

    private static string FormatTrainPoseDiagnostics(IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
    }

    private static string FormatDebugViewportDiagnostics(IReadOnlyList<DebugViewportSnapshotV1ValidationDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
    }

    private readonly struct IndexedDistance
    {
        public IndexedDistance(double distance, int originalIndex)
        {
            Distance = distance;
            OriginalIndex = originalIndex;
        }

        public double Distance { get; }

        public int OriginalIndex { get; }
    }
}
