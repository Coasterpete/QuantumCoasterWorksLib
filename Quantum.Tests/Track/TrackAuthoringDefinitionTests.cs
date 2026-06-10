using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class TrackAuthoringDefinitionTests
{
    [Fact]
    public void Constructor_PreservesSectionOrderReferencesAndExactIds()
    {
        var first = new StraightSectionDefinition("  lift-01  ", 12.0, 0.1);
        var second = new ConstantCurvatureSectionDefinition("turn-02", 8.0, -20.0, -0.2);
        var source = new List<GeometricSectionDefinition> { first, second };

        var definition = new TrackAuthoringDefinition(source);
        source.Reverse();

        Assert.Same(first, definition.Sections[0]);
        Assert.Same(second, definition.Sections[1]);
        Assert.Equal("  lift-01  ", definition.Sections[0].Id);
        Assert.Equal("turn-02", definition.Sections[1].Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SectionDefinitions_RejectBlankIds(string? id)
    {
        Assert.Throws<ArgumentException>(() => new StraightSectionDefinition(id!, 1.0));
        Assert.Throws<ArgumentException>(
            () => new ConstantCurvatureSectionDefinition(id!, 1.0, 10.0));
    }

    [Fact]
    public void TrackDefinition_RejectsDuplicateIds()
    {
        var sections = new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("same-id", 4.0),
            new ConstantCurvatureSectionDefinition("same-id", 5.0, 12.0)
        };

        Assert.Throws<ArgumentException>(() => new TrackAuthoringDefinition(sections));
    }

    [Fact]
    public void TrackDefinition_UsesOrdinalCaseSensitiveIds()
    {
        var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
        {
            new StraightSectionDefinition("section", 4.0),
            new StraightSectionDefinition("SECTION", 5.0)
        });

        Assert.Equal(2, definition.Sections.Count);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SectionDefinitions_RejectInvalidLengths(double length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StraightSectionDefinition("straight", length));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConstantCurvatureSectionDefinition("arc", length, 10.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ConstantCurvatureDefinition_RejectsInvalidRadius(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConstantCurvatureSectionDefinition("arc", 10.0, radius));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SectionDefinitions_RejectInvalidRoll(double rollRadians)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StraightSectionDefinition("straight", 10.0, rollRadians));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConstantCurvatureSectionDefinition("arc", 10.0, 20.0, rollRadians));
    }

    [Fact]
    public void TrackDefinition_RejectsNullEmptyAndNullEntryCollections()
    {
        Assert.Throws<ArgumentNullException>(() => new TrackAuthoringDefinition(null!));
        Assert.Throws<ArgumentException>(
            () => new TrackAuthoringDefinition(Array.Empty<GeometricSectionDefinition>()));
        Assert.Throws<ArgumentException>(
            () => new TrackAuthoringDefinition(new GeometricSectionDefinition[] { null! }));
    }
}
