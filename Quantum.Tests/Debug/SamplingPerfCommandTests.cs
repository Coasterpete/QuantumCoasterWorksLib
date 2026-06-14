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
        Assert.Equal(8, dataRows.Length);

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

        string[] bodyDocument = Assert.Single(dataRows, static row => row[0] == "body_batch_document");
        string[] bodyRuntime = Assert.Single(dataRows, static row => row[0] == "body_batch_runtime");
        string[] bogieDocument = Assert.Single(dataRows, static row => row[0] == "bogie_batch_document");
        string[] bogieRuntime = Assert.Single(dataRows, static row => row[0] == "bogie_batch_runtime");
        string[] poseDocument = Assert.Single(dataRows, static row => row[0] == "train_pose_document");
        string[] poseRuntime = Assert.Single(dataRows, static row => row[0] == "train_pose_runtime");

        Assert.Equal("-", bodyDocument[7]);
        Assert.Equal("-", bogieDocument[7]);
        Assert.Equal("-", poseDocument[7]);
        Assert.Equal(bodyDocument[6], bodyRuntime[6]);
        Assert.Equal(bogieDocument[6], bogieRuntime[6]);
        Assert.Equal(poseDocument[6], poseRuntime[6]);
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
