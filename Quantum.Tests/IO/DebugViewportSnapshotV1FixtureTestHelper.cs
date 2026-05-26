using System;
using System.Collections.Generic;
using System.IO;
using Quantum.Debug;
using Quantum.IO.DebugViewport.V1;
using Quantum.IO.Fixtures.Csv;

namespace Quantum.Tests;

public sealed class CenterlineFrameCsvFixtureSpec
{
    public CenterlineFrameCsvFixtureSpec(
        string fileName,
        int expectedRowCount,
        double expectedFirstDistance,
        double expectedLastDistance)
    {
        FileName = fileName;
        ExpectedRowCount = expectedRowCount;
        ExpectedFirstDistance = expectedFirstDistance;
        ExpectedLastDistance = expectedLastDistance;
    }

    public string FileName { get; }

    public int ExpectedRowCount { get; }

    public double ExpectedFirstDistance { get; }

    public double ExpectedLastDistance { get; }

    public override string ToString()
    {
        return FileName;
    }
}

public static class DebugViewportSnapshotV1FixtureTestHelper
{
    private static readonly CenterlineFrameCsvFixtureSpec[] Milestone7FixtureSpecs =
    {
        new CenterlineFrameCsvFixtureSpec(
            "Milestone7.synthetic.straight_line.centerline_frames.csv",
            expectedRowCount: 4,
            expectedFirstDistance: 0.0,
            expectedLastDistance: 15.0),
        new CenterlineFrameCsvFixtureSpec(
            "Milestone7.synthetic.simple_hill.centerline_frames.csv",
            expectedRowCount: 5,
            expectedFirstDistance: 0.0,
            expectedLastDistance: 12.8),
        new CenterlineFrameCsvFixtureSpec(
            "Milestone7.synthetic.banked_turn.centerline_frames.csv",
            expectedRowCount: 4,
            expectedFirstDistance: 0.0,
            expectedLastDistance: 15.707963267948966),
        new CenterlineFrameCsvFixtureSpec(
            "Milestone7.synthetic.descending_ascending_curve.centerline_frames.csv",
            expectedRowCount: 4,
            expectedFirstDistance: 0.0,
            expectedLastDistance: 13.5)
    };

    public static IReadOnlyList<CenterlineFrameCsvFixtureSpec> Milestone7Fixtures => Milestone7FixtureSpecs;

    public static IEnumerable<object[]> Milestone7FixtureData
    {
        get
        {
            foreach (CenterlineFrameCsvFixtureSpec fixture in Milestone7FixtureSpecs)
            {
                yield return new object[] { fixture };
            }
        }
    }

    public static string GetFixturePath(CenterlineFrameCsvFixtureSpec fixture)
    {
        if (fixture == null)
        {
            throw new ArgumentNullException(nameof(fixture));
        }

        string path = Path.Combine(AppContext.BaseDirectory, "IO", "Fixtures", fixture.FileName);
        Assert.True(File.Exists(path), $"Synthetic CSV fixture file was not found at '{path}'.");
        return path;
    }

    public static CenterlineFrameCsvFixture ParseFixture(CenterlineFrameCsvFixtureSpec fixture)
    {
        return CenterlineFrameCsvFixtureParser.ParseFile(GetFixturePath(fixture), fixture.FileName);
    }

    public static DebugViewportSnapshotV1Dto BuildSnapshot(CenterlineFrameCsvFixtureSpec fixture)
    {
        return DebugViewportSnapshotV1FromCsvCommand.BuildSnapshot(ParseFixture(fixture));
    }

    public static string CreateTempDirectoryPath(string testName)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks." + testName,
            Guid.NewGuid().ToString("N"));
    }

    public static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
