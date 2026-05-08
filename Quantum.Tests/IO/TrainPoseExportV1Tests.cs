using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainPoseExportV1Tests
{
    [Fact]
    public void Export_SetsContractAndVersion()
    {
        TrainPoseResult source = CreateSourcePoseResult();

        TrainPoseExportV1Dto export = TrainPoseExportV1Mapper.Export(source);

        Assert.Equal("quantum.train_pose", export.Contract);
        Assert.Equal(1, export.Version);
    }

    [Fact]
    public void Export_CopiesDefinitionSnapshot()
    {
        TrainPoseResult source = CreateSourcePoseResult();

        TrainPoseExportV1Dto export = TrainPoseExportV1Mapper.Export(source);

        Assert.Equal(source.Definition.CarCount, export.Definition.CarCount);
        Assert.Equal(source.Definition.CarSpacing, export.Definition.CarSpacing);
        Assert.Equal(source.Definition.CarGeometry.Length, export.Definition.CarGeometry.Length);
        Assert.Equal(source.Definition.CarGeometry.Width, export.Definition.CarGeometry.Width);
        Assert.Equal(source.Definition.CarGeometry.Height, export.Definition.CarGeometry.Height);
        Assert.Equal(source.Definition.BogieLayout.BogieSpacing, export.Definition.BogieLayout.BogieSpacing);

        Assert.NotNull(export.Definition.WheelLayout);
        Assert.Equal(source.Definition.WheelLayout!.WheelCountPerBogie, export.Definition.WheelLayout!.WheelCountPerBogie);
        Assert.Equal(source.Definition.WheelLayout.WheelRadius, export.Definition.WheelLayout.WheelRadius);
        Assert.Equal(source.Definition.WheelLayout.WheelWidth, export.Definition.WheelLayout.WheelWidth);
        Assert.Equal(source.Definition.WheelLayout.AxleSpacing, export.Definition.WheelLayout.AxleSpacing);
    }

    [Fact]
    public void Export_CopiesCarsAndWheels()
    {
        TrainPoseResult source = CreateSourcePoseResult();
        ArticulatedTrainCarWithWheelsTransform[] sourceCars = source.Cars;

        TrainPoseExportV1Dto export = TrainPoseExportV1Mapper.Export(source);

        Assert.Equal(sourceCars.Length, export.Cars.Length);

        for (int i = 0; i < sourceCars.Length; i++)
        {
            Assert.Equal(sourceCars[i].Body.OriginalBody.CarIndex, export.Cars[i].Body.OriginalBody.CarIndex);
            Assert.Equal(sourceCars[i].FrontBogie.Wheels.Length, export.Cars[i].FrontBogie.Wheels.Length);
            Assert.Equal(sourceCars[i].RearBogie.Wheels.Length, export.Cars[i].RearBogie.Wheels.Length);

            Assert.Equal(sourceCars[i].FrontBogie.Wheels[0].WheelIndex, export.Cars[i].FrontBogie.Wheels[0].WheelIndex);
            Assert.Equal(sourceCars[i].RearBogie.Wheels[0].WheelIndex, export.Cars[i].RearBogie.Wheels[0].WheelIndex);
        }
    }

    [Fact]
    public void Export_DoesNotShareMutableArrayReferences()
    {
        TrainPoseResult source = CreateSourcePoseResult();
        ArticulatedTrainCarWithWheelsTransform[] sourceCars = source.Cars;
        WheelTransform[] sourceFrontWheels = sourceCars[0].FrontBogie.Wheels;

        TrainPoseExportV1Dto export = TrainPoseExportV1Mapper.Export(source);

        Assert.False(ReferenceEquals(sourceCars, export.Cars));
        Assert.False(ReferenceEquals(sourceFrontWheels, export.Cars[0].FrontBogie.Wheels));

        int expectedCarIndex = export.Cars[0].Body.OriginalBody.CarIndex;
        int expectedWheelIndex = export.Cars[0].FrontBogie.Wheels[0].WheelIndex;

        sourceCars[0] = sourceCars[1];
        sourceFrontWheels[0] = sourceFrontWheels[1];

        Assert.Equal(expectedCarIndex, export.Cars[0].Body.OriginalBody.CarIndex);
        Assert.Equal(expectedWheelIndex, export.Cars[0].FrontBogie.Wheels[0].WheelIndex);
    }

    [Fact]
    public void Export_PreservesRepresentativeFrameAndMatrixValues()
    {
        TrainPoseResult source = CreateSourcePoseResult();
        ArticulatedTrainCarWithWheelsTransform sourceCar = source.Cars[0];

        TrainPoseExportV1Dto export = TrainPoseExportV1Mapper.Export(source);
        ArticulatedTrainCarWithWheelsV1Dto exportedCar = export.Cars[0];

        Assert.Equal(sourceCar.Body.ArticulatedFrame.Distance, exportedCar.Body.ArticulatedFrame.Distance);
        Assert.Equal(sourceCar.Body.ArticulatedFrame.Position.X, exportedCar.Body.ArticulatedFrame.Position.X);
        Assert.Equal(sourceCar.Body.ArticulatedFrame.Position.Y, exportedCar.Body.ArticulatedFrame.Position.Y);
        Assert.Equal(sourceCar.Body.ArticulatedFrame.Position.Z, exportedCar.Body.ArticulatedFrame.Position.Z);
        Assert.Equal(sourceCar.Body.ArticulatedFrame.Tangent.X, exportedCar.Body.ArticulatedFrame.Tangent.X);

        Assert.Equal((double)sourceCar.Body.OriginalBody.Matrix.M12, exportedCar.Body.OriginalBody.Matrix.M12);
        Assert.Equal((double)sourceCar.Body.OriginalBody.Matrix.M34, exportedCar.Body.OriginalBody.Matrix.M34);
        Assert.Equal(sourceCar.Body.ArticulatedMatrix.M21, exportedCar.Body.ArticulatedMatrix.M21);
        Assert.Equal(sourceCar.Body.ArticulatedMatrix.M43, exportedCar.Body.ArticulatedMatrix.M43);
        Assert.Equal(sourceCar.FrontBogie.Bogie.Matrix.M14, exportedCar.FrontBogie.Bogie.Matrix.M14);
        Assert.Equal(sourceCar.FrontBogie.Wheels[0].Matrix.M44, exportedCar.FrontBogie.Wheels[0].Matrix.M44);
    }

    [Fact]
    public void Serialize_IncludesExpectedCamelCaseTopLevelFields()
    {
        TrainPoseExportV1Dto dto = TrainPoseExportV1Mapper.Export(CreateSourcePoseResult());

        string json = TrainPoseExportV1Json.Serialize(dto);

        Assert.Contains("\"contract\":", json);
        Assert.Contains("\"version\":", json);
        Assert.Contains("\"leadDistance\":", json);
        Assert.Contains("\"definition\":", json);
        Assert.Contains("\"cars\":", json);

        Assert.DoesNotContain("\"Contract\":", json);
        Assert.DoesNotContain("\"Version\":", json);
    }

    [Fact]
    public void Serialize_Indented_MatchesGoldenFixture()
    {
        TrainPoseExportV1Dto dto = TrainPoseExportV1Mapper.Export(CreateGoldenFixtureSourcePoseResult());

        string actual = NormalizeLineEndings(TrainPoseExportV1Json.Serialize(dto, indented: true)).TrimEnd();
        string expected = NormalizeLineEndings(LoadGoldenFixtureJson()).TrimEnd();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Deserialize_GoldenFixture_PreservesContractVersionAndRepresentativeValues()
    {
        TrainPoseExportV1Dto dto = TrainPoseExportV1Json.Deserialize(LoadGoldenFixtureJson());

        Assert.Equal(TrainPoseExportV1Dto.ContractName, dto.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, dto.Version);
        Assert.Equal(21.5, dto.LeadDistance);
        Assert.Equal(1, dto.Definition.CarCount);
        ArticulatedTrainCarWithWheelsV1Dto car = Assert.Single(dto.Cars);
        Assert.Equal(0, car.Body.OriginalBody.CarIndex);
        Assert.Equal(20.0, car.Body.OriginalBody.Distance);
        Assert.Equal(20.5, car.Body.ArticulatedFrame.Distance);
        Assert.Equal(4.0, car.Body.ArticulatedFrame.Position.X);
        Assert.Equal(12.0, car.Body.OriginalBody.Matrix.M12);
        Assert.Equal(516.0, car.Body.ArticulatedMatrix.M44);

        Assert.Equal(0, car.FrontBogie.Bogie.BogieIndex);
        Assert.Equal(21.25, car.FrontBogie.Bogie.Frame.Distance);
        Assert.Equal(102.0, car.FrontBogie.Bogie.Matrix.M12);

        Assert.Equal(2, car.FrontBogie.Wheels.Length);
        WheelTransformV1Dto frontWheel = car.FrontBogie.Wheels[0];
        Assert.Equal(0, frontWheel.WheelIndex);
        Assert.Equal(-0.35, frontWheel.LocalOffsetX);
        Assert.Equal(21.25, frontWheel.Frame.Distance);
        Assert.Equal(102.1, frontWheel.Matrix.M12);

        Assert.Equal(1, car.RearBogie.Bogie.BogieIndex);
        Assert.Equal(18.75, car.RearBogie.Bogie.Frame.Distance);
        Assert.Equal(216.0, car.RearBogie.Bogie.Matrix.M44);
    }

    [Fact]
    public void SerializeDeserialize_RoundtripPreservesRepresentativeValues()
    {
        TrainPoseExportV1Dto expected = TrainPoseExportV1Mapper.Export(CreateSourcePoseResult());

        string json = TrainPoseExportV1Json.Serialize(expected);
        TrainPoseExportV1Dto actual = TrainPoseExportV1Json.Deserialize(json);

        Assert.Equal(expected.Contract, actual.Contract);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.LeadDistance, actual.LeadDistance);
        Assert.Equal(expected.Definition.CarCount, actual.Definition.CarCount);
        Assert.Equal(expected.Definition.CarSpacing, actual.Definition.CarSpacing);
        Assert.Equal(expected.Definition.CarGeometry.Length, actual.Definition.CarGeometry.Length);
        Assert.Equal(expected.Definition.BogieLayout.BogieSpacing, actual.Definition.BogieLayout.BogieSpacing);

        Assert.NotNull(actual.Definition.WheelLayout);
        Assert.Equal(expected.Definition.WheelLayout!.WheelCountPerBogie, actual.Definition.WheelLayout!.WheelCountPerBogie);
        Assert.Equal(expected.Definition.WheelLayout.WheelRadius, actual.Definition.WheelLayout.WheelRadius);

        Assert.Equal(expected.Cars.Length, actual.Cars.Length);
        Assert.Equal(expected.Cars[0].Body.OriginalBody.CarIndex, actual.Cars[0].Body.OriginalBody.CarIndex);
        Assert.Equal(expected.Cars[0].FrontBogie.Wheels[0].WheelIndex, actual.Cars[0].FrontBogie.Wheels[0].WheelIndex);
        Assert.Equal(expected.Cars[0].RearBogie.Bogie.Matrix.M44, actual.Cars[0].RearBogie.Bogie.Matrix.M44);
    }

    [Fact]
    public void SerializeDeserialize_RoundtripPreservesNullWheelLayout()
    {
        TrainPoseExportV1Dto dto = TrainPoseExportV1Mapper.Export(CreateSourcePoseResultWithoutWheelLayout());
        Assert.Null(dto.Definition.WheelLayout);

        string json = TrainPoseExportV1Json.Serialize(dto);
        TrainPoseExportV1Dto roundtrip = TrainPoseExportV1Json.Deserialize(json);

        Assert.Null(roundtrip.Definition.WheelLayout);
    }

    [Fact]
    public void Deserialize_RejectsWrongContract()
    {
        string json = CreateMinimalJson(contract: "wrong.contract", version: TrainPoseExportV1Dto.ContractVersion);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TrainPoseExportV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsWrongVersion()
    {
        string json = CreateMinimalJson(contract: TrainPoseExportV1Dto.ContractName, version: 999);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => TrainPoseExportV1Json.Deserialize(json));

        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string malformedJson = "{ \"contract\": \"quantum.train_pose\", \"version\": 1, ";

        JsonException ex = Assert.Throws<JsonException>(() => TrainPoseExportV1Json.Deserialize(malformedJson));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TrainPoseResult CreateSourcePoseResult()
    {
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 3.25,
            carGeometry: new TrainCarGeometry(length: 4.5, width: 1.8, height: 2.1),
            bogieLayout: new TrainBogieLayout(bogieSpacing: 2.75),
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: 4,
                wheelRadius: 0.45,
                wheelWidth: 0.25,
                axleSpacing: 1.1));

        var cars = new[]
        {
            CreateCar(carIndex: 0, distance: 20.0),
            CreateCar(carIndex: 1, distance: 16.75)
        };

        return new TrainPoseResult(leadDistance: 21.5, definition: definition, cars: cars);
    }

    private static TrainPoseResult CreateGoldenFixtureSourcePoseResult()
    {
        var definition = new TrainConsistDefinition(
            carCount: 1,
            carSpacing: 3.25,
            carGeometry: new TrainCarGeometry(length: 4.5, width: 1.8, height: 2.1),
            bogieLayout: new TrainBogieLayout(bogieSpacing: 2.75),
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: 2,
                wheelRadius: 0.45,
                wheelWidth: 0.25,
                axleSpacing: 1.1));

        var cars = new[]
        {
            CreateCar(carIndex: 0, distance: 20.0)
        };

        return new TrainPoseResult(leadDistance: 21.5, definition: definition, cars: cars);
    }

    private static TrainPoseResult CreateSourcePoseResultWithoutWheelLayout()
    {
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 3.25,
            carGeometry: new TrainCarGeometry(length: 4.5, width: 1.8, height: 2.1),
            bogieLayout: new TrainBogieLayout(bogieSpacing: 2.75),
            wheelLayout: null);

        var cars = new[]
        {
            CreateCar(carIndex: 0, distance: 20.0),
            CreateCar(carIndex: 1, distance: 16.75)
        };

        return new TrainPoseResult(leadDistance: 21.5, definition: definition, cars: cars);
    }

    private static string CreateMinimalJson(string contract, int version)
    {
        return $@"{{
  ""contract"": ""{contract}"",
  ""version"": {version},
  ""leadDistance"": 0.0,
  ""definition"": {{
    ""carCount"": 0,
    ""carSpacing"": 0.0,
    ""carGeometry"": {{
      ""length"": 0.0,
      ""width"": 0.0,
      ""height"": 0.0
    }},
    ""bogieLayout"": {{
      ""bogieSpacing"": 0.0
    }},
    ""wheelLayout"": null
  }},
  ""cars"": []
}}";
    }

    private static string LoadGoldenFixtureJson()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", "TrainPoseExportV1.golden.json");
        Assert.True(File.Exists(fixturePath), $"Golden fixture file was not found at '{fixturePath}'.");
        return File.ReadAllText(fixturePath);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
    }

    private static ArticulatedTrainCarWithWheelsTransform CreateCar(int carIndex, double distance)
    {
        TrainBogieWithWheelsTransform frontBogie = CreateBogie(
            carIndex: carIndex,
            bogieIndex: 0,
            distance: distance + 1.25,
            seed: 100.0 + carIndex);
        TrainBogieWithWheelsTransform rearBogie = CreateBogie(
            carIndex: carIndex,
            bogieIndex: 1,
            distance: distance - 1.25,
            seed: 200.0 + carIndex);

        var originalBody = new TrainCarTransform(
            carIndex: carIndex,
            distance: distance,
            frame: CreateFrame(distance, 1.0 + carIndex, 2.0 + carIndex, 3.0 + carIndex),
            matrix: CreateFloatMatrix(10f + carIndex));

        var articulatedBody = new ArticulatedTrainCarTransform(
            originalBody: originalBody,
            frontBogie: frontBogie.Bogie,
            rearBogie: rearBogie.Bogie,
            articulatedFrame: CreateFrame(distance + 0.5, 4.0 + carIndex, 5.0 + carIndex, 6.0 + carIndex),
            articulatedMatrix: CreateDoubleMatrix(500.0 + carIndex),
            centerDistance: distance + 0.5);

        return new ArticulatedTrainCarWithWheelsTransform(
            body: articulatedBody,
            frontBogie: frontBogie,
            rearBogie: rearBogie);
    }

    private static TrainBogieWithWheelsTransform CreateBogie(int carIndex, int bogieIndex, double distance, double seed)
    {
        ExportTrackFrame frame = CreateFrame(distance, seed + 1.0, seed + 2.0, seed + 3.0);

        var bogie = new BogieTransform(
            carIndex: carIndex,
            bogieIndex: bogieIndex,
            distance: distance,
            frame: frame,
            matrix: CreateDoubleMatrix(seed));

        var wheels = new[]
        {
            CreateWheel(carIndex, bogieIndex, wheelIndex: 0, -0.35, 0.0, 0.8, frame, CreateDoubleMatrix(seed + 0.1)),
            CreateWheel(carIndex, bogieIndex, wheelIndex: 1, 0.35, 0.0, 0.8, frame, CreateDoubleMatrix(seed + 0.2))
        };

        return new TrainBogieWithWheelsTransform(bogie, wheels);
    }

    private static WheelTransform CreateWheel(
        int carIndex,
        int bogieIndex,
        int wheelIndex,
        double localOffsetX,
        double localOffsetY,
        double localOffsetZ,
        ExportTrackFrame frame,
        Matrix4x4d matrix)
    {
        return new WheelTransform(
            carIndex: carIndex,
            bogieIndex: bogieIndex,
            wheelIndex: wheelIndex,
            localOffsetX: localOffsetX,
            localOffsetY: localOffsetY,
            localOffsetZ: localOffsetZ,
            frame: frame,
            matrix: matrix);
    }

    private static ExportTrackFrame CreateFrame(double distance, double x, double y, double z)
    {
        return new ExportTrackFrame(
            distance: distance,
            position: new Vector3d(x, y, z),
            tangent: new Vector3d(1.0, 0.0, 0.0),
            normal: new Vector3d(0.0, 1.0, 0.0),
            binormal: new Vector3d(0.0, 0.0, 1.0));
    }

    private static Matrix4x4 CreateFloatMatrix(float seed)
    {
        return new Matrix4x4(
            seed + 1f, seed + 2f, seed + 3f, seed + 4f,
            seed + 5f, seed + 6f, seed + 7f, seed + 8f,
            seed + 9f, seed + 10f, seed + 11f, seed + 12f,
            seed + 13f, seed + 14f, seed + 15f, seed + 16f);
    }

    private static Matrix4x4d CreateDoubleMatrix(double seed)
    {
        return new Matrix4x4d(
            seed + 1.0, seed + 2.0, seed + 3.0, seed + 4.0,
            seed + 5.0, seed + 6.0, seed + 7.0, seed + 8.0,
            seed + 9.0, seed + 10.0, seed + 11.0, seed + 12.0,
            seed + 13.0, seed + 14.0, seed + 15.0, seed + 16.0);
    }
}
