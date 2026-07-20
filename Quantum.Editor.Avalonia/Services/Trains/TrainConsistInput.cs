using System.Globalization;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Services.Trains;

/// <summary>
/// Frontend text fields used to atomically construct an immutable backend consist.
/// </summary>
public sealed record TrainConsistInput(
    string CarCount,
    string CarCenterSpacing,
    string CarBodyLength,
    string CarBodyWidth,
    string CarBodyHeight,
    string BogieSpacing)
{
    public static TrainConsistInput FromDefinition(TrainConsistDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new TrainConsistInput(
            definition.CarCount.ToString(CultureInfo.InvariantCulture),
            definition.CarSpacing.ToString("R", CultureInfo.InvariantCulture),
            definition.CarLength.ToString("R", CultureInfo.InvariantCulture),
            definition.CarWidth.ToString("R", CultureInfo.InvariantCulture),
            definition.CarHeight.ToString("R", CultureInfo.InvariantCulture),
            definition.BogieSpacing.ToString("R", CultureInfo.InvariantCulture));
    }
}
