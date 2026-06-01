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
  body ci=0 d=156.500000 frame=d=156.500000 p=(128.834965,34.600184,70.814372) t=(0.976862,0.006174,0.213780) n=(-0.076980,0.942737,0.324533) b=(-0.199535,-0.333481,0.921399) matrix=[0.976862,-0.076980,-0.199535,128.834961|0.006174,0.942737,-0.333481,34.600185|0.213780,0.324533,0.921399,70.814369|0.000000,0.000000,0.000000,1.000000]
  articulated center=156.500000 frame=d=156.500000 p=(128.836065,34.589470,70.751114) t=(0.976704,0.006291,0.214499) n=(-0.076427,0.944224,0.320312) b=(-0.200520,-0.329243,0.922708) matrix=[0.976704,-0.076427,-0.200520,128.836060|0.006291,0.944224,-0.329243,34.589470|0.214499,0.320312,0.922708,70.751114|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=0 bi=0 d=158.300000 frame=d=158.300000 p=(129.827939,34.595859,70.968945) t=(0.995557,-0.014822,0.092983) n=(-0.017099,0.942651,0.333341) b=(-0.092592,-0.333450,0.938210) matrix=[0.995557,-0.017099,-0.092592,129.827942|-0.014822,0.942651,-0.333450,34.595860|0.092983,0.333341,0.938210,70.968948|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:offset=(-0.800000,-0.225000,0.000000)@158.300000;1:offset=(-0.800000,0.225000,0.000000)@158.300000;2:offset=(0.800000,-0.225000,0.000000)@158.300000;3:offset=(0.800000,0.225000,0.000000)@158.300000
  rearBogie ci=0 bi=1 d=154.700000 frame=d=154.700000 p=(127.844191,34.583082,70.533283) t=(0.943671,0.026751,0.329801) n=(-0.133831,0.942417,0.306495) b=(-0.302611,-0.333368,0.892912) matrix=[0.943671,-0.133831,-0.302611,127.844193|0.026751,0.942417,-0.333368,34.583080|0.329801,0.306495,0.892912,70.533279|0.000000,0.000000,0.000000,1.000000]
  rearWheels 0:offset=(-0.800000,-0.225000,0.000000)@154.700000;1:offset=(-0.800000,0.225000,0.000000)@154.700000;2:offset=(0.800000,-0.225000,0.000000)@154.700000;3:offset=(0.800000,0.225000,0.000000)@154.700000
car[1]
  body ci=1 d=149.250000 frame=d=149.250000 p=(124.857762,34.393325,68.863829) t=(0.787158,0.078782,0.611699) n=(-0.263276,0.939824,0.217752) b=(-0.557734,-0.332451,0.760532) matrix=[0.787158,-0.263276,-0.557734,124.857765|0.078782,0.939824,-0.332451,34.393326|0.611699,0.217752,0.760532,68.863831|0.000000,0.000000,0.000000,1.000000]
  articulated center=149.250000 frame=d=149.250000 p=(124.858862,34.381169,68.791323) t=(0.786857,0.078847,0.612077) n=(-0.262287,0.940498,0.216030) b=(-0.558624,-0.330525,0.760718) matrix=[0.786857,-0.262287,-0.558624,124.858864|0.078847,0.940498,-0.330525,34.381168|0.612077,0.216030,0.760718,68.791321|0.000000,0.000000,0.000000,1.000000]
  frontBogie ci=1 bi=0 d=151.050000 frame=d=151.050000 p=(125.841875,34.479672,69.555984) t=(0.844178,0.063775,0.532256) n=(-0.228722,0.940836,0.250030) b=(-0.484820,-0.332808,0.808819) matrix=[0.844178,-0.228722,-0.484820,125.841873|0.063775,0.940836,-0.332808,34.479671|0.532256,0.250030,0.808819,69.555984|0.000000,0.000000,0.000000,1.000000]
  frontWheels 0:offset=(-0.800000,-0.225000,0.000000)@151.050000;1:offset=(-0.800000,0.225000,0.000000)@151.050000;2:offset=(0.800000,-0.225000,0.000000)@151.050000;3:offset=(0.800000,0.225000,0.000000)@151.050000
  rearBogie ci=1 bi=1 d=147.450000 frame=d=147.450000 p=(123.875850,34.282666,68.026661) t=(0.729550,0.091530,0.677775) n=(-0.290201,0.938797,0.185589) b=(-0.619306,-0.332087,0.711462) matrix=[0.729550,-0.290201,-0.619306,123.875847|0.091530,0.938797,-0.332087,34.282665|0.677775,0.185589,0.711462,68.026665|0.000000,0.000000,0.000000,1.000000]
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
                length: 52.0,
                id: "m21-s0",
                spline: new LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(52.0, 6.0, 0.0)),
                rollRadians: 0.0),
            new CurvedSegment(
                length: 92.0,
                id: "m21-c1",
                spline: new CubicBezierCurve(
                    new Vector3d(52.0, 6.0, 0.0),
                    new Vector3d(80.0, 10.0, 3.0),
                    new Vector3d(105.0, 31.0, 45.0),
                    new Vector3d(122.0, 34.0, 66.0)),
                rollRadians: 0.18),
            new CurvedSegment(
                length: 94.0,
                id: "m21-c2",
                spline: new CubicBezierCurve(
                    new Vector3d(122.0, 34.0, 66.0),
                    new Vector3d(139.0, 37.0, 87.0),
                    new Vector3d(157.0, 28.0, 36.0),
                    new Vector3d(176.0, 24.0, 22.0)),
                rollRadians: 0.34),
            new CurvedSegment(
                length: 76.0,
                id: "m21-c3",
                spline: new CubicBezierCurve(
                    new Vector3d(176.0, 24.0, 22.0),
                    new Vector3d(195.0, 20.0, 8.0),
                    new Vector3d(220.0, 12.0, -8.0),
                    new Vector3d(244.0, 10.0, -6.0)),
                rollRadians: 0.16),
            new StraightSegment(
                length: 54.0,
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
