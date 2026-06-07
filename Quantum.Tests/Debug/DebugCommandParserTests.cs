using Quantum.Debug;

namespace Quantum.Tests;

public sealed class DebugCommandParserTests
{
    [Fact]
    public void TryParse_NoArguments_DefaultsToValidate()
    {
        bool parsed = DebugCommandParser.TryParse(Array.Empty<string>(), out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.Validate, command);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void TryParse_HelpToken_ParsesAsHelp(string token)
    {
        bool parsed = DebugCommandParser.TryParse(new[] { token }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.Help, command);
    }

    [Fact]
    public void TryParse_SamplingPerfCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "SaMpLiNg-PeRf" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.SamplingPerf, command);
    }

    [Fact]
    public void TryParse_TrainPoseExportV1Command_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "TrAiN-PoSe-ExPoRt-V1" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.TrainPoseExportV1, command);
    }

    [Fact]
    public void TryParse_MeshExportV1SampleCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "MeSh-ExPoRt-V1-SaMpLe" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.MeshExportV1Sample, command);
    }

    [Fact]
    public void TryParse_DebugViewportSnapshotV1Command_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DeBuG-ViEwPoRt-SnApShOt-V1" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DebugViewportSnapshotV1, command);
    }

    [Fact]
    public void TryParse_DebugViewportSnapshotV1FromCsvCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DeBuG-ViEwPoRt-SnApShOt-V1-FrOm-CsV" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DebugViewportSnapshotV1FromCsv, command);
    }

    [Fact]
    public void TryParse_DebugViewportSnapshotV1ValidateCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DeBuG-ViEwPoRt-SnApShOt-V1-VaLiDaTe" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DebugViewportSnapshotV1Validate, command);
    }

    [Fact]
    public void TryParse_DebugViewportSnapshotV1SvgCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DeBuG-ViEwPoRt-SnApShOt-V1-SvG" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DebugViewportSnapshotV1Svg, command);
    }

    [Fact]
    public void TryParse_DebugViewportSnapshotV1GalleryCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DeBuG-ViEwPoRt-SnApShOt-V1-GaLlErY" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DebugViewportSnapshotV1Gallery, command);
    }

    [Fact]
    public void TryParse_DebugViewportSnapshotV1BrowserCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DeBuG-ViEwPoRt-SnApShOt-V1-BrOwSeR" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DebugViewportSnapshotV1Browser, command);
    }

    [Fact]
    public void TryParse_DebugViewportSnapshotV1BankingProfileCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DeBuG-ViEwPoRt-SnApShOt-V1-BaNkInG-PrOfIlE" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DebugViewportSnapshotV1BankingProfile, command);
    }

    [Fact]
    public void TryParse_LongitudinalForcePreviewCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "LoNgItUdInAl-FoRcE-PrEvIeW" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.LongitudinalForcePreview, command);
    }

    [Fact]
    public void TryParse_LongitudinalSpeedPreviewCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "LoNgItUdInAl-SpEeD-PrEvIeW" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.LongitudinalSpeedPreview, command);
    }

    [Fact]
    public void TryParse_CenterlineFrameContinuityCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "CeNtErLiNe-FrAmE-CoNtInUiTy" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.CenterlineFrameContinuity, command);
    }

    [Fact]
    public void TryParse_TransportedFrameComparisonCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "TrAnSpOrTeD-FrAmE-CoMpArIsOn" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.TransportedFrameComparison, command);
    }

    [Fact]
    public void TryParse_TransportedFrameComparisonBrowserCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "TrAnSpOrTeD-FrAmE-CoMpArIsOn-BrOwSeR" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.TransportedFrameComparisonBrowser, command);
    }

    [Fact]
    public void TryParse_BankingProfileDiagnosticsCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "BaNkInG-PrOfIlE-DiAgNoStIcS" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.BankingProfileDiagnostics, command);
    }

    [Fact]
    public void TryParse_ContinuousRollDiagnosticsSampleCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "CoNtInUoUs-RoLl-DiAgNoStIcS-SaMpLe" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.ContinuousRollDiagnosticsSample, command);
    }

    [Fact]
    public void TryParse_ContinuousRollDiagnosticsJsonCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "CoNtInUoUs-RoLl-DiAgNoStIcS-JsOn" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.ContinuousRollDiagnosticsJson, command);
    }

    [Fact]
    public void TryParse_DistanceInspectionJsonCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DiStAnCe-InSpEcTiOn-JsOn" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DistanceInspectionJson, command);
    }

    [Fact]
    public void TryParse_DistanceInspectionBrowserCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "DiStAnCe-InSpEcTiOn-BrOwSeR" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.DistanceInspectionBrowser, command);
    }

    [Fact]
    public void TryParse_BankingProfileBrowserCommand_ParsesCaseInsensitive()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "BaNkInG-PrOfIlE-BrOwSeR" }, out DebugCommandKind command);

        Assert.True(parsed);
        Assert.Equal(DebugCommandKind.BankingProfileBrowser, command);
    }

    [Fact]
    public void TryParse_UnknownCommand_ReturnsFalse()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "unknown-command" }, out DebugCommandKind command);

        Assert.False(parsed);
        Assert.Equal(default, command);
    }
}
