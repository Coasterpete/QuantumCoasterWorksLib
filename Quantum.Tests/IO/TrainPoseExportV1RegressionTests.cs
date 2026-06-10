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
  original ci=0 d=156.500000 frame=d=156.500000 p=(123.036263,34.167967,67.190510) t=(0.681539,0.100785,0.724808) n=(-0.443646,0.844598,0.299720) b=(-0.581965,-0.525829,0.620339) matrix=[0.681539,-0.443646,-0.581965,123.036263|0.100785,0.844598,-0.525829,34.167965|0.724808,0.299720,0.620339,67.190514|0.000000,0.000000,0.000000,1.000000]
  bodyFrontBogie ci=0 bi=0 d=158.300000 frame=d=158.300000 p=(124.328371,34.336761,68.431019) t=(0.756015,0.085924,0.648890) n=(-0.394378,0.850997,0.346799) b=(-0.522405,-0.518093,0.677253) matrix=[0.756015,-0.394378,-0.522405,124.328369|0.085924,0.850997,-0.518093,34.336761|0.648890,0.346799,0.677253,68.431023|0.000000,0.000000,0.000000,1.000000]
  bodyRearBogie ci=0 bi=1 d=154.700000 frame=d=154.700000 p=(121.867317,33.976250,65.835874) t=(0.624119,0.113287,0.773073) n=(-0.371871,0.913250,0.166390) b=(-0.687159,-0.391331,0.612105) matrix=[0.624119,-0.371871,-0.687159,121.867317|0.113287,0.913250,-0.391331,33.976250|0.773073,0.166390,0.612105,65.835876|0.000000,0.000000,0.000000,1.000000]
  articulated center=156.500000 frame=d=156.500000 p=(123.097844,34.156506,67.133446) t=(0.684643,0.100291,0.721946) n=(-0.392781,0.885133,0.249526) b=(-0.613993,-0.454403,0.645392) matrix=[0.684643,-0.392781,-0.613993,123.097847|0.100291,0.885133,-0.454403,34.156506|0.721946,0.249526,0.645392,67.133446|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=0 bi=0 d=158.300000 frame=d=158.300000 p=(124.328371,34.336761,68.431019) t=(0.756015,0.085924,0.648890) n=(-0.394378,0.850997,0.346799) b=(-0.522405,-0.518093,0.677253) matrix=[0.756015,-0.394378,-0.522405,124.328369|0.085924,0.850997,-0.518093,34.336761|0.648890,0.346799,0.677253,68.431023|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:ci=0 bi=0 offset=(-0.800000,-0.225000,0.000000)@158.300000;1:ci=0 bi=0 offset=(-0.800000,0.225000,0.000000)@158.300000;2:ci=0 bi=0 offset=(0.800000,-0.225000,0.000000)@158.300000;3:ci=0 bi=0 offset=(0.800000,0.225000,0.000000)@158.300000
  rearBogie ci=0 bi=1 d=154.700000 frame=d=154.700000 p=(121.867317,33.976250,65.835874) t=(0.624119,0.113287,0.773073) n=(-0.371871,0.913250,0.166390) b=(-0.687159,-0.391331,0.612105) matrix=[0.624119,-0.371871,-0.687159,121.867317|0.113287,0.913250,-0.391331,33.976250|0.773073,0.166390,0.612105,65.835876|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:ci=0 bi=1 offset=(-0.800000,-0.225000,0.000000)@154.700000;1:ci=0 bi=1 offset=(-0.800000,0.225000,0.000000)@154.700000;2:ci=0 bi=1 offset=(0.800000,-0.225000,0.000000)@154.700000;3:ci=0 bi=1 offset=(0.800000,0.225000,0.000000)@154.700000
car[1]
  original ci=1 d=149.250000 frame=d=149.250000 p=(118.538807,33.183130,61.595138) t=(0.599435,0.173493,0.781395) n=(-0.411993,0.903853,0.115372) b=(-0.686250,-0.391088,0.613279) matrix=[0.599434,-0.411993,-0.686251,118.538803|0.173493,0.903853,-0.391088,33.183128|0.781395,0.115372,0.613279,61.595139|0.000000,0.000000,0.000000,1.000000]
  bodyFrontBogie ci=1 bi=0 d=151.050000 frame=d=151.050000 p=(119.623737,33.480315,63.000305) t=(0.606242,0.156340,0.779762) n=(-0.400842,0.906904,0.129812) b=(-0.686874,-0.391259,0.612471) matrix=[0.606242,-0.400842,-0.686874,119.623734|0.156340,0.906904,-0.391259,33.480316|0.779762,0.129812,0.612471,63.000305|0.000000,0.000000,0.000000,1.000000]
  bodyRearBogie ci=1 bi=1 d=147.450000 frame=d=147.450000 p=(117.465121,32.856969,60.187744) t=(0.593718,0.188598,0.782260) n=(-0.421603,0.900937,0.102777) b=(-0.685383,-0.390824,0.614415) matrix=[0.593718,-0.421603,-0.685383,117.465118|0.188598,0.900937,-0.390824,32.856968|0.782260,0.102777,0.614415,60.187744|0.000000,0.000000,0.000000,1.000000]
  articulated center=149.250000 frame=d=149.250000 p=(118.544429,33.168642,61.594024) t=(0.599646,0.173161,0.781307) n=(-0.411762,0.903918,0.115689) b=(-0.686204,-0.391085,0.613332) matrix=[0.599646,-0.411762,-0.686204,118.544426|0.173161,0.903918,-0.391085,33.168644|0.781307,0.115689,0.613332,61.594025|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=1 bi=0 d=151.050000 frame=d=151.050000 p=(119.623737,33.480315,63.000305) t=(0.606242,0.156340,0.779762) n=(-0.400842,0.906904,0.129812) b=(-0.686874,-0.391259,0.612471) matrix=[0.606242,-0.400842,-0.686874,119.623734|0.156340,0.906904,-0.391259,33.480316|0.779762,0.129812,0.612471,63.000305|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:ci=1 bi=0 offset=(-0.800000,-0.225000,0.000000)@151.050000;1:ci=1 bi=0 offset=(-0.800000,0.225000,0.000000)@151.050000;2:ci=1 bi=0 offset=(0.800000,-0.225000,0.000000)@151.050000;3:ci=1 bi=0 offset=(0.800000,0.225000,0.000000)@151.050000
  rearBogie ci=1 bi=1 d=147.450000 frame=d=147.450000 p=(117.465121,32.856969,60.187744) t=(0.593718,0.188598,0.782260) n=(-0.421603,0.900937,0.102777) b=(-0.685383,-0.390824,0.614415) matrix=[0.593718,-0.421603,-0.685383,117.465118|0.188598,0.900937,-0.390824,32.856968|0.782260,0.102777,0.614415,60.187744|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:ci=1 bi=1 offset=(-0.800000,-0.225000,0.000000)@147.450000;1:ci=1 bi=1 offset=(-0.800000,0.225000,0.000000)@147.450000;2:ci=1 bi=1 offset=(0.800000,-0.225000,0.000000)@147.450000;3:ci=1 bi=1 offset=(0.800000,0.225000,0.000000)@147.450000
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
                length: 52.3450093132096,
                id: "m22-s0",
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(52.0, 6.0, 0.0)),
                rollRadians: 0.0),
            new CurvedSegment(
                length: 102.5673744475657,
                id: "m22-c1",
                spline: new CubicBezierCurve(
                    new Vector3d(52.0, 6.0, 0.0),
                    new Vector3d(80.0, 10.0, 3.0),
                    new Vector3d(105.0, 31.0, 45.0),
                    new Vector3d(122.0, 34.0, 66.0)),
                rollRadians: 0.18),
            new CurvedSegment(
                length: 79.23453083610931,
                id: "m22-c2",
                spline: new CubicBezierCurve(
                    new Vector3d(122.0, 34.0, 66.0),
                    new Vector3d(139.0, 37.0, 87.0),
                    new Vector3d(157.0, 28.0, 36.0),
                    new Vector3d(176.0, 24.0, 22.0)),
                rollRadians: 0.34),
            new CurvedSegment(
                length: 76.41747274859141,
                id: "m22-c3",
                spline: new CubicBezierCurve(
                    new Vector3d(176.0, 24.0, 22.0),
                    new Vector3d(195.0, 20.0, 8.0),
                    new Vector3d(220.0, 12.0, -8.0),
                    new Vector3d(244.0, 10.0, -6.0)),
                rollRadians: 0.16),
            new StraightSegment(
                length: 54.037024344425184,
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
