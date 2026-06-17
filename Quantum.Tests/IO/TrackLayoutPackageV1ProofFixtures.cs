using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackLayoutPackageV1ProofFixture
{
    public TrackLayoutPackageV1ProofFixture(
        string name,
        string goldenFileName,
        TrackAuthoringDefinition sourceDefinition)
    {
        Name = name;
        GoldenFileName = goldenFileName;
        SourceDefinition = sourceDefinition;
        ParityStations = TrackLayoutPackageV1ProofFixtures.BuildParityStations(sourceDefinition);
    }

    public string Name { get; }

    public string GoldenFileName { get; }

    public TrackAuthoringDefinition SourceDefinition { get; }

    public IReadOnlyList<double> ParityStations { get; }

    public override string ToString()
    {
        return Name;
    }
}

public static class TrackLayoutPackageV1ProofFixtures
{
    public const string PlanarName = "planar";
    public const string SpatialName = "spatial";
    public const string ExplicitBankedName = "explicit-banked";
    public const string FullFeatureName = "full-feature";

    public const double SharedTrainLeadDistance = 24.0;

    public static IEnumerable<object[]> FixtureCases()
    {
        return All().Select(fixture => new object[] { fixture });
    }

    public static IReadOnlyList<TrackLayoutPackageV1ProofFixture> All()
    {
        return new[]
        {
            CreatePlanar(),
            CreateSpatial(),
            CreateExplicitBanked(),
            CreateFullFeature()
        };
    }

