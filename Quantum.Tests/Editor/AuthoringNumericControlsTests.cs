using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Quantum.Editor.Avalonia.Controls;

namespace Quantum.Tests;

public sealed class AuthoringNumericControlsTests
{
    [Fact]
    public void Profiles_ExposeExpectedBoundsStepsPrecisionAndSpinnerControls()
    {
        NumericUpDown length = AuthoringNumericControls.Create(
            "length",
            AuthoringNumericParameterKind.LengthMeters,
            10.0);
        NumericUpDown roll = AuthoringNumericControls.Create(
            "roll",
            AuthoringNumericParameterKind.RollDegrees,
            0.0);
        NumericUpDown radius = AuthoringNumericControls.Create(
            "radius",
            AuthoringNumericParameterKind.SignedRadiusMeters,
            25.0);
        NumericUpDown curvature = AuthoringNumericControls.Create(
            "curvature",
            AuthoringNumericParameterKind.CurvaturePerMeter,
            0.04);

        Assert.Equal(0.0m, length.Minimum);
        Assert.Equal(1_000_000.0m, length.Maximum);
        Assert.Equal(1.0m, length.Increment);
        Assert.Equal("0.######", length.FormatString);

        Assert.Equal(-36_000.0m, roll.Minimum);
        Assert.Equal(36_000.0m, roll.Maximum);
        Assert.Equal(1.0m, roll.Increment);
        Assert.Equal("0.###", roll.FormatString);

        Assert.Equal(-1_000_000.0m, radius.Minimum);
        Assert.Equal(1_000_000.0m, radius.Maximum);
        Assert.Equal(1.0m, radius.Increment);
        Assert.Equal("0.######", radius.FormatString);

        Assert.Equal(-100.0m, curvature.Minimum);
        Assert.Equal(100.0m, curvature.Maximum);
        Assert.Equal(0.001m, curvature.Increment);
        Assert.Equal("0.########", curvature.FormatString);

        Assert.All(
            new[] { length, roll, radius, curvature },
            field =>
            {
                Assert.True(field.AllowSpin);
                Assert.True(field.ShowButtonSpinner);
                Assert.False(field.ClipValueToMinMax);
                Assert.Same(NumberFormatInfo.InvariantInfo, field.NumberFormat);
                Assert.Equal(NumberStyles.Float, field.ParsingNumberStyle);
            });
    }

    [Fact]
    public void ReadFiniteDouble_PreservesUnderlyingPrecisionWhileDisplayIsRounded()
    {
        const double productionValue = 0.04000000000000001;
        NumericUpDown field = AuthoringNumericControls.Create(
            "curvature",
            AuthoringNumericParameterKind.CurvaturePerMeter,
            productionValue);

        Assert.Equal("0.04", field.Value!.Value.ToString(
            field.FormatString,
            field.NumberFormat));
        Assert.Equal(
            productionValue,
            AuthoringNumericControls.ReadFiniteDouble(field, "curvature"));
    }

    [Fact]
    public void ReadFiniteDouble_AcceptsTypedInvariantDecimalAndExponentValues()
    {
        NumericUpDown field = AuthoringNumericControls.Create(
            "curvature",
            AuthoringNumericParameterKind.CurvaturePerMeter,
            0.0);

        field.Text = "0.01234567";
        Assert.Equal(
            0.01234567,
            AuthoringNumericControls.ReadFiniteDouble(field, "curvature"),
            12);

        field.Text = "1e-3";
        Assert.Equal(
            0.001,
            AuthoringNumericControls.ReadFiniteDouble(field, "curvature"),
            12);
    }

    [Fact]
    public void ReadFiniteDouble_RejectsInvalidAndOutOfRangeTypedText()
    {
        NumericUpDown field = AuthoringNumericControls.Create(
            "curvature",
            AuthoringNumericParameterKind.CurvaturePerMeter,
            0.0);

        field.Text = "not-a-number";
        Assert.Throws<FormatException>(() =>
            AuthoringNumericControls.ReadFiniteDouble(field, "curvature"));

        field.Text = "101";
        Assert.Throws<FormatException>(() =>
            AuthoringNumericControls.ReadFiniteDouble(field, "curvature"));
    }

    [Fact]
    public void IncrementedValue_UsesConfiguredArrowStepWithoutRoundingDrift()
    {
        NumericUpDown field = AuthoringNumericControls.Create(
            "curvature",
            AuthoringNumericParameterKind.CurvaturePerMeter,
            0.04);

        field.Value += field.Increment;
        field.Text = field.Value!.Value.ToString(field.FormatString, field.NumberFormat);

        Assert.Equal(0.041m, field.Value);
        Assert.Equal(
            0.041,
            AuthoringNumericControls.ReadFiniteDouble(field, "curvature"),
            12);
    }

    [Fact]
    public void TransitionalEditors_DeclareNumericControlsOnlyForEditableParameters()
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        string[] dialogNumericFields =
        {
            "lengthField",
            "rollField",
            "radiusField",
            "startCurvatureField",
            "endCurvatureField"
        };

        Assert.All(
            dialogNumericFields,
            fieldName => Assert.Equal(
                typeof(NumericUpDown),
                typeof(SectionEditorDialog).GetField(fieldName, privateInstance)?.FieldType));
        Assert.Equal(
            typeof(TextBox),
            typeof(SectionEditorDialog).GetField("idField", privateInstance)?.FieldType);

        FieldInfo? inspectorNumericFields = typeof(InspectorPaneControl).GetField(
            "numericInspectorFields",
            privateInstance);
        FieldInfo? inspectorTextFields = typeof(InspectorPaneControl).GetField(
            "inspectorFields",
            privateInstance);
        Assert.Equal(
            typeof(Dictionary<string, NumericUpDown>),
            inspectorNumericFields?.FieldType);
        Assert.Equal(
            typeof(Dictionary<string, TextBox>),
            inspectorTextFields?.FieldType);
    }
}
