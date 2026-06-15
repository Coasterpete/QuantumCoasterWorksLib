using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoredBankingTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void TrackBankingDefinition_DefensivelyCopiesAndExposesReadOnlyKeys()
    {
        var source = new List<BankingProfileKey>
        {
            new BankingProfileKey(0.0, 0.25, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(10.0, 0.75, BankingProfileInterpolationMode.SmoothStep)
        };

        var banking = new TrackBankingDefinition(source);
        source[0] = new BankingProfileKey(0.0, 99.0);
        source.Add(new BankingProfileKey(20.0, 1.0));

        Assert.Equal(2, banking.Keys.Count);
        Assert.Equal(0.25, banking.Keys[0].RollRadians);
        IList<BankingProfileKey> exposed =
            Assert.IsAssignableFrom<IList<BankingProfileKey>>(banking.Keys);
        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => exposed[0] = new BankingProfileKey(0.0, 1.0));
    }

    [Fact]
    public void TrackBankingDefinition_RejectsNullAndFewerThanTwoKeys()
    {
        Assert.Throws<ArgumentNullException>(() => new TrackBankingDefinition(null!));
        Assert.Throws<ArgumentException>(
            () => new TrackBankingDefinition(Array.Empty<BankingProfileKey>()));
        Assert.Throws<ArgumentException>(
            () => new TrackBankingDefinition(new[] { new BankingProfileKey(0.0, 0.0) }));
    }

    [Fact]
    public void TrackBankingDefinition_RejectsNonFiniteValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(double.NaN, 0.0),
            new BankingProfileKey(10.0, 1.0)
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.0),
            new BankingProfileKey(10.0, double.PositiveInfinity)
        }));
    }

    [Fact]
    public void TrackBankingDefinition_RejectsDuplicateDescendingAndUnsupportedKeys()
    {
        Assert.Throws<ArgumentException>(() => new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.0),
            new BankingProfileKey(0.0, 1.0)
        }));
        Assert.Throws<ArgumentException>(() => new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(2.0, 0.0),
            new BankingProfileKey(1.0, 1.0)
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, 0.0, (BankingProfileInterpolationMode)99),
            new BankingProfileKey(10.0, 1.0)
        }));
    }

    [Fact]
    public void Compile_RejectsExplicitBankingWithoutExactEndpoints()
    {
        Assert.Throws<ArgumentException>(() => TrackAuthoringDocumentBuilder.Compile(
            CreateDefinitionWithBanking(
                new BankingProfileKey(1.0, 0.0),
                new BankingProfileKey(10.0, 1.0))));
        Assert.Throws<ArgumentException>(() => TrackAuthoringDocumentBuilder.Compile(
            CreateDefinitionWithBanking(
                new BankingProfileKey(0.0, 0.0),
                new BankingProfileKey(9.0, 1.0))));
    }

    [Fact]
    public void Compile_RejectsExplicitBankingOutsideAuthoredDomain()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackAuthoringDocumentBuilder.Compile(
            CreateDefinitionWithBanking(
                new BankingProfileKey(-1.0, 0.0),
                new BankingProfileKey(10.0, 1.0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackAuthoringDocumentBuilder.Compile(
            CreateDefinitionWithBanking(
                new BankingProfileKey(0.0, 0.0),
                new BankingProfileKey(11.0, 1.0))));
    }

    [Fact]
    public void Compile_ExplicitBankingPreservesKeysModesAndUnwrappedRolls()
    {
        double fullTurn = System.Math.PI * 2.0;
        var authored = new TrackBankingDefinition(new[]
        {
            new BankingProfileKey(0.0, fullTurn + 0.25, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(4.0, fullTurn * 2.0, BankingProfileInterpolationMode.SmoothStep),
            new BankingProfileKey(10.0, -fullTurn - 0.5, BankingProfileInterpolationMode.Constant)
        });
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(
                new[] { new StraightSectionDefinition("track", 10.0, rollRadians: 0.3) },
                TrackStartPose.Identity,
                authored));

        Assert.NotSame(authored.Keys, compilation.BankingProfile.Keys);
        AssertKeysEqual(authored.Keys, compilation.BankingProfile.Keys);
    }

    [Fact]
    public void Compile_WithoutExplicitBankingCreatesSectionBoundaryFallbackKeys()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("first", 4.0, 0.1),
                new StraightSectionDefinition("second", 3.0, -0.2),
                new StraightSectionDefinition("third", 5.0, 0.7)
            }));

        Assert.Collection(
            compilation.BankingProfile.Keys,
            key => AssertKey(key, 0.0, 0.1),
            key => AssertKey(key, 4.0, -0.2),
            key => AssertKey(key, 7.0, 0.7),
            key => AssertKey(key, 12.0, 0.7));
    }

    [Fact]
    public void FallbackBankingFramesMatchDefaultEvaluatorAcrossSectionBoundaries()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(new GeometricSectionDefinition[]
            {
                new StraightSectionDefinition("first", 4.0, 0.1),
                new StraightSectionDefinition("second", 4.0, -0.4),
                new StraightSectionDefinition("third", 4.0, 0.8)
            }));
        var evaluator = new TrackEvaluator(compilation.Runtime);
        double[] distances =
        {
            0.0,
            3.999999,
            4.0,
            4.000001,
            7.999999,
            8.0,
            8.000001,
            12.0
        };

        TrackFrame[] profileFrames = BankingProfileSampler.SampleFramesAtDistances(
            evaluator,
            compilation.BankingProfile,
            distances);

        for (int i = 0; i < distances.Length; i++)
        {
            AssertFrameNear(evaluator.EvaluateFrameAtDistance(distances[i]), profileFrames[i]);
        }
    }

    [Fact]
    public void ExplicitBankingDoesNotChangeCenterlineSegmentRollsOrDefaultFrames()
    {
        GeometricSectionDefinition[] sections =
        {
            new StraightSectionDefinition("entry", 5.0, 0.2),
            new ConstantCurvatureSectionDefinition("turn", 5.0, 20.0, -0.3)
        };
        TrackAuthoringCompilation legacy = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(sections));
        TrackAuthoringCompilation explicitBanking = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(
                sections,
                TrackStartPose.Identity,
                new TrackBankingDefinition(new[]
                {
                    new BankingProfileKey(0.0, 0.9, BankingProfileInterpolationMode.SmoothStep),
                    new BankingProfileKey(10.0, -1.1, BankingProfileInterpolationMode.Linear)
                })));

        Assert.Equal(legacy.Document.Segments.Count, explicitBanking.Document.Segments.Count);
        for (int i = 0; i < sections.Length; i++)
        {
            Assert.Equal(
                legacy.Document.Segments[i].RollRadians,
                explicitBanking.Document.Segments[i].RollRadians);
        }

        var legacyEvaluator = new TrackEvaluator(legacy.Runtime);
        var explicitEvaluator = new TrackEvaluator(explicitBanking.Runtime);
        foreach (double distance in new[] { 0.0, 4.999999, 5.0, 5.000001, 10.0 })
        {
            AssertFrameNear(
                legacyEvaluator.EvaluateFrameAtDistance(distance),
                explicitEvaluator.EvaluateFrameAtDistance(distance));
        }
    }

    [Fact]
    public void OptInBankingProfileSamplerUsesExplicitAuthoredBanking()
    {
        TrackAuthoringCompilation compilation = TrackAuthoringDocumentBuilder.Compile(
            new TrackAuthoringDefinition(
                new[] { new StraightSectionDefinition("track", 10.0, rollRadians: 0.25) },
                TrackStartPose.Identity,
                new TrackBankingDefinition(new[]
                {
                    new BankingProfileKey(0.0, 0.0, BankingProfileInterpolationMode.Linear),
                    new BankingProfileKey(10.0, System.Math.PI * 0.5)
                })));
        var evaluator = new TrackEvaluator(compilation.Runtime);

        TrackFrame sampled = Assert.Single(BankingProfileSampler.SampleFramesAtDistances(
            evaluator,
            compilation.BankingProfile,
            new[] { 5.0 }));
        double expectedAxisComponent = System.Math.Sqrt(0.5);

        AssertVectorNear(new Vector3d(5.0, 0.0, 0.0), sampled.Position);
        AssertVectorNear(Vector3d.UnitX, sampled.Tangent);
        AssertVectorNear(new Vector3d(0.0, expectedAxisComponent, expectedAxisComponent), sampled.Normal);
        AssertVectorNear(new Vector3d(0.0, -expectedAxisComponent, expectedAxisComponent), sampled.Binormal);
        Assert.NotEqual(evaluator.EvaluateFrameAtDistance(5.0).Normal, sampled.Normal);
    }

    [Fact]
    public void Recompile_ProducesDistinctEquivalentBankingProfileSnapshots()
    {
        TrackAuthoringDefinition definition = CreateDefinitionWithBanking(
            new BankingProfileKey(0.0, 0.25, BankingProfileInterpolationMode.Linear),
            new BankingProfileKey(10.0, 7.0, BankingProfileInterpolationMode.SmoothStep));

        TrackAuthoringCompilation first = TrackAuthoringDocumentBuilder.Compile(definition);
        TrackAuthoringCompilation second = TrackAuthoringDocumentBuilder.Compile(definition);

        Assert.NotSame(first.BankingProfile, second.BankingProfile);
        Assert.NotSame(first.BankingProfile.Keys, second.BankingProfile.Keys);
        AssertKeysEqual(first.BankingProfile.Keys, second.BankingProfile.Keys);
    }

    private static TrackAuthoringDefinition CreateDefinitionWithBanking(
        params BankingProfileKey[] keys)
    {
        return new TrackAuthoringDefinition(
            new[] { new StraightSectionDefinition("track", 10.0) },
            TrackStartPose.Identity,
            new TrackBankingDefinition(keys));
    }

    private static void AssertKeysEqual(
        IReadOnlyList<BankingProfileKey> expected,
        IReadOnlyList<BankingProfileKey> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Distance, actual[i].Distance);
            Assert.Equal(expected[i].RollRadians, actual[i].RollRadians);
            Assert.Equal(expected[i].InterpolationToNext, actual[i].InterpolationToNext);
        }
    }

    private static void AssertKey(BankingProfileKey key, double distance, double rollRadians)
    {
        Assert.Equal(distance, key.Distance);
        Assert.Equal(rollRadians, key.RollRadians);
        Assert.Equal(BankingProfileInterpolationMode.Constant, key.InterpolationToNext);
    }

    private static void AssertFrameNear(TrackFrame expected, TrackFrame actual)
    {
        AssertNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertNear(expected.X, actual.X);
        AssertNear(expected.Y, actual.Y);
        AssertNear(expected.Z, actual.Z);
    }

    private static void AssertNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
