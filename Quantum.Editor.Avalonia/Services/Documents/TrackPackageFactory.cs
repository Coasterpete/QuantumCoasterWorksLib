using Quantum.IO.TrackLayout.V2;

namespace Quantum.Editor.Avalonia.Services.Documents;

public static class TrackPackageFactory
{
    public static TrackLayoutPackageV2Dto CreateShowcasePackage()
    {
        const double totalLength = 195.0;

        return new TrackLayoutPackageV2Dto
        {
            Metadata = new TrackLayoutMetadataV2Dto
            {
                Units = "meters",
                SourceName = "M156 Showcase Layout",
                LayoutId = "m156-showcase"
            },
            Sections = new[]
            {
                Straight("launch", 30.0),
                Transition("curve-in", 25.0, 0.0, 0.02),
                Arc("sweeper", 35.0, 50.0),
                Transition("reverse-transition", 25.0, 0.02, -0.015),
                Arc("return-curve", 30.0, -66.66666666666667),
                Transition("curve-out", 25.0, -0.015, 0.0),
                Straight("brake-run", 25.0)
            },
            Banking = new TrackBankingV2Dto
            {
                Keys = new[]
                {
                    BankingKey(0.0, 0.0, TrackLayoutPackageV2Vocabulary.BankingInterpolationSmoothStep),
                    BankingKey(45.0, 12.0, TrackLayoutPackageV2Vocabulary.BankingInterpolationCubic),
                    BankingKey(90.0, 34.0, TrackLayoutPackageV2Vocabulary.BankingInterpolationSmoothStep),
                    BankingKey(135.0, -18.0, TrackLayoutPackageV2Vocabulary.BankingInterpolationSinusoidal),
                    BankingKey(totalLength, 0.0, TrackLayoutPackageV2Vocabulary.BankingInterpolationConstant)
                }
            },
            Heartline = new TrackHeartlineV2Dto
            {
                NormalOffset = 1.1,
                LateralOffset = 0.0
            }
        };
    }

    private static TrackLayoutSectionV2Dto Straight(string id, double length)
    {
        return new TrackLayoutSectionV2Dto
        {
            Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
            Id = id,
            Length = length
        };
    }

    private static TrackLayoutSectionV2Dto Arc(string id, double length, double radius)
    {
        return new TrackLayoutSectionV2Dto
        {
            Kind = TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind,
            Id = id,
            Length = length,
            Radius = radius
        };
    }

    private static TrackLayoutSectionV2Dto Transition(
        string id,
        double length,
        double startCurvature,
        double endCurvature)
    {
        return new TrackLayoutSectionV2Dto
        {
            Kind = TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind,
            Id = id,
            Length = length,
            StartCurvature = startCurvature,
            EndCurvature = endCurvature,
            InterpolationMode = TrackLayoutPackageV2Vocabulary.CurvatureInterpolationLinear
        };
    }

    private static TrackBankingKeyV2Dto BankingKey(
        double distance,
        double rollDegrees,
        string interpolation)
    {
        return new TrackBankingKeyV2Dto
        {
            Distance = distance,
            RollRadians = rollDegrees * System.Math.PI / 180.0,
            InterpolationToNext = interpolation
        };
    }
}
