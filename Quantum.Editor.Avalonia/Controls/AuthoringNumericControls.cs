using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Quantum.Editor.Avalonia.Controls;

internal enum AuthoringNumericParameterKind
{
    LengthMeters = 0,
    RollDegrees = 1,
    SignedRadiusMeters = 2,
    CurvaturePerMeter = 3
}

/// <summary>
/// Shared Avalonia presentation settings for transitional section-authoring controls.
/// Production section constructors remain authoritative for domain validation.
/// </summary>
internal static class AuthoringNumericControls
{
    private static readonly ConditionalWeakTable<NumericUpDown, InitialValue> InitialValues = new();

    internal static NumericUpDown Create(
        string key,
        AuthoringNumericParameterKind kind,
        double value)
    {
        Settings settings = GetSettings(kind);
        decimal decimalValue = ToDecimal(value, key);
        decimal minimum = System.Math.Min(settings.Minimum, decimalValue);
        decimal maximum = System.Math.Max(settings.Maximum, decimalValue);

        var field = new NumericUpDown
        {
            Tag = key,
            Minimum = minimum,
            Maximum = maximum,
            Increment = settings.Increment,
            FormatString = settings.FormatString,
            NumberFormat = NumberFormatInfo.InvariantInfo,
            ParsingNumberStyle = NumberStyles.Float,
            AllowSpin = true,
            ShowButtonSpinner = true,
            ClipValueToMinMax = false,
            MinHeight = 30
        };
        field.Value = decimalValue;
        field.Text = decimalValue.ToString(field.FormatString, field.NumberFormat);
        InitialValues.Add(field, new InitialValue(value, decimalValue));
        return field;
    }

    internal static double ReadFiniteDouble(NumericUpDown field, string label)
    {
        if (field is null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        string text = field.Text?.Trim() ?? string.Empty;
        if (field.Value.HasValue)
        {
            decimal currentValue = field.Value.Value;
            if (InitialValues.TryGetValue(field, out InitialValue? initialValue) &&
                string.Equals(
                    text,
                    initialValue.DecimalValue.ToString(
                        field.FormatString,
                        field.NumberFormat),
                    StringComparison.Ordinal))
            {
                return initialValue.DoubleValue;
            }

            string formattedValue = currentValue.ToString(
                field.FormatString,
                field.NumberFormat);
            if (string.Equals(text, formattedValue, StringComparison.Ordinal))
            {
                return ToFiniteDouble(currentValue, label);
            }
        }

        if (!decimal.TryParse(
                text,
                field.ParsingNumberStyle,
                field.NumberFormat,
                out decimal parsedValue))
        {
            throw new FormatException(
                $"'{text}' is not a finite invariant-culture number for {label}.");
        }

        if (parsedValue < field.Minimum || parsedValue > field.Maximum)
        {
            throw new FormatException(
                $"'{text}' is outside the supported range " +
                $"[{field.Minimum}, {field.Maximum}] for {label}.");
        }

        return ToFiniteDouble(parsedValue, label);
    }

    private static Settings GetSettings(AuthoringNumericParameterKind kind)
    {
        return kind switch
        {
            AuthoringNumericParameterKind.LengthMeters => new Settings(
                minimum: 0.0m,
                maximum: 1_000_000.0m,
                increment: 1.0m,
                formatString: "0.######"),
            AuthoringNumericParameterKind.RollDegrees => new Settings(
                minimum: -36_000.0m,
                maximum: 36_000.0m,
                increment: 1.0m,
                formatString: "0.###"),
            AuthoringNumericParameterKind.SignedRadiusMeters => new Settings(
                minimum: -1_000_000.0m,
                maximum: 1_000_000.0m,
                increment: 1.0m,
                formatString: "0.######"),
            AuthoringNumericParameterKind.CurvaturePerMeter => new Settings(
                minimum: -100.0m,
                maximum: 100.0m,
                increment: 0.001m,
                formatString: "0.########"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported authoring numeric parameter kind.")
        };
    }

    private static decimal ToDecimal(double value, string label)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"The initial value for {label} must be finite.");
        }

        try
        {
            return (decimal)value;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"The initial value for {label} is outside the numeric authoring range.");
        }
    }

    private static double ToFiniteDouble(decimal value, string label)
    {
        double result = decimal.ToDouble(value);
        if (!double.IsFinite(result))
        {
            throw new FormatException($"The numeric value for {label} must be finite.");
        }

        return result;
    }

    private readonly struct Settings
    {
        public Settings(
            decimal minimum,
            decimal maximum,
            decimal increment,
            string formatString)
        {
            Minimum = minimum;
            Maximum = maximum;
            Increment = increment;
            FormatString = formatString;
        }

        public decimal Minimum { get; }

        public decimal Maximum { get; }

        public decimal Increment { get; }

        public string FormatString { get; }
    }

    private sealed class InitialValue
    {
        public InitialValue(double doubleValue, decimal decimalValue)
        {
            DoubleValue = doubleValue;
            DecimalValue = decimalValue;
        }

        public double DoubleValue { get; }

        public decimal DecimalValue { get; }
    }
}
