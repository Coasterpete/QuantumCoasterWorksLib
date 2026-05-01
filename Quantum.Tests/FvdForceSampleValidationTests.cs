using System;
using System.Collections.Generic;
using Quantum.FVD;
using Quantum.Math;
using Xunit;

namespace Quantum.Tests;

public class FvdForceSampleValidationTests
{
    [Fact]
    public void FvdGraph_RejectsOutOfRangeUValues_InForceSamples()
    {
        List<FvdControlNode> controlNodes = BuildValidControlNodes();

        var belowZeroSamples = new List<FvdForceSample>
        {
            new FvdForceSample(-0.10, normalG: 1.0, lateralG: 0.0, rollRateDegPerSec: 0.0)
        };

        var aboveOneSamples = new List<FvdForceSample>
        {
            new FvdForceSample(1.10, normalG: 1.0, lateralG: 0.0, rollRateDegPerSec: 0.0)
        };

        Assert.ThrowsAny<Exception>(() => new FvdGraph(controlNodes, degree: 3, belowZeroSamples));
        Assert.ThrowsAny<Exception>(() => new FvdGraph(controlNodes, degree: 3, aboveOneSamples));
    }

    [Fact]
    public void FvdGraph_RejectsUnsortedForceSamples()
    {
        List<FvdControlNode> controlNodes = BuildValidControlNodes();

        var unsortedSamples = new List<FvdForceSample>
        {
            new FvdForceSample(0.00, normalG: 1.0, lateralG: 0.0, rollRateDegPerSec: 0.0),
            new FvdForceSample(0.70, normalG: 1.2, lateralG: 0.1, rollRateDegPerSec: 4.0),
            new FvdForceSample(0.50, normalG: 1.1, lateralG: 0.1, rollRateDegPerSec: 3.0)
        };

        Assert.ThrowsAny<Exception>(() => new FvdGraph(controlNodes, degree: 3, unsortedSamples));
    }

    [Fact]
    public void FvdGraph_RejectsDuplicateUValues_InForceSamples()
    {
        List<FvdControlNode> controlNodes = BuildValidControlNodes();

        var duplicateUSamples = new List<FvdForceSample>
        {
            new FvdForceSample(0.00, normalG: 1.0, lateralG: 0.0, rollRateDegPerSec: 0.0),
            new FvdForceSample(0.50, normalG: 1.2, lateralG: 0.1, rollRateDegPerSec: 4.0),
            new FvdForceSample(0.50, normalG: 1.1, lateralG: 0.1, rollRateDegPerSec: 3.0)
        };

        Assert.ThrowsAny<Exception>(() => new FvdGraph(controlNodes, degree: 3, duplicateUSamples));
    }

    private static List<FvdControlNode> BuildValidControlNodes()
    {
        return new List<FvdControlNode>
        {
            new FvdControlNode(0.00, new Vector3d(0, 0, 0), 1.0),
            new FvdControlNode(0.33, new Vector3d(4, 5, 0), 0.9),
            new FvdControlNode(0.66, new Vector3d(8, -3, 0), 1.2),
            new FvdControlNode(1.00, new Vector3d(12, 0, 0), 1.0)
        };
    }
}
