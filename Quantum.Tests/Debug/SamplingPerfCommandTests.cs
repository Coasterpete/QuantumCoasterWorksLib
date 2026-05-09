using System.Globalization;
using System.IO;
using Quantum.Debug;

namespace Quantum.Tests;

public sealed class SamplingPerfCommandTests
{
    [Fact]
    public void Run_WritesTableWithExpectedSchemaAndRelativeSpeedupColumn()
    {
        TextWriter originalOut = Console.Out;
        var writer = new StringWriter(CultureInfo.InvariantCulture);

        try
        {
            Console.SetOut(writer);
            SamplingPerfCommand.Run();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        string[] lines = writer
            .ToString()
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        string[] tableRows = lines
            .Where(static line => line.StartsWith("|", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(tableRows);

        string[] headers = ParseCells(tableRows[0]);
        Assert.Equal(
            new[]
            {
                "benchmark",
                "scenario",
                "mean_ms",
                "min_ms",
                "max_ms",
                "throughput",
                "checksum",
                "relative_speedup"
            },
            headers);

        string[][] dataRows = tableRows.Skip(1).Select(ParseCells).ToArray();
        Assert.Equal(4, dataRows.Length);

        for (int i = 0; i < dataRows.Length; i++)
        {
            Assert.Equal("smoke", dataRows[i][1]);
        }

        string[] scalarRow = Assert.Single(dataRows, static row => row[0] == "track_scalar");
        Assert.Equal("baseline", scalarRow[7]);

        string[] batchRow = Assert.Single(dataRows, static row => row[0] == "track_batch");
        Assert.True(
            batchRow[7].EndsWith("x faster", StringComparison.Ordinal) ||
            batchRow[7].EndsWith("x slower", StringComparison.Ordinal),
            "track_batch should report relative speedup/slowdown against track_scalar.");

        string[] bodyRow = Assert.Single(dataRows, static row => row[0] == "body_batch");
        Assert.Equal("-", bodyRow[7]);

        string[] bogieRow = Assert.Single(dataRows, static row => row[0] == "bogie_batch");
        Assert.Equal("-", bogieRow[7]);
    }

    private static string[] ParseCells(string row)
    {
        Assert.StartsWith("|", row);
        Assert.EndsWith("|", row);

        return row[1..^1]
            .Split('|')
            .Select(static value => value.Trim())
            .ToArray();
    }
}
