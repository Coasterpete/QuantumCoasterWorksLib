using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainPoseDeterministicRegressionFixtureTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void EvaluateTrainPose_StraightSyntheticLayout_MatchesKnownSnapshot()
    {
        TrackDocument document = BuildStraightRegressionTrack(length: 32.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildStraightConsistDefinition();

        TrainPoseResult pose = provider.EvaluateTrainPose(
            leadDistance: 18.0,
            definition: definition);

        string actual = FormatPoseSnapshot(pose);
        const string expected = """
pose lead=18.000000 cars=3
definition spacing=5.000000 car=(4.000000,1.500000,2.000000) bogieSpacing=2.000000 wheel=(6,0.450000,0.400000,1.200000)
car[0]
  body ci=0 d=18.000000 frame=d=18.000000 p=(18.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,18.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  articulated center=18.000000 frame=d=18.000000 p=(18.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,18.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=0 bi=0 d=19.000000 frame=d=19.000000 p=(19.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,19.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:offset=(-1.200000,-0.200000,0.000000)@19.000000;1:offset=(-1.200000,0.200000,0.000000)@19.000000;2:offset=(0.000000,-0.200000,0.000000)@19.000000;3:offset=(0.000000,0.200000,0.000000)@19.000000;4:offset=(1.200000,-0.200000,0.000000)@19.000000;5:offset=(1.200000,0.200000,0.000000)@19.000000
  rearBogie ci=0 bi=1 d=17.000000 frame=d=17.000000 p=(17.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,17.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:offset=(-1.200000,-0.200000,0.000000)@17.000000;1:offset=(-1.200000,0.200000,0.000000)@17.000000;2:offset=(0.000000,-0.200000,0.000000)@17.000000;3:offset=(0.000000,0.200000,0.000000)@17.000000;4:offset=(1.200000,-0.200000,0.000000)@17.000000;5:offset=(1.200000,0.200000,0.000000)@17.000000
car[1]
  body ci=1 d=13.000000 frame=d=13.000000 p=(13.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,13.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  articulated center=13.000000 frame=d=13.000000 p=(13.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,13.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=1 bi=0 d=14.000000 frame=d=14.000000 p=(14.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,14.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:offset=(-1.200000,-0.200000,0.000000)@14.000000;1:offset=(-1.200000,0.200000,0.000000)@14.000000;2:offset=(0.000000,-0.200000,0.000000)@14.000000;3:offset=(0.000000,0.200000,0.000000)@14.000000;4:offset=(1.200000,-0.200000,0.000000)@14.000000;5:offset=(1.200000,0.200000,0.000000)@14.000000
  rearBogie ci=1 bi=1 d=12.000000 frame=d=12.000000 p=(12.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,12.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:offset=(-1.200000,-0.200000,0.000000)@12.000000;1:offset=(-1.200000,0.200000,0.000000)@12.000000;2:offset=(0.000000,-0.200000,0.000000)@12.000000;3:offset=(0.000000,0.200000,0.000000)@12.000000;4:offset=(1.200000,-0.200000,0.000000)@12.000000;5:offset=(1.200000,0.200000,0.000000)@12.000000
car[2]
  body ci=2 d=8.000000 frame=d=8.000000 p=(8.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,8.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  articulated center=8.000000 frame=d=8.000000 p=(8.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,8.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=2 bi=0 d=9.000000 frame=d=9.000000 p=(9.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,9.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:offset=(-1.200000,-0.200000,0.000000)@9.000000;1:offset=(-1.200000,0.200000,0.000000)@9.000000;2:offset=(0.000000,-0.200000,0.000000)@9.000000;3:offset=(0.000000,0.200000,0.000000)@9.000000;4:offset=(1.200000,-0.200000,0.000000)@9.000000;5:offset=(1.200000,0.200000,0.000000)@9.000000
  rearBogie ci=2 bi=1 d=7.000000 frame=d=7.000000 p=(7.000000,0.000000,0.000000) t=(1.000000,0.000000,0.000000) n=(0.000000,1.000000,0.000000) b=(0.000000,0.000000,1.000000) matrix=[1.000000,0.000000,0.000000,7.000000|0.000000,1.000000,0.000000,0.000000|0.000000,0.000000,1.000000,0.000000|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:offset=(-1.200000,-0.200000,0.000000)@7.000000;1:offset=(-1.200000,0.200000,0.000000)@7.000000;2:offset=(0.000000,-0.200000,0.000000)@7.000000;3:offset=(0.000000,0.200000,0.000000)@7.000000;4:offset=(1.200000,-0.200000,0.000000)@7.000000;5:offset=(1.200000,0.200000,0.000000)@7.000000
""";

        AssertSnapshot(expected, actual);
        AssertPoseHierarchyConsistent(pose);
    }

    [Fact]
    public void EvaluateTrainPose_CurvedSyntheticLayout_MatchesKnownRepresentativeSnapshot()
    {
        TrackDocument document = BuildCurvedRegressionTrack();
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildCurvedConsistDefinition();

        TrainPoseResult pose = provider.EvaluateTrainPose(
            leadDistance: 156.5,
            definition: definition);
        TrainPoseResult repeatedPose = provider.EvaluateTrainPose(
            leadDistance: 156.5,
            definition: definition);

        string actual = FormatPoseSnapshot(pose);
        string repeated = FormatPoseSnapshot(repeatedPose);
        const string expected = """
pose lead=156.500000 cars=2
definition spacing=7.250000 car=(5.000000,1.600000,2.100000) bogieSpacing=3.600000 wheel=(4,0.480000,0.450000,1.600000)
car[0]
  body ci=0 d=156.500000 frame=d=156.500000 p=(123.036263,34.167967,67.190510) t=(0.681539,0.100785,0.724808) n=(-0.443646,0.844598,0.299720) b=(-0.581965,-0.525829,0.620339) matrix=[0.681539,-0.443646,-0.581965,123.036263|0.100785,0.844598,-0.525829,34.167965|0.724808,0.299720,0.620339,67.190514|0.000000,0.000000,0.000000,1.000000]
  articulated center=156.500000 frame=d=156.500000 p=(123.097844,34.156506,67.133446) t=(0.684643,0.100291,0.721946) n=(-0.392781,0.885133,0.249526) b=(-0.613993,-0.454403,0.645392) matrix=[0.684643,-0.392781,-0.613993,123.097847|0.100291,0.885133,-0.454403,34.156506|0.721946,0.249526,0.645392,67.133446|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=0 bi=0 d=158.300000 frame=d=158.300000 p=(124.328371,34.336761,68.431019) t=(0.756015,0.085924,0.648890) n=(-0.394378,0.850997,0.346799) b=(-0.522405,-0.518093,0.677253) matrix=[0.756015,-0.394378,-0.522405,124.328369|0.085924,0.850997,-0.518093,34.336761|0.648890,0.346799,0.677253,68.431023|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:offset=(-0.800000,-0.225000,0.000000)@158.300000;1:offset=(-0.800000,0.225000,0.000000)@158.300000;2:offset=(0.800000,-0.225000,0.000000)@158.300000;3:offset=(0.800000,0.225000,0.000000)@158.300000
  rearBogie ci=0 bi=1 d=154.700000 frame=d=154.700000 p=(121.867317,33.976250,65.835874) t=(0.624119,0.113287,0.773073) n=(-0.371871,0.913250,0.166390) b=(-0.687159,-0.391331,0.612105) matrix=[0.624119,-0.371871,-0.687159,121.867317|0.113287,0.913250,-0.391331,33.976250|0.773073,0.166390,0.612105,65.835876|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:offset=(-0.800000,-0.225000,0.000000)@154.700000;1:offset=(-0.800000,0.225000,0.000000)@154.700000;2:offset=(0.800000,-0.225000,0.000000)@154.700000;3:offset=(0.800000,0.225000,0.000000)@154.700000
car[1]
  body ci=1 d=149.250000 frame=d=149.250000 p=(118.538807,33.183130,61.595138) t=(0.599435,0.173493,0.781395) n=(-0.411993,0.903853,0.115372) b=(-0.686250,-0.391088,0.613279) matrix=[0.599434,-0.411993,-0.686251,118.538803|0.173493,0.903853,-0.391088,33.183128|0.781395,0.115372,0.613279,61.595139|0.000000,0.000000,0.000000,1.000000]
  articulated center=149.250000 frame=d=149.250000 p=(118.544429,33.168642,61.594024) t=(0.599646,0.173161,0.781307) n=(-0.411762,0.903918,0.115689) b=(-0.686204,-0.391085,0.613332) matrix=[0.599646,-0.411762,-0.686204,118.544426|0.173161,0.903918,-0.391085,33.168644|0.781307,0.115689,0.613332,61.594025|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=1 bi=0 d=151.050000 frame=d=151.050000 p=(119.623737,33.480315,63.000305) t=(0.606242,0.156340,0.779762) n=(-0.400842,0.906904,0.129812) b=(-0.686874,-0.391259,0.612471) matrix=[0.606242,-0.400842,-0.686874,119.623734|0.156340,0.906904,-0.391259,33.480316|0.779762,0.129812,0.612471,63.000305|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:offset=(-0.800000,-0.225000,0.000000)@151.050000;1:offset=(-0.800000,0.225000,0.000000)@151.050000;2:offset=(0.800000,-0.225000,0.000000)@151.050000;3:offset=(0.800000,0.225000,0.000000)@151.050000
  rearBogie ci=1 bi=1 d=147.450000 frame=d=147.450000 p=(117.465121,32.856969,60.187744) t=(0.593718,0.188598,0.782260) n=(-0.421603,0.900937,0.102777) b=(-0.685383,-0.390824,0.614415) matrix=[0.593718,-0.421603,-0.685383,117.465118|0.188598,0.900937,-0.390824,32.856968|0.782260,0.102777,0.614415,60.187744|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:offset=(-0.800000,-0.225000,0.000000)@147.450000;1:offset=(-0.800000,0.225000,0.000000)@147.450000;2:offset=(0.800000,-0.225000,0.000000)@147.450000;3:offset=(0.800000,0.225000,0.000000)@147.450000
""";

        AssertSnapshot(expected, actual);
        AssertSnapshot(actual, repeated);
        AssertPoseHierarchyConsistent(pose);
    }

    private static TrackDocument BuildStraightRegressionTrack(double length)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: length)
        });
    }

    private static TrackDocument BuildCurvedRegressionTrack()
    {
        TrackSegment[] segments =
        {
            new StraightSegment(
                length: 52.3450093132096,
                id: "m21-s0",
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(52.0, 6.0, 0.0)),
                rollRadians: 0.0),
            new CurvedSegment(
                length: 102.5673744475657,
                id: "m21-c1",
                spline: new CubicBezierCurve(
                    new Vector3d(52.0, 6.0, 0.0),
                    new Vector3d(80.0, 10.0, 3.0),
                    new Vector3d(105.0, 31.0, 45.0),
                    new Vector3d(122.0, 34.0, 66.0)),
                rollRadians: 0.18),
            new CurvedSegment(
                length: 79.23453083610931,
                id: "m21-c2",
                spline: new CubicBezierCurve(
                    new Vector3d(122.0, 34.0, 66.0),
                    new Vector3d(139.0, 37.0, 87.0),
                    new Vector3d(157.0, 28.0, 36.0),
                    new Vector3d(176.0, 24.0, 22.0)),
                rollRadians: 0.34),
            new CurvedSegment(
                length: 76.41747274859141,
                id: "m21-c3",
                spline: new CubicBezierCurve(
                    new Vector3d(176.0, 24.0, 22.0),
                    new Vector3d(195.0, 20.0, 8.0),
                    new Vector3d(220.0, 12.0, -8.0),
                    new Vector3d(244.0, 10.0, -6.0)),
                rollRadians: 0.16),
            new StraightSegment(
                length: 54.037024344425184,
                id: "m21-s4",
                spline: new LineCurve(
                    new Vector3d(244.0, 10.0, -6.0),
                    new Vector3d(298.0, 8.0, -6.0)),
                rollRadians: 0.06)
        };

        return new TrackDocument(segments);
    }

    private static TrainConsistDefinition BuildStraightConsistDefinition()
    {
        return new TrainConsistDefinition(
            carCount: 3,
            carSpacing: 5.0,
            carLength: 4.0,
            carWidth: 1.5,
            carHeight: 2.0,
            bogieSpacing: 2.0,
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: 6,
                wheelRadius: 0.45,
                wheelWidth: 0.4,
                axleSpacing: 1.2));
    }

    private static TrainConsistDefinition BuildCurvedConsistDefinition()
    {
        return new TrainConsistDefinition(
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
    }

    private static string FormatPoseSnapshot(TrainPoseResult pose)
    {
        var builder = new StringBuilder();
        TrainConsistDefinition definition = pose.Definition;
        TrainWheelLayout wheelLayout = definition.WheelLayout!;

        builder.Append("pose lead=").Append(F(pose.LeadDistance))
            .Append(" cars=").Append(pose.CarsReadOnly.Count)
            .AppendLine();
        builder.Append("definition spacing=").Append(F(definition.CarSpacing))
            .Append(" car=(").Append(F(definition.CarLength)).Append(',')
            .Append(F(definition.CarWidth)).Append(',')
            .Append(F(definition.CarHeight)).Append(')')
            .Append(" bogieSpacing=").Append(F(definition.BogieSpacing))
            .Append(" wheel=(").Append(wheelLayout.WheelCountPerBogie).Append(',')
            .Append(F(wheelLayout.WheelRadius)).Append(',')
            .Append(F(wheelLayout.WheelWidth)).Append(',')
            .Append(F(wheelLayout.AxleSpacing)).Append(')')
            .AppendLine();

        for (int i = 0; i < pose.CarsReadOnly.Count; i++)
        {
            ArticulatedTrainCarWithWheelsTransform car = pose.CarsReadOnly[i];

            builder.Append("car[").Append(i).Append(']').AppendLine();
            AppendTrainCar(builder, "body", car.Body.OriginalBody);
            AppendArticulatedBody(builder, car.Body);
            AppendBogieWithWheels(builder, "front", car.FrontBogie);
            AppendBogieWithWheels(builder, "rear", car.RearBogie);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendTrainCar(StringBuilder builder, string label, TrainCarTransform body)
    {
        builder.Append("  ").Append(label)
            .Append(" ci=").Append(body.CarIndex)
            .Append(" d=").Append(F(body.Distance))
            .Append(" frame=").Append(FormatFrame(body.Frame))
            .Append(" matrix=").Append(FormatMatrix(body.Matrix))
            .AppendLine();
    }

    private static void AppendArticulatedBody(StringBuilder builder, ArticulatedTrainCarTransform body)
    {
        builder.Append("  articulated center=").Append(F(body.CenterDistance))
            .Append(" frame=").Append(FormatFrame(body.ArticulatedFrame))
            .Append(" matrix=").Append(FormatMatrix(body.ArticulatedMatrix))
            .AppendLine();
    }

    private static void AppendBogieWithWheels(
        StringBuilder builder,
        string label,
        TrainBogieWithWheelsTransform bogieWithWheels)
    {
        BogieTransform bogie = bogieWithWheels.Bogie;
        builder.Append("  ").Append(label)
            .Append("Bogie ci=").Append(bogie.CarIndex)
            .Append(" bi=").Append(bogie.BogieIndex)
            .Append(" d=").Append(F(bogie.Distance))
            .Append(" frame=").Append(FormatFrame(bogie.Frame))
            .Append(" matrix=").Append(FormatMatrix(bogie.Matrix))
            .AppendLine();

        WheelTransform[] wheels = bogieWithWheels.Wheels;
        builder.Append("  ").Append(label).Append("Wheels");
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelTransform wheel = wheels[i];
            builder.Append(i == 0 ? " " : ";")
                .Append(wheel.WheelIndex)
                .Append(":offset=(").Append(F(wheel.LocalOffsetX)).Append(',')
                .Append(F(wheel.LocalOffsetY)).Append(',')
                .Append(F(wheel.LocalOffsetZ)).Append(")@")
                .Append(F(wheel.Frame.Distance));
        }

        builder.AppendLine();
    }

    private static string FormatFrame(ExportTrackFrame frame)
    {
        return "d=" + F(frame.Distance) +
            " p=" + FormatVector(frame.Position) +
            " t=" + FormatVector(frame.Tangent) +
            " n=" + FormatVector(frame.Normal) +
            " b=" + FormatVector(frame.Binormal);
    }

    private static string FormatVector(Vector3d vector)
    {
        return "(" + F(vector.X) + "," + F(vector.Y) + "," + F(vector.Z) + ")";
    }

    private static string FormatMatrix(Matrix4x4 matrix)
    {
        return "[" +
            F(matrix.M11) + "," + F(matrix.M12) + "," + F(matrix.M13) + "," + F(matrix.M14) + "|" +
            F(matrix.M21) + "," + F(matrix.M22) + "," + F(matrix.M23) + "," + F(matrix.M24) + "|" +
            F(matrix.M31) + "," + F(matrix.M32) + "," + F(matrix.M33) + "," + F(matrix.M34) + "|" +
            F(matrix.M41) + "," + F(matrix.M42) + "," + F(matrix.M43) + "," + F(matrix.M44) + "]";
    }

    private static string FormatMatrix(Matrix4x4d matrix)
    {
        return "[" +
            F(matrix.M11) + "," + F(matrix.M12) + "," + F(matrix.M13) + "," + F(matrix.M14) + "|" +
            F(matrix.M21) + "," + F(matrix.M22) + "," + F(matrix.M23) + "," + F(matrix.M24) + "|" +
            F(matrix.M31) + "," + F(matrix.M32) + "," + F(matrix.M33) + "," + F(matrix.M34) + "|" +
            F(matrix.M41) + "," + F(matrix.M42) + "," + F(matrix.M43) + "," + F(matrix.M44) + "]";
    }

    private static void AssertPoseHierarchyConsistent(TrainPoseResult pose)
    {
        TrainConsistDefinition definition = pose.Definition;
        TrainWheelLayout wheelLayout = definition.WheelLayout!;
        double bogieHalfSpacing = definition.BogieSpacing * 0.5;

        Assert.Equal(definition.CarCount, pose.CarsReadOnly.Count);

        for (int i = 0; i < pose.CarsReadOnly.Count; i++)
        {
            ArticulatedTrainCarWithWheelsTransform car = pose.CarsReadOnly[i];
            TrainCarTransform body = car.Body.OriginalBody;
            double expectedBodyDistance = pose.LeadDistance - (i * definition.CarSpacing);

            Assert.Equal(i, body.CarIndex);
            AssertDoubleNear(expectedBodyDistance, body.Distance);
            AssertMatrixNear(body.Frame.ToMatrix4x4(), body.Matrix);

            AssertDoubleNear(expectedBodyDistance + bogieHalfSpacing, car.FrontBogie.Bogie.Distance);
            AssertDoubleNear(expectedBodyDistance - bogieHalfSpacing, car.RearBogie.Bogie.Distance);
            AssertBogieNear(car.Body.FrontBogie, car.FrontBogie.Bogie);
            AssertBogieNear(car.Body.RearBogie, car.RearBogie.Bogie);

            Vector3d expectedArticulatedCenter = (car.FrontBogie.Bogie.Frame.Position + car.RearBogie.Bogie.Frame.Position) * 0.5;
            AssertDoubleNear(expectedBodyDistance, car.Body.CenterDistance);
            AssertDoubleNear(expectedBodyDistance, car.Body.ArticulatedFrame.Distance);
            AssertVectorNear(expectedArticulatedCenter, car.Body.ArticulatedFrame.Position);
            AssertMatrixNear(Matrix4x4d.FromMatrix4x4(car.Body.ArticulatedFrame.ToMatrix4x4()), car.Body.ArticulatedMatrix);
            AssertTrackFrameOrthonormal(car.Body.ArticulatedFrame);

            AssertWheelsMatchBogieAndLayout(car.FrontBogie, wheelLayout);
            AssertWheelsMatchBogieAndLayout(car.RearBogie, wheelLayout);
        }
    }

    private static void AssertWheelsMatchBogieAndLayout(
        TrainBogieWithWheelsTransform bogieWithWheels,
        TrainWheelLayout wheelLayout)
    {
        WheelTransform[] wheels = bogieWithWheels.Wheels;
        BogieTransform bogie = bogieWithWheels.Bogie;
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
            AssertDoubleNear(expectedLocalOffsetX, wheel.LocalOffsetX);
            AssertDoubleNear(expectedLocalOffsetY, wheel.LocalOffsetY);
            AssertDoubleNear(0.0, wheel.LocalOffsetZ);
            AssertTrackFrameNear(bogie.Frame, wheel.Frame);
            AssertMatrixNear(bogie.Matrix, wheel.Matrix);
        }
    }

    private static void AssertBogieNear(BogieTransform expected, BogieTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertTrackFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertTrackFrameOrthonormal(ExportTrackFrame frame)
    {
        AssertDoubleNear(1.0, frame.Tangent.Length);
        AssertDoubleNear(1.0, frame.Normal.Length);
        AssertDoubleNear(1.0, frame.Binormal.Length);
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Normal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Tangent, frame.Binormal));
        AssertDoubleNear(0.0, Vector3d.Dot(frame.Normal, frame.Binormal));
        AssertVectorNear(Vector3d.Cross(frame.Tangent, frame.Normal), frame.Binormal);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertDoubleNear(expected.M11, actual.M11);
        AssertDoubleNear(expected.M12, actual.M12);
        AssertDoubleNear(expected.M13, actual.M13);
        AssertDoubleNear(expected.M14, actual.M14);
        AssertDoubleNear(expected.M21, actual.M21);
        AssertDoubleNear(expected.M22, actual.M22);
        AssertDoubleNear(expected.M23, actual.M23);
        AssertDoubleNear(expected.M24, actual.M24);
        AssertDoubleNear(expected.M31, actual.M31);
        AssertDoubleNear(expected.M32, actual.M32);
        AssertDoubleNear(expected.M33, actual.M33);
        AssertDoubleNear(expected.M34, actual.M34);
        AssertDoubleNear(expected.M41, actual.M41);
        AssertDoubleNear(expected.M42, actual.M42);
        AssertDoubleNear(expected.M43, actual.M43);
        AssertDoubleNear(expected.M44, actual.M44);
    }

    private static void AssertMatrixNear(Matrix4x4d expected, Matrix4x4d actual)
    {
        AssertDoubleNear(expected.M11, actual.M11);
        AssertDoubleNear(expected.M12, actual.M12);
        AssertDoubleNear(expected.M13, actual.M13);
        AssertDoubleNear(expected.M14, actual.M14);
        AssertDoubleNear(expected.M21, actual.M21);
        AssertDoubleNear(expected.M22, actual.M22);
        AssertDoubleNear(expected.M23, actual.M23);
        AssertDoubleNear(expected.M24, actual.M24);
        AssertDoubleNear(expected.M31, actual.M31);
        AssertDoubleNear(expected.M32, actual.M32);
        AssertDoubleNear(expected.M33, actual.M33);
        AssertDoubleNear(expected.M34, actual.M34);
        AssertDoubleNear(expected.M41, actual.M41);
        AssertDoubleNear(expected.M42, actual.M42);
        AssertDoubleNear(expected.M43, actual.M43);
        AssertDoubleNear(expected.M44, actual.M44);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
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

    private static string F(double value)
    {
        return value.ToString("0.000000", CultureInfo.InvariantCulture);
    }
}
