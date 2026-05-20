using System;
using System.Collections.Generic;
using System.Linq;
using Quantum.IO.TrainPose.V1;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrainPoseExportV1CompositeDocumentBoundaryTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Export_ZeroRollCompositeTrainPose_RoundTripsAndValidates()
    {
        GeometricSection[] sections =
        {
            new GeometricSection(length: 5.0, curvature: 0.12, roll: 0.0),
            new GeometricSection(length: 3.0),
            new GeometricSection(length: 4.0, curvature: -0.08, roll: 0.0)
        };
        TrackDocument document = GeometricSectionTrackDocumentBuilder.BuildZeroRollCompositeDocument(
            sections,
            segmentId: "composite-train-pose-export-boundary-regression");
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 3.0,
            carLength: 2.0,
            carWidth: 1.2,
            carHeight: 1.4,
            bogieSpacing: 1.2,
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: 2,
                wheelRadius: 0.35,
                wheelWidth: 0.4,
                axleSpacing: 0.0));
        const double leadDistance = 8.5;

        TrainPoseResult pose = provider.EvaluateTrainPose(leadDistance, definition);

        Assert.Equal(definition.CarCount, pose.CarsReadOnly.Count);

        TrainPoseExportV1Dto export = TrainPoseExportV1Mapper.Export(pose);

        Assert.Equal(TrainPoseExportV1Dto.ContractName, export.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, export.Version);
        Assert.Equal(definition.CarCount, export.Cars.Length);
        AssertExportHierarchyPresent(export, definition);
        AssertDistancesMatchPose(pose, export);
        AssertExportFinite(export);

        string json = TrainPoseExportV1Json.Serialize(export);
        TrainPoseExportV1Dto roundtrip = TrainPoseExportV1Json.Deserialize(json);

        Assert.Equal(export.Cars.Length, roundtrip.Cars.Length);
        AssertDistancesMatchExport(export, roundtrip);
        AssertExportFinite(roundtrip);

        bool isValid = TrainPoseExportV1Validator.TryValidate(
            roundtrip,
            out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid, FormatDiagnostics(diagnostics));
        Assert.Empty(diagnostics);
    }

    private static void AssertExportHierarchyPresent(
        TrainPoseExportV1Dto export,
        TrainConsistDefinition definition)
    {
        Assert.NotNull(export.Definition);
        Assert.NotNull(export.Definition.CarGeometry);
        Assert.NotNull(export.Definition.BogieLayout);
        Assert.NotNull(export.Definition.WheelLayout);
        Assert.Equal(definition.WheelLayout!.WheelCountPerBogie, export.Definition.WheelLayout!.WheelCountPerBogie);

        for (int i = 0; i < export.Cars.Length; i++)
        {
            ArticulatedTrainCarWithWheelsV1Dto car = export.Cars[i];

            Assert.NotNull(car.Body);
            Assert.NotNull(car.Body.OriginalBody);
            Assert.NotNull(car.Body.FrontBogie);
            Assert.NotNull(car.Body.RearBogie);
            Assert.NotNull(car.Body.ArticulatedFrame);
            Assert.NotNull(car.Body.ArticulatedMatrix);

            Assert.NotNull(car.FrontBogie);
            Assert.NotNull(car.FrontBogie.Bogie);
            Assert.NotNull(car.FrontBogie.Wheels);
            Assert.Equal(definition.WheelLayout.WheelCountPerBogie, car.FrontBogie.Wheels.Length);

            Assert.NotNull(car.RearBogie);
            Assert.NotNull(car.RearBogie.Bogie);
            Assert.NotNull(car.RearBogie.Wheels);
            Assert.Equal(definition.WheelLayout.WheelCountPerBogie, car.RearBogie.Wheels.Length);
        }
    }

    private static void AssertDistancesMatchPose(
        TrainPoseResult expected,
        TrainPoseExportV1Dto actual)
    {
        AssertDoubleNear(expected.LeadDistance, actual.LeadDistance);
        Assert.Equal(expected.CarsReadOnly.Count, actual.Cars.Length);

        for (int i = 0; i < expected.CarsReadOnly.Count; i++)
        {
            ArticulatedTrainCarWithWheelsTransform expectedCar = expected.CarsReadOnly[i];
            ArticulatedTrainCarWithWheelsV1Dto actualCar = actual.Cars[i];

            AssertDoubleNear(expectedCar.Body.OriginalBody.Distance, actualCar.Body.OriginalBody.Distance);
            AssertDoubleNear(expectedCar.Body.OriginalBody.Frame.Distance, actualCar.Body.OriginalBody.Frame.Distance);
            AssertDoubleNear(expectedCar.Body.CenterDistance, actualCar.Body.CenterDistance);
            AssertDoubleNear(expectedCar.Body.ArticulatedFrame.Distance, actualCar.Body.ArticulatedFrame.Distance);
            AssertDoubleNear(expectedCar.Body.FrontBogie.Distance, actualCar.Body.FrontBogie.Distance);
            AssertDoubleNear(expectedCar.Body.RearBogie.Distance, actualCar.Body.RearBogie.Distance);
            AssertDoubleNear(expectedCar.FrontBogie.Bogie.Distance, actualCar.FrontBogie.Bogie.Distance);
            AssertDoubleNear(expectedCar.RearBogie.Bogie.Distance, actualCar.RearBogie.Bogie.Distance);
            AssertWheelDistancesMatch(expectedCar.FrontBogie.WheelsReadOnly, actualCar.FrontBogie.Wheels);
            AssertWheelDistancesMatch(expectedCar.RearBogie.WheelsReadOnly, actualCar.RearBogie.Wheels);
        }
    }

    private static void AssertDistancesMatchExport(
        TrainPoseExportV1Dto expected,
        TrainPoseExportV1Dto actual)
    {
        AssertDoubleNear(expected.LeadDistance, actual.LeadDistance);
        Assert.Equal(expected.Cars.Length, actual.Cars.Length);

        for (int i = 0; i < expected.Cars.Length; i++)
        {
            ArticulatedTrainCarWithWheelsV1Dto expectedCar = expected.Cars[i];
            ArticulatedTrainCarWithWheelsV1Dto actualCar = actual.Cars[i];

            AssertDoubleNear(expectedCar.Body.OriginalBody.Distance, actualCar.Body.OriginalBody.Distance);
            AssertDoubleNear(expectedCar.Body.OriginalBody.Frame.Distance, actualCar.Body.OriginalBody.Frame.Distance);
            AssertDoubleNear(expectedCar.Body.CenterDistance, actualCar.Body.CenterDistance);
            AssertDoubleNear(expectedCar.Body.ArticulatedFrame.Distance, actualCar.Body.ArticulatedFrame.Distance);
            AssertDoubleNear(expectedCar.Body.FrontBogie.Distance, actualCar.Body.FrontBogie.Distance);
            AssertDoubleNear(expectedCar.Body.RearBogie.Distance, actualCar.Body.RearBogie.Distance);
            AssertDoubleNear(expectedCar.FrontBogie.Bogie.Distance, actualCar.FrontBogie.Bogie.Distance);
            AssertDoubleNear(expectedCar.RearBogie.Bogie.Distance, actualCar.RearBogie.Bogie.Distance);
            AssertWheelDistancesMatch(expectedCar.FrontBogie.Wheels, actualCar.FrontBogie.Wheels);
            AssertWheelDistancesMatch(expectedCar.RearBogie.Wheels, actualCar.RearBogie.Wheels);
        }
    }

    private static void AssertWheelDistancesMatch(
        IReadOnlyList<WheelTransform> expected,
        WheelTransformV1Dto[] actual)
    {
        Assert.Equal(expected.Count, actual.Length);

        for (int i = 0; i < expected.Count; i++)
        {
            AssertDoubleNear(expected[i].Frame.Distance, actual[i].Frame.Distance);
        }
    }

    private static void AssertWheelDistancesMatch(
        WheelTransformV1Dto[] expected,
        WheelTransformV1Dto[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            AssertDoubleNear(expected[i].Frame.Distance, actual[i].Frame.Distance);
        }
    }

    private static void AssertExportFinite(TrainPoseExportV1Dto dto)
    {
        AssertFinite(dto.LeadDistance);

        for (int i = 0; i < dto.Cars.Length; i++)
        {
            ArticulatedTrainCarWithWheelsV1Dto car = dto.Cars[i];

            AssertTrackFrameFinite(car.Body.OriginalBody.Frame);
            AssertMatrixFinite(car.Body.OriginalBody.Matrix);
            AssertTrackFrameFinite(car.Body.ArticulatedFrame);
            AssertMatrixFinite(car.Body.ArticulatedMatrix);
            AssertBogieFinite(car.Body.FrontBogie);
            AssertBogieFinite(car.Body.RearBogie);
            AssertBogieWithWheelsFinite(car.FrontBogie);
            AssertBogieWithWheelsFinite(car.RearBogie);
        }
    }

    private static void AssertBogieWithWheelsFinite(TrainBogieWithWheelsV1Dto bogieWithWheels)
    {
        AssertBogieFinite(bogieWithWheels.Bogie);

        for (int i = 0; i < bogieWithWheels.Wheels.Length; i++)
        {
            WheelTransformV1Dto wheel = bogieWithWheels.Wheels[i];

            AssertFinite(wheel.LocalOffsetX);
            AssertFinite(wheel.LocalOffsetY);
            AssertFinite(wheel.LocalOffsetZ);
            AssertTrackFrameFinite(wheel.Frame);
            AssertMatrixFinite(wheel.Matrix);
        }
    }

    private static void AssertBogieFinite(BogieTransformV1Dto bogie)
    {
        AssertFinite(bogie.Distance);
        AssertTrackFrameFinite(bogie.Frame);
        AssertMatrixFinite(bogie.Matrix);
    }

    private static void AssertTrackFrameFinite(TrackFrameV1Dto frame)
    {
        AssertFinite(frame.Distance);
        AssertVectorFinite(frame.Position);
        AssertVectorFinite(frame.Tangent);
        AssertVectorFinite(frame.Normal);
        AssertVectorFinite(frame.Binormal);
    }

    private static void AssertVectorFinite(Vector3dV1Dto vector)
    {
        AssertFinite(vector.X);
        AssertFinite(vector.Y);
        AssertFinite(vector.Z);
    }

    private static void AssertMatrixFinite(Matrix4x4V1Dto matrix)
    {
        AssertFinite(matrix.M11);
        AssertFinite(matrix.M12);
        AssertFinite(matrix.M13);
        AssertFinite(matrix.M14);
        AssertFinite(matrix.M21);
        AssertFinite(matrix.M22);
        AssertFinite(matrix.M23);
        AssertFinite(matrix.M24);
        AssertFinite(matrix.M31);
        AssertFinite(matrix.M32);
        AssertFinite(matrix.M33);
        AssertFinite(matrix.M34);
        AssertFinite(matrix.M41);
        AssertFinite(matrix.M42);
        AssertFinite(matrix.M43);
        AssertFinite(matrix.M44);
    }

    private static void AssertFinite(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }

    private static string FormatDiagnostics(IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
    }
}