    public static TrainConsistDefinition CreateSharedTrainConsist()
    {
        return new TrainConsistDefinition(
            carCount: 3,
            carSpacing: 5.5,
            carLength: 4.2,
            carWidth: 1.6,
            carHeight: 2.0,
            bogieSpacing: 3.2,
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: 4,
                wheelRadius: 0.42,
                wheelWidth: 0.32,
                axleSpacing: 1.1));
    }

    public static IReadOnlyList<double> BuildParityStations(
        TrackAuthoringDefinition definition)
    {
        var stations = new List<double> { 0.0 };
        double cursor = 0.0;

        for (int i = 0; i < definition.Sections.Count; i++)
        {
            GeometricSectionDefinition section = definition.Sections[i];
            AddUnique(stations, cursor + (section.Length * 0.5));
            cursor += section.Length;
            AddUnique(stations, cursor);
        }

        AddUnique(stations, cursor);
        return stations;
    }

    private static TrackLayoutPackageV1ProofFixture CreatePlanar()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("planar-entry", 12.0),
            new ConstantCurvatureSectionDefinition("planar-arc", 14.0, 28.0),
            new CurvatureTransitionSectionDefinition(
                "planar-transition",
                10.0,
                startCurvature: 1.0 / 28.0,
                endCurvature: 0.0)
        });

        return CreateFixture(PlanarName, definition);
    }

    private static TrackLayoutPackageV1ProofFixture CreateSpatial()
    {
        SpatialSectionDefinition rise = CreateSpatialSection(
            "spatial-rise-turn",
            new[]
            {
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(3.0, 0.0, 0.0),
                new Vector3d(6.0, 1.8, 0.9),
                new Vector3d(10.0, 3.4, 2.4),
                new Vector3d(14.0, 2.8, 3.2)
            },
            new[] { 1.0, 0.95, 1.1, 0.9, 1.0 });
        SpatialSectionDefinition drop = CreateSpatialSection(
            "spatial-drop-turn",
            new[]
            {
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(2.5, 0.0, 0.0),
                new Vector3d(5.5, -1.6, -0.7),
                new Vector3d(9.0, -3.0, -2.0),
                new Vector3d(12.0, -2.2, -3.0)
            },
            new[] { 1.0, 1.05, 0.92, 1.15, 1.0 });

        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("spatial-entry", 8.0),
            rise,
            new ConstantCurvatureSectionDefinition("spatial-arc", 12.0, 24.0),
            drop,
            new StraightSectionDefinition("spatial-exit", 8.0)
        });

        return CreateFixture(SpatialName, definition);
    }

    private static TrackLayoutPackageV1ProofFixture CreateExplicitBanked()
    {
        GeometricSectionDefinition[] sections =
        {
            new StraightSectionDefinition("banked-entry", 10.0),
            new ConstantCurvatureSectionDefinition("banked-arc", 12.0, 30.0),
            new CurvatureTransitionSectionDefinition(
                "banked-transition",
                8.0,
                startCurvature: 1.0 / 30.0,
                endCurvature: 0.0),
            new StraightSectionDefinition("banked-exit", 6.0)
        };
        double totalLength = sections.Sum(section => section.Length);
        var banking = new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(10.0, 0.25, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(22.0, -0.15, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(30.0, 0.4, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(totalLength, 0.1, BankingProfileInterpolationMode.Constant)
        });
        var definition = new TrackAuthoringDefinition(
            sections,
            TrackStartPose.Identity,
            banking);

        return CreateFixture(ExplicitBankedName, definition);
    }

    private static TrackLayoutPackageV1ProofFixture CreateFullFeature()
    {
        double inverseSqrtTwo = 1.0 / System.Math.Sqrt(2.0);
        var startPose = new TrackStartPose(
            new Vector3d(3.0, 2.0, -5.0),
            new Vector3d(inverseSqrtTwo, 0.0, inverseSqrtTwo),
            Vector3d.UnitY,
            new Vector3d(-inverseSqrtTwo, 0.0, inverseSqrtTwo));
        SpatialSectionDefinition spatial = CreateSpatialSection(
            "full-weighted-spatial",
            new[]
            {
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(2.8, 0.0, 0.0),
                new Vector3d(5.6, -1.2, 1.4),
                new Vector3d(9.2, -2.6, 3.0),
                new Vector3d(13.0, -1.8, 1.2)
            },
            new[] { 1.0, 0.85, 1.2, 0.95, 1.1 },
            rollRadians: 0.12);
        GeometricSectionDefinition[] sections =
        {
            new StraightSectionDefinition("full-entry", 9.0, rollRadians: 0.05),
            new ConstantCurvatureSectionDefinition("full-negative-arc", 12.0, -22.0, rollRadians: -0.08),
            new CurvatureTransitionSectionDefinition(
                "full-signed-transition",
                7.0,
                startCurvature: -0.025,
                endCurvature: 0.015,
                rollRadians: 0.03),
            spatial,
            new ConstantCurvatureSectionDefinition("full-positive-arc", 10.0, 18.0, rollRadians: 0.18),
            new StraightSectionDefinition("full-exit", 7.0, rollRadians: -0.04)
        };
        double firstBoundary = sections[0].Length;
        double secondBoundary = firstBoundary + sections[1].Length;
        double fourthBoundary = secondBoundary + sections[2].Length + sections[3].Length;
        double totalLength = sections.Sum(section => section.Length);
        var banking = new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.02, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(firstBoundary, -0.18, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(secondBoundary, 0.31, BankingProfileInterpolationMode.Constant),
            new BankingProfileKey(fourthBoundary, 0.31, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(totalLength, -0.12, BankingProfileInterpolationMode.Constant)
        });
        var definition = new TrackAuthoringDefinition(sections, startPose, banking);

        return CreateFixture(FullFeatureName, definition);
    }

    private static TrackLayoutPackageV1ProofFixture CreateFixture(
        string name,
        TrackAuthoringDefinition definition)
    {
        return new TrackLayoutPackageV1ProofFixture(
            name,
            "TrackLayoutPackageV1." + name + ".golden.json",
            definition);
    }

    private static SpatialSectionDefinition CreateSpatialSection(
        string id,
        IReadOnlyList<Vector3d> controlPoints,
        IReadOnlyList<double> weights,
        int degree = 3,
        double rollRadians = 0.0)
    {
        double length = MeasureNurbsLength(controlPoints, weights, degree);
        return new SpatialSectionDefinition(
            id,
            length,
            controlPoints,
            degree,
            weights,
            rollRadians);
    }

    private static double MeasureNurbsLength(
        IReadOnlyList<Vector3d> controlPoints,
        IReadOnlyList<double> weights,
        int degree)
    {
        var curve = new ArcLengthCurveAdapter(
            new GSharkNurbsCurveAdapter(
                new List<Vector3d>(controlPoints),
                new List<double>(weights),
                degree),
            TrackSamplingOptions.DefaultArcLengthSamples,
            TrackSamplingOptions.DefaultArcLengthTolerance);
        return curve.Length;
    }

    private static void AddUnique(ICollection<double> stations, double station)
    {
        if (!stations.Contains(station))
        {
            stations.Add(station);
        }
    }
}
