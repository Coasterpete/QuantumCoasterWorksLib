using System.Globalization;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Services.Trains;

/// <summary>
/// Owns the last valid immutable train definition while frontend edits are staged.
/// </summary>
public sealed class TrainConsistEditorSession
{
    public TrainConsistEditorSession()
        : this(CreateDefaultDefinition())
    {
    }

    public TrainConsistEditorSession(TrainConsistDefinition initialDefinition)
    {
        CurrentDefinition = initialDefinition ??
            throw new ArgumentNullException(nameof(initialDefinition));
    }

    public event EventHandler? StateChanged;

    public TrainConsistDefinition CurrentDefinition { get; private set; }

    public string? LastValidationMessage { get; private set; }

    public bool LastAttemptSucceeded { get; private set; } = true;

    public int Revision { get; private set; }

    public bool TryApply(TrainConsistInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            int carCount = ParseCarCount(input.CarCount);
            double carSpacing = ParseNumber(input.CarCenterSpacing, "Car-center spacing");
            var geometry = new TrainCarGeometry(
                ParseNumber(input.CarBodyLength, "Car body length"),
                ParseNumber(input.CarBodyWidth, "Car body width"),
                ParseNumber(input.CarBodyHeight, "Car body height"));
            var bogieLayout = new TrainBogieLayout(
                ParseNumber(input.BogieSpacing, "Bogie spacing"));

            var candidate = new TrainConsistDefinition(
                carCount,
                carSpacing,
                geometry,
                bogieLayout);

            CurrentDefinition = candidate;
            LastValidationMessage = null;
            LastAttemptSucceeded = true;
            Revision++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception exception) when (
            exception is FormatException ||
            exception is OverflowException ||
            exception is ArgumentOutOfRangeException)
        {
            LastValidationMessage = FirstLine(exception.Message);
            LastAttemptSucceeded = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }
    }

    private static TrainConsistDefinition CreateDefaultDefinition()
    {
        return new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carGeometry: new TrainCarGeometry(length: 2.2, width: 1.2, height: 1.1),
            bogieLayout: new TrainBogieLayout(bogieSpacing: 1.4));
    }

    private static int ParseCarCount(string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result))
        {
            throw new FormatException("Car count must be a whole number.");
        }

        return result;
    }

    private static double ParseNumber(string value, string displayName)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double result))
        {
            throw new FormatException(displayName + " must be a number in metres.");
        }

        return result;
    }

    private static string FirstLine(string message)
    {
        int lineBreak = message.IndexOfAny(new[] { '\r', '\n' });
        return lineBreak < 0 ? message : message[..lineBreak];
    }
}
