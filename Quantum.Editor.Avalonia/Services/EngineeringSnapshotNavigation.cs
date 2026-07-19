using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Services;

/// <summary>
/// Resolves editor navigation against the immutable canonical station grid and
/// resolved-section intervals in an engineering snapshot.
/// </summary>
public static class EngineeringSnapshotNavigation
{
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

    public static int FindSectionIndex(
        EngineeringSnapshot snapshot,
        int sampleIndex)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (sampleIndex < 0 || sampleIndex >= snapshot.SampleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIndex));
        }

        return FindSectionIndex(snapshot.ResolvedSections, snapshot.StationGrid[sampleIndex]);
    }

    public static int FindSectionIndex(
        IReadOnlyList<EngineeringResolvedSectionMetadata> sections,
        double station)
    {
        ArgumentNullException.ThrowIfNull(sections);
        for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            EngineeringResolvedSectionMetadata section = sections[sectionIndex];
            bool contains = station >= section.StartStation &&
                (station < section.EndStation ||
                 (section.IncludesEndStation && station <= section.EndStation));
            if (contains)
            {
                return sectionIndex;
            }
        }

        return sections.Count - 1;
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
