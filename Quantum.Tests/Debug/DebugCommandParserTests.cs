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
    public void TryParse_UnknownCommand_ReturnsFalse()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "unknown-command" }, out DebugCommandKind command);

        Assert.False(parsed);
        Assert.Equal(default, command);
    }
}
