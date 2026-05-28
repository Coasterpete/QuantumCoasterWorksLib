using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrainPoseExportV1RegressionTests
{
    [Fact]
    public void Export_CurvedSyntheticLayout_MatchesStableContractSnapshot()
    {
        TrainPoseExportV1Dto export = CreateCurvedSyntheticExport();
        TrainPoseExportV1Dto repeatedExport = CreateCurvedSyntheticExport();

        string actual = FormatExportSnapshot(export);
        string repeated = FormatExportSnapshot(repeatedExport);
        const string expected = """
export contract=quantum.train_pose version=1 lead=156.500000 cars=2
definition carCount=2 spacing=7.250000 car=(5.000000,1.600000,2.100000) bogieSpacing=3.600000 wheel=(4,0.480000,0.450000,1.600000)
car[0]
  original ci=0 d=156.500000 frame=d=12.500000 p=(128.834965,34.600184,70.814372) t=(0.976862,0.006174,0.213780) n=(-0.076980,0.942737,0.324533) b=(-0.199535,-0.333481,0.921399) matrix=[0.976862,-0.076980,-0.199535,128.834961|0.006174,0.942737,-0.333481,34.600185|0.213780,0.324533,0.921399,70.814369|0.000000,0.000000,0.000000,1.000000]
  bodyFrontBogie ci=0 bi=0 d=158.300000 frame=d=14.300000 p=(129.827939,34.595859,70.968945) t=(0.995557,-0.014822,0.092983) n=(-0.017099,0.942651,0.333341) b=(-0.092592,-0.333450,0.938210) matrix=[0.995557,-0.017099,-0.092592,129.827942|-0.014822,0.942651,-0.333450,34.595860|0.092983,0.333341,0.938210,70.968948|0.000000,0.000000,0.000000,1.000000]
  bodyRearBogie ci=0 bi=1 d=154.700000 frame=d=10.700000 p=(127.844191,34.583082,70.533283) t=(0.943671,0.026751,0.329801) n=(-0.133831,0.942417,0.306495) b=(-0.302611,-0.333368,0.892912) matrix=[0.943671,-0.133831,-0.302611,127.844193|0.026751,0.942417,-0.333368,34.583080|0.329801,0.306495,0.892912,70.533279|0.000000,0.000000,0.000000,1.000000]
  articulated center=156.500000 frame=d=156.500000 p=(128.836065,34.589470,70.751114) t=(0.976704,0.006291,0.214499) n=(-0.076427,0.944224,0.320312) b=(-0.200520,-0.329243,0.922708) matrix=[0.976704,-0.076427,-0.200520,128.836060|0.006291,0.944224,-0.329243,34.589470|0.214499,0.320312,0.922708,70.751114|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=0 bi=0 d=158.300000 frame=d=14.300000 p=(129.827939,34.595859,70.968945) t=(0.995557,-0.014822,0.092983) n=(-0.017099,0.942651,0.333341) b=(-0.092592,-0.333450,0.938210) matrix=[0.995557,-0.017099,-0.092592,129.827942|-0.014822,0.942651,-0.333450,34.595860|0.092983,0.333341,0.938210,70.968948|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:ci=0 bi=0 offset=(-0.800000,-0.225000,0.000000)@14.300000;1:ci=0 bi=0 offset=(-0.800000,0.225000,0.000000)@14.300000;2:ci=0 bi=0 offset=(0.800000,-0.225000,0.000000)@14.300000;3:ci=0 bi=0 offset=(0.800000,0.225000,0.000000)@14.300000
  rearBogie ci=0 bi=1 d=154.700000 frame=d=10.700000 p=(127.844191,34.583082,70.533283) t=(0.943671,0.026751,0.329801) n=(-0.133831,0.942417,0.306495) b=(-0.302611,-0.333368,0.892912) matrix=[0.943671,-0.133831,-0.302611,127.844193|0.026751,0.942417,-0.333368,34.583080|0.329801,0.306495,0.892912,70.533279|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:ci=0 bi=1 offset=(-0.800000,-0.225000,0.000000)@10.700000;1:ci=0 bi=1 offset=(-0.800000,0.225000,0.000000)@10.700000;2:ci=0 bi=1 offset=(0.800000,-0.225000,0.000000)@10.700000;3:ci=0 bi=1 offset=(0.800000,0.225000,0.000000)@10.700000
car[1]
  original ci=1 d=149.250000 frame=d=5.250000 p=(124.857762,34.393325,68.863829) t=(0.787158,0.078782,0.611699) n=(-0.263276,0.939824,0.217752) b=(-0.557734,-0.332451,0.760532) matrix=[0.787158,-0.263276,-0.557734,124.857765|0.078782,0.939824,-0.332451,34.393326|0.611699,0.217752,0.760532,68.863831|0.000000,0.000000,0.000000,1.000000]
  bodyFrontBogie ci=1 bi=0 d=151.050000 frame=d=7.050000 p=(125.841875,34.479672,69.555984) t=(0.844178,0.063775,0.532256) n=(-0.228722,0.940836,0.250030) b=(-0.484820,-0.332808,0.808819) matrix=[0.844178,-0.228722,-0.484820,125.841873|0.063775,0.940836,-0.332808,34.479671|0.532256,0.250030,0.808819,69.555984|0.000000,0.000000,0.000000,1.000000]
  bodyRearBogie ci=1 bi=1 d=147.450000 frame=d=3.450000 p=(123.875850,34.282666,68.026661) t=(0.729550,0.091530,0.677775) n=(-0.290201,0.938797,0.185589) b=(-0.619306,-0.332087,0.711462) matrix=[0.729550,-0.290201,-0.619306,123.875847|0.091530,0.938797,-0.332087,34.282665|0.677775,0.185589,0.711462,68.026665|0.000000,0.000000,0.000000,1.000000]
  articulated center=149.250000 frame=d=149.250000 p=(124.858862,34.381169,68.791323) t=(0.786857,0.078847,0.612077) n=(-0.262287,0.940498,0.216030) b=(-0.558624,-0.330525,0.760718) matrix=[0.786857,-0.262287,-0.558624,124.858864|0.078847,0.940498,-0.330525,34.381168|0.612077,0.216030,0.760718,68.791321|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=1 bi=0 d=151.050000 frame=d=7.050000 p=(125.841875,34.479672,69.555984) t=(0.844178,0.063775,0.532256) n=(-0.228722,0.940836,0.250030) b=(-0.484820,-0.332808,0.808819) matrix=[0.844178,-0.228722,-0.484820,125.841873|0.063775,0.940836,-0.332808,34.479671|0.532256,0.250030,0.808819,69.555984|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:ci=1 bi=0 offset=(-0.800000,-0.225000,0.000000)@7.050000;1:ci=1 bi=0 offset=(-0.800000,0.225000,0.000000)@7.050000;2:ci=1 bi=0 offset=(0.800000,-0.225000,0.000000)@7.050000;3:ci=1 bi=0 offset=(0.800000,0.225000,0.000000)@7.050000
  rearBogie ci=1 bi=1 d=147.450000 frame=d=3.450000 p=(123.875850,34.282666,68.026661) t=(0.729550,0.091530,0.677775) n=(-0.290201,0.938797,0.185589) b=(-0.619306,-0.332087,0.711462) matrix=[0.729550,-0.290201,-0.619306,123.875847|0.091530,0.938797,-0.332087,34.282665|0.677775,0.185589,0.711462,68.026665|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:ci=1 bi=1 offset=(-0.800000,-0.225000,0.000000)@3.450000;1:ci=1 bi=1 offset=(-0.800000,0.225000,0.000000)@3.450000;2:ci=1 bi=1 offset=(0.800000,-0.225000,0.000000)@3.450000;3:ci=1 bi=1 offset=(0.800000,0.225000,0.000000)@3.450000
""";

        AssertSnapshot(expected, actual);
        AssertSnapshot(actual, repeated);

        string json = TrainPoseExportV1Json.Serialize(export);
        TrainPoseExportV1Dto roundtrip = TrainPoseExportV1Json.Deserialize(json);
        AssertSnapshot(actual, FormatExportSnapshot(roundtrip));

        bool isValid = TrainPoseExportV1Validator.TryValidate(
            export,
            out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid, FormatDiagnostics(diagnostics));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Export_CurvedSyntheticLayout_PreservesContractHierarchy()
    {
        TrainPoseExportV1Dto export = CreateCurvedSyntheticExport();

        Assert.Equal(TrainPoseExportV1Dto.ContractName, export.Contract);
        Assert.Equal(TrainPoseExportV1Dto.ContractVersion, export.Version);
        Assert.Equal(export.Definition.CarCount, export.Cars.Length);
        Assert.NotNull(export.Definition.WheelLayout);

        for (int i = 0; i < export.Cars.Length; i++)
        {
            ArticulatedTrainCarWithWheelsV1Dto car = export.Cars[i];

            AssertBogieMatches(car.Body.FrontBogie, car.FrontBogie.Bogie);
            AssertBogieMatches(car.Body.RearBogie, car.RearBogie.Bogie);
            Assert.Equal(export.Definition.WheelLayout!.WheelCountPerBogie, car.FrontBogie.Wheels.Length);
            Assert.Equal(export.Definition.WheelLayout.WheelCountPerBogie, car.RearBogie.Wheels.Length);
            AssertMatrixBottomRowCanonical(car.Body.OriginalBody.Matrix);
            AssertMatrixBottomRowCanonical(car.Body.ArticulatedMatrix);
            AssertMatrixBottomRowCanonical(car.FrontBogie.Bogie.Matrix);
            AssertMatrixBottomRowCanonical(car.RearBogie.Bogie.Matrix);

            AssertWheelsMatchBogie(car.FrontBogie);
            AssertWheelsMatchBogie(car.RearBogie);
        }
    }

    private static TrainPoseExportV1Dto CreateCurvedSyntheticExport()
    {
        TrackDocument document = BuildCurvedSyntheticTrack();
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = new(
            carCount: 2,
            carSpacing: 7.25,
            carLength: 5.0,
            carWidth: 1.6,
            carHeight: 2.1,
            bogieSpacing: 3.6,
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: 4,
                wheelRadius: 0.48,
                wheelWidth: 0.45,
                axleSpacing: 1.6));

        TrainPoseResult pose = provider.EvaluateTrainPose(
            leadDistance: 156.5,
            definition: definition);

        return TrainPoseExportV1Mapper.Export(pose);
    }

    private static TrackDocument BuildCurvedSyntheticTrack()
    {
        TrackSegment[] segments =
        {
            new StraightSegment(
                length: 52.0,
                id: "m22-s0",
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(52.0, 6.0, 0.0)),
                rollRadians: 0.0),
            new CurvedSegment(
                length: 92.0,
                id: "m22-c1",
                spline: new CubicBezierCurve(
                    new Vector3d(52.0, 6.0, 0.0),
                    new Vector3d(80.0, 10.0, 3.0),
                    new Vector3d(105.0, 31.0, 45.0),
                    new Vector3d(122.0, 34.0, 66.0)),
                rollRadians: 0.18),
            new CurvedSegment(
                length: 94.0,
                id: "m22-c2",
                spline: new CubicBezierCurve(
                    new Vector3d(122.0, 34.0, 66.0),
                    new Vector3d(139.0, 37.0, 87.0),
                    new Vector3d(157.0, 28.0, 36.0),
                    new Vector3d(176.0, 24.0, 22.0)),
                rollRadians: 0.34),
            new CurvedSegment(
                length: 76.0,
                id: "m22-c3",
                spline: new CubicBezierCurve(
                    new Vector3d(176.0, 24.0, 22.0),
                    new Vector3d(195.0, 20.0, 8.0),
                    new Vector3d(220.0, 12.0, -8.0),
                    new Vector3d(244.0, 10.0, -6.0)),
                rollRadians: 0.16),
            new StraightSegment(
                length: 54.0,
                id: "m22-s4",
                spline: new LineCurve(
                    new Vector3d(244.0, 10.0, -6.0),
                    new Vector3d(298.0, 8.0, -6.0)),
                rollRadians: 0.06)
        };

        return new TrackDocument(segments);
    }

    private static string FormatExportSnapshot(TrainPoseExportV1Dto export)
    {
        var builder = new StringBuilder();
        TrainConsistDefinitionV1Dto definition = export.Definition;
        TrainWheelLayoutV1Dto wheelLayout = definition.WheelLayout!;

        builder.Append("export contract=").Append(export.Contract)
            .Append(" version=").Append(export.Version)
            .Append(" lead=").Append(F(export.LeadDistance))
            .Append(" cars=").Append(export.Cars.Length)
            .AppendLine();
        builder.Append("definition carCount=").Append(definition.CarCount)
            .Append(" spacing=").Append(F(definition.CarSpacing))
            .Append(" car=(").Append(F(definition.CarGeometry.Length)).Append(',')
            .Append(F(definition.CarGeometry.Width)).Append(',')
            .Append(F(definition.CarGeometry.Height)).Append(')')
            .Append(" bogieSpacing=").Append(F(definition.BogieLayout.BogieSpacing))
            .Append(" wheel=(").Append(wheelLayout.WheelCountPerBogie).Append(',')
            .Append(F(wheelLayout.WheelRadius)).Append(',')
            .Append(F(wheelLayout.WheelWidth)).Append(',')
            .Append(F(wheelLayout.AxleSpacing)).Append(')')
            .AppendLine();

        for (int i = 0; i < export.Cars.Length; i++)
        {
            ArticulatedTrainCarWithWheelsV1Dto car = export.Cars[i];

            builder.Append("car[").Append(i).Append(']').AppendLine();
            AppendTrainCar(builder, "original", car.Body.OriginalBody);
            AppendBogie(builder, "bodyFrontBogie", car.Body.FrontBogie);
            AppendBogie(builder, "bodyRearBogie", car.Body.RearBogie);
            AppendArticulatedBody(builder, car.Body);
            AppendBogieWithWheels(builder, "front", car.FrontBogie);
            AppendBogieWithWheels(builder, "rear", car.RearBogie);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendTrainCar(StringBuilder builder, string label, TrainCarTransformV1Dto body)
    {
        builder.Append("  ").Append(label)
            .Append(" ci=").Append(body.CarIndex)
            .Append(" d=").Append(F(body.Distance))
            .Append(" frame=").Append(FormatFrame(body.Frame))
            .Append(" matrix=").Append(FormatMatrix(body.Matrix))
            .AppendLine();
    }

    private static void AppendArticulatedBody(StringBuilder builder, ArticulatedTrainCarV1Dto body)
    {
        builder.Append("  articulated center=").Append(F(body.CenterDistance))
            .Append(" frame=").Append(FormatFrame(body.ArticulatedFrame))
            .Append(" matrix=").Append(FormatMatrix(body.ArticulatedMatrix))
            .AppendLine();
    }

    private static void AppendBogieWithWheels(
        StringBuilder builder,
        string label,
        TrainBogieWithWheelsV1Dto bogieWithWheels)
    {
        AppendBogie(builder, label + "Bogie", bogieWithWheels.Bogie);

        builder.Append("  ").Append(label).Append("Wheels");
        for (int i = 0; i < bogieWithWheels.Wheels.Length; i++)
        {
            WheelTransformV1Dto wheel = bogieWithWheels.Wheels[i];

            builder.Append(i == 0 ? " " : ";")
                .Append(wheel.WheelIndex)
                .Append(":ci=").Append(wheel.CarIndex)
                .Append(" bi=").Append(wheel.BogieIndex)
                .Append(" offset=(").Append(F(wheel.LocalOffsetX)).Append(',')
                .Append(F(wheel.LocalOffsetY)).Append(',')
                .Append(F(wheel.LocalOffsetZ)).Append(')')
                .Append('@').Append(F(wheel.Frame.Distance));
        }

        builder.AppendLine();
    }

    private static void AppendBogie(StringBuilder builder, string label, BogieTransformV1Dto bogie)
    {
        builder.Append("  ").Append(label)
            .Append(" ci=").Append(bogie.CarIndex)
            .Append(" bi=").Append(bogie.BogieIndex)
            .Append(" d=").Append(F(bogie.Distance))
            .Append(" frame=").Append(FormatFrame(bogie.Frame))
            .Append(" matrix=").Append(FormatMatrix(bogie.Matrix))
            .AppendLine();
    }

    private static string FormatFrame(TrackFrameV1Dto frame)
    {
        return "d=" + F(frame.Distance) +
            " p=" + FormatVector(frame.Position) +
            " t=" + FormatVector(frame.Tangent) +
            " n=" + FormatVector(frame.Normal) +
            " b=" + FormatVector(frame.Binormal);
    }

    private static string FormatVector(Vector3dV1Dto vector)
    {
        return "(" + F(vector.X) + "," + F(vector.Y) + "," + F(vector.Z) + ")";
    }

    private static string FormatMatrix(Matrix4x4V1Dto matrix)
    {
        return "[" +
            F(matrix.M11) + "," + F(matrix.M12) + "," + F(matrix.M13) + "," + F(matrix.M14) + "|" +
            F(matrix.M21) + "," + F(matrix.M22) + "," + F(matrix.M23) + "," + F(matrix.M24) + "|" +
            F(matrix.M31) + "," + F(matrix.M32) + "," + F(matrix.M33) + "," + F(matrix.M34) + "|" +
            F(matrix.M41) + "," + F(matrix.M42) + "," + F(matrix.M43) + "," + F(matrix.M44) + "]";
    }

    private static void AssertBogieMatches(BogieTransformV1Dto expected, BogieTransformV1Dto actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        Assert.Equal(expected.Distance, actual.Distance);
        AssertFrameMatches(expected.Frame, actual.Frame);
        AssertMatrixMatches(expected.Matrix, actual.Matrix);
    }

    private static void AssertWheelsMatchBogie(TrainBogieWithWheelsV1Dto bogieWithWheels)
    {
        for (int i = 0; i < bogieWithWheels.Wheels.Length; i++)
        {
            WheelTransformV1Dto wheel = bogieWithWheels.Wheels[i];

            Assert.Equal(bogieWithWheels.Bogie.CarIndex, wheel.CarIndex);
            Assert.Equal(bogieWithWheels.Bogie.BogieIndex, wheel.BogieIndex);
            Assert.Equal(i, wheel.WheelIndex);
            AssertFrameMatches(bogieWithWheels.Bogie.Frame, wheel.Frame);
            AssertMatrixMatches(bogieWithWheels.Bogie.Matrix, wheel.Matrix);
            AssertMatrixBottomRowCanonical(wheel.Matrix);
        }
    }

    private static void AssertFrameMatches(TrackFrameV1Dto expected, TrackFrameV1Dto actual)
    {
        Assert.Equal(expected.Distance, actual.Distance);
        AssertVectorMatches(expected.Position, actual.Position);
        AssertVectorMatches(expected.Tangent, actual.Tangent);
        AssertVectorMatches(expected.Normal, actual.Normal);
        AssertVectorMatches(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorMatches(Vector3dV1Dto expected, Vector3dV1Dto actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Z, actual.Z);
    }

    private static void AssertMatrixMatches(Matrix4x4V1Dto expected, Matrix4x4V1Dto actual)
    {
        Assert.Equal(expected.M11, actual.M11);
        Assert.Equal(expected.M12, actual.M12);
        Assert.Equal(expected.M13, actual.M13);
        Assert.Equal(expected.M14, actual.M14);
        Assert.Equal(expected.M21, actual.M21);
        Assert.Equal(expected.M22, actual.M22);
        Assert.Equal(expected.M23, actual.M23);
        Assert.Equal(expected.M24, actual.M24);
        Assert.Equal(expected.M31, actual.M31);
        Assert.Equal(expected.M32, actual.M32);
        Assert.Equal(expected.M33, actual.M33);
        Assert.Equal(expected.M34, actual.M34);
        Assert.Equal(expected.M41, actual.M41);
        Assert.Equal(expected.M42, actual.M42);
        Assert.Equal(expected.M43, actual.M43);
        Assert.Equal(expected.M44, actual.M44);
    }

    private static void AssertMatrixBottomRowCanonical(Matrix4x4V1Dto matrix)
    {
        Assert.Equal(0.0, matrix.M41);
        Assert.Equal(0.0, matrix.M42);
        Assert.Equal(0.0, matrix.M43);
        Assert.Equal(1.0, matrix.M44);
    }

    private static void AssertSnapshot(string expected, string actual)
    {
        string normalizedExpected = NormalizeSnapshot(expected);
        string normalizedActual = NormalizeSnapshot(actual);

        if (!string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
        {
            Assert.Fail("Snapshot mismatch. Actual snapshot:" + Environment.NewLine + normalizedActual);
        }
    }

    private static string NormalizeSnapshot(string value)
    {
        return value.ReplaceLineEndings("\n").Trim();
    }

    private static string FormatDiagnostics(IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic => $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
    }

    private static string F(double value)
    {
        return value.ToString("0.000000", CultureInfo.InvariantCulture);
    }
}
