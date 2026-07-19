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
            _ => throw new ArgumentOutOfRangeException(nameof(plot), plot, "A single engineering plot is required.")
        };
    }

    public static int FindNearestSampleIndex(
        EngineeringSnapshot snapshot,
        double station)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SampleCount == 0)
        {
            return -1;
        }

        double clampedStation = System.Math.Clamp(station, 0.0, snapshot.TotalLength);
        int upperIndex = LowerBound(snapshot.StationGrid, clampedStation);
        if (upperIndex <= 0)
        {
            return 0;
        }

        if (upperIndex >= snapshot.SampleCount)
        {
            return snapshot.SampleCount - 1;
        }

        double lowerDelta = clampedStation - snapshot.StationGrid[upperIndex - 1];
        double upperDelta = snapshot.StationGrid[upperIndex] - clampedStation;
        return lowerDelta <= upperDelta ? upperIndex - 1 : upperIndex;
    }

    private static int LowerBound(IReadOnlyList<double> stations, double station)
    {
        int lower = 0;
        int upper = stations.Count;
        while (lower < upper)
        {
            int middle = lower + ((upper - lower) / 2);
            if (stations[middle] < station)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower;
    }
}
