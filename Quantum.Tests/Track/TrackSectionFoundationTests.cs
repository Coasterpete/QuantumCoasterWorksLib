using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrackSectionFoundationTests
{
    [Fact]
    public void TrackSection_CanCreateForceAndGeometricSections()
    {
        var lengthDrivenForce = new ForceSection(targetNormalG: 3.2, targetLateralG: 0.25, length: 18.0);
        var durationDrivenForce = new ForceSection(duration: 2.5);
        var geometric = new GeometricSection(length: 24.0, curvature: 0.08, roll: 0.15);

        Assert.IsType<ForceSection>(lengthDrivenForce);
        Assert.IsType<ForceSection>(durationDrivenForce);
        Assert.IsType<GeometricSection>(geometric);
    }

    [Fact]
    public void TrackDocument_CanContainMixedSections()
    {
        var document = new TrackDocument(
            sections: new TrackSection[]
            {
                new ForceSection(length: 10.0, targetNormalG: 2.8),
                new GeometricSection(length: 16.0, curvature: 0.12),
                new ForceSection(duration: 1.2, targetLateralG: -0.15)
            });

        Assert.Equal(3, document.Sections.Count);
        Assert.IsType<ForceSection>(document.Sections[0]);
        Assert.IsType<GeometricSection>(document.Sections[1]);
        Assert.IsType<ForceSection>(document.Sections[2]);
    }

    [Fact]
    public void TrackDocument_PreservesSectionProperties()
    {
        var force = new ForceSection(targetNormalG: 3.5, targetLateralG: 0.4, duration: 1.75);
        var geometric = new GeometricSection(length: 22.0, curvature: 0.11, roll: -0.05);
        var document = new TrackDocument(sections: new TrackSection[] { force, geometric });

        var storedForce = Assert.IsType<ForceSection>(document.Sections[0]);
        Assert.Equal(3.5, storedForce.TargetNormalG);
        Assert.Equal(0.4, storedForce.TargetLateralG);
        Assert.Null(storedForce.Length);
        Assert.Equal(1.75, storedForce.Duration);

        var storedGeometric = Assert.IsType<GeometricSection>(document.Sections[1]);
        Assert.Equal(22.0, storedGeometric.Length);
        Assert.Equal(0.11, storedGeometric.Curvature);
        Assert.Equal(-0.05, storedGeometric.Roll);
    }
}
