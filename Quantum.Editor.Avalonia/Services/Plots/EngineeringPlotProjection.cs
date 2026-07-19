using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services.Plots;

public static class EngineeringPlotProjection
{
    private const double RadiansToDegrees = 180.0 / System.Math.PI;

    public static double? GetValue(
        EngineeringSnapshot snapshot,
        EngineeringPlotKind plot,
        int sampleIndex)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (sampleIndex < 0 || sampleIndex >= snapshot.SampleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIndex));
        }

        EngineeringGeometrySample geometry = snapshot.Geometry[sampleIndex];
        return plot switch
        {
            EngineeringPlotKind.Elevation => geometry.Position.Y,
            EngineeringPlotKind.Curvature => geometry.CurvatureMagnitude,
            EngineeringPlotKind.Roll => snapshot.BankingRollRadians[sampleIndex] * RadiansToDegrees,
            EngineeringPlotKind.Pitch => System.Math.Atan2(
                geometry.Tangent.Y,
                System.Math.Sqrt(
                    (geometry.Tangent.X * geometry.Tangent.X) +
                    (geometry.Tangent.Z * geometry.Tangent.Z))) * RadiansToDegrees,
            EngineeringPlotKind.Yaw => System.Math.Atan2(
                geometry.Tangent.Z,
                geometry.Tangent.X) * RadiansToDegrees,
            _ => throw new ArgumentOutOfRangeException(nameof(plot), plot, "A single Math Plot is required.")
        };
    }

    public static int FindNearestSampleIndex(
        EngineeringSnapshot snapshot,
        double station)
    {
        return EngineeringSnapshotNavigation.FindNearestSampleIndex(snapshot, station);
    }
}
