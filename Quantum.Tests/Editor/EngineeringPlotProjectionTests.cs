using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Plots;
using Quantum.Track.Authoring;

namespace Quantum.Tests;

public sealed class EngineeringPlotProjectionTests
{
    [Fact]
    public void GetValue_ProjectsEveryPlotDirectlyFromEngineeringSnapshot()
    {
        var workspace = new EditorWorkspace();
        workspace.NewDocument();
        EngineeringSnapshot snapshot = workspace.EngineeringSnapshot!;
        const int sampleIndex = 37;
        EngineeringGeometrySample geometry = snapshot.Geometry[sampleIndex];

        Assert.Equal(
            geometry.Position.Y,
            EngineeringPlotProjection.GetValue(
                snapshot,
                EngineeringPlotKind.Elevation,
                sampleIndex));
        Assert.Equal(
            geometry.CurvatureMagnitude,
            EngineeringPlotProjection.GetValue(
                snapshot,
                EngineeringPlotKind.Curvature,
                sampleIndex));
        Assert.Equal(
            snapshot.BankingRollRadians[sampleIndex] * 180.0 / System.Math.PI,
            EngineeringPlotProjection.GetValue(
                snapshot,
                EngineeringPlotKind.Roll,
                sampleIndex)!.Value,
            12);

        double expectedPitch = System.Math.Atan2(
            geometry.Tangent.Y,
            System.Math.Sqrt(
                (geometry.Tangent.X * geometry.Tangent.X) +
                (geometry.Tangent.Z * geometry.Tangent.Z))) * 180.0 / System.Math.PI;
        double expectedYaw = System.Math.Atan2(
            geometry.Tangent.Z,
            geometry.Tangent.X) * 180.0 / System.Math.PI;
        Assert.Equal(
            expectedPitch,
            EngineeringPlotProjection.GetValue(
                snapshot,
                EngineeringPlotKind.Pitch,
                sampleIndex)!.Value,
            12);
        Assert.Equal(
            expectedYaw,
            EngineeringPlotProjection.GetValue(
                snapshot,
                EngineeringPlotKind.Yaw,
                sampleIndex)!.Value,
            12);
    }

    [Fact]
    public void FindNearestSampleIndex_ClampsAndUsesCanonicalStationGrid()
    {
        var workspace = new EditorWorkspace();
        workspace.NewDocument();
        EngineeringSnapshot snapshot = workspace.EngineeringSnapshot!;
        double lowerStation = snapshot.StationGrid[10];
        double upperStation = snapshot.StationGrid[11];

        Assert.Equal(0, EngineeringPlotProjection.FindNearestSampleIndex(snapshot, -10.0));
        Assert.Equal(
            snapshot.SampleCount - 1,
            EngineeringPlotProjection.FindNearestSampleIndex(
                snapshot,
                snapshot.TotalLength + 10.0));
        Assert.Equal(
            10,
            EngineeringPlotProjection.FindNearestSampleIndex(
                snapshot,
                lowerStation + ((upperStation - lowerStation) * 0.49)));
        Assert.Equal(
            11,
            EngineeringPlotProjection.FindNearestSampleIndex(
                snapshot,
                lowerStation + ((upperStation - lowerStation) * 0.51)));
    }
}
