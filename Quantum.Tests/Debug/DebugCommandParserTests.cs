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
    public void TryParse_UnknownCommand_ReturnsFalse()
    {
        bool parsed = DebugCommandParser.TryParse(new[] { "unknown-command" }, out DebugCommandKind command);

        Assert.False(parsed);
        Assert.Equal(default, command);
    }
}
