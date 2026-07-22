namespace Quantum.Editor.Avalonia.Services.Authoring;

/// <summary>
/// Small injectable policy shared by pointer and keyboard straight-length
/// scrubbing. Values are metres per pointer pixel or keyboard step.
/// </summary>
public sealed class StraightLengthScrubSensitivity
{
    public StraightLengthScrubSensitivity(
        double normalMetersPerStep = 0.1,
        double fineMetersPerStep = 0.01,
        double coarseMetersPerStep = 1.0)
    {
        NormalMetersPerStep = RequirePositiveFinite(
            normalMetersPerStep,
            nameof(normalMetersPerStep));
        FineMetersPerStep = RequirePositiveFinite(
            fineMetersPerStep,
            nameof(fineMetersPerStep));
        CoarseMetersPerStep = RequirePositiveFinite(
            coarseMetersPerStep,
            nameof(coarseMetersPerStep));
    }

    public static StraightLengthScrubSensitivity Default { get; } = new();

    public double NormalMetersPerStep { get; }

    public double FineMetersPerStep { get; }

    public double CoarseMetersPerStep { get; }

    public double Resolve(bool shift, bool control) => shift
        ? FineMetersPerStep
        : control ? CoarseMetersPerStep : NormalMetersPerStep;

    private static double RequirePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Scrub sensitivity must be finite and greater than zero.");
        }

        return value;
    }
}
