using Quantum.Track;

namespace Quantum.Editor.Avalonia.Models;

/// <summary>
/// Deterministic read-only frontend projection of backend consist geometry.
/// </summary>
public sealed class TrainConsistPresentation
{
    private TrainConsistPresentation(
        TrainConsistDefinition definition,
        IReadOnlyList<TrainCarSchematic> cars)
    {
        Definition = definition;
        Cars = cars;
        ApproximateTotalLength = definition.CarLength +
            ((definition.CarCount - 1) * definition.CarSpacing);
        InterCarGap = definition.CarSpacing - definition.CarLength;
    }

    public TrainConsistDefinition Definition { get; }

    public IReadOnlyList<TrainCarSchematic> Cars { get; }

    public double ApproximateTotalLength { get; }

    public double InterCarGap { get; }

    public static TrainConsistPresentation Create(TrainConsistDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        double firstCenter = -0.5 * (definition.CarCount - 1) * definition.CarSpacing;
        var cars = new TrainCarSchematic[definition.CarCount];
        for (int index = 0; index < cars.Length; index++)
        {
            double center = firstCenter + (index * definition.CarSpacing);
            cars[index] = new TrainCarSchematic(
                index,
                center,
                center - (definition.CarLength * 0.5),
                center - (definition.BogieSpacing * 0.5),
                center + (definition.BogieSpacing * 0.5));
        }

        return new TrainConsistPresentation(definition, Array.AsReadOnly(cars));
    }
}

public readonly record struct TrainCarSchematic(
    int Index,
    double Center,
    double Start,
    double RearBogieCenter,
    double FrontBogieCenter);
