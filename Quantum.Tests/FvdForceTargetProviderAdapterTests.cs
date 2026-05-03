using System.Collections.Generic;
using Quantum.FVD;
using Quantum.Math;
using Quantum.Physics;
using Xunit;

namespace Quantum.Tests;

public sealed class FvdForceTargetProviderAdapterTests
{
    private const double ValueTolerance = 1e-9;

    [Fact]
    public void FvdForceTargetProviderAdapter_ReturnsNormalGFromFvd()
    {
        var graph = new FvdGraph(
            new List<FvdControlNode>
            {
                new(0.0, new Vector3d(0.0, 0.0, 0.0), 1.0),
                new(1.0, new Vector3d(10.0, 0.0, 0.0), 1.0)
            },
            degree: 1,
            forceSamples: new List<FvdForceSample>(),
            sections: new List<FvdSectionDefinition>
            {
                new(
                    FvdSectionKind.Force,
                    FvdFunctionDomain.Distance,
                    startX: 0.0,
                    endX: 10.0,
                    new List<FvdSectionFunction>
                    {
                        new(
                            FvdSectionChannel.NormalG,
                            new List<FvdSectionSample>
                            {
                                new(0.0, 1.0),
                                new(10.0, 3.0)
                            })
                    })
            });

        var adapter = new FvdForceTargetProviderAdapter(graph, FvdFunctionDomain.Distance);

        bool returned = adapter.TryGetForceTargets(5.0, out ForceTargets targets);

        Assert.True(returned);
        Assert.InRange(System.Math.Abs(targets.NormalG - 2.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(targets.LateralG), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(targets.RollRateDegPerSec), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdForceTargetProviderAdapter_ReturnsLateralGFromFvd()
    {
        var graph = new FvdGraph(
            new List<FvdControlNode>
            {
                new(0.0, new Vector3d(0.0, 0.0, 0.0), 1.0),
                new(1.0, new Vector3d(10.0, 0.0, 0.0), 1.0)
            },
            degree: 1,
            forceSamples: new List<FvdForceSample>(),
            sections: new List<FvdSectionDefinition>
            {
                new(
                    FvdSectionKind.Force,
                    FvdFunctionDomain.Distance,
                    startX: 0.0,
                    endX: 10.0,
                    new List<FvdSectionFunction>
                    {
                        new(
                            FvdSectionChannel.NormalG,
                            new List<FvdSectionSample>
                            {
                                new(0.0, 1.0),
                                new(10.0, 3.0)
                            }),
                        new(
                            FvdSectionChannel.LateralG,
                            new List<FvdSectionSample>
                            {
                                new(0.0, 2.0),
                                new(10.0, 4.0)
                            })
                    })
            });

        var adapter = new FvdForceTargetProviderAdapter(graph, FvdFunctionDomain.Distance);

        bool returned = adapter.TryGetForceTargets(5.0, out ForceTargets targets);

        Assert.True(returned);
        Assert.InRange(System.Math.Abs(targets.NormalG - 2.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(targets.LateralG - 3.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(targets.RollRateDegPerSec), 0.0, ValueTolerance);
    }

    [Fact]
    public void FvdForceTargetProviderAdapter_ReturnsRollRateFromFvd()
    {
        var graph = new FvdGraph(
            new List<FvdControlNode>
            {
                new(0.0, new Vector3d(0.0, 0.0, 0.0), 1.0),
                new(1.0, new Vector3d(10.0, 0.0, 0.0), 1.0)
            },
            degree: 1,
            forceSamples: new List<FvdForceSample>(),
            sections: new List<FvdSectionDefinition>
            {
                new(
                    FvdSectionKind.Force,
                    FvdFunctionDomain.Distance,
                    startX: 0.0,
                    endX: 10.0,
                    new List<FvdSectionFunction>
                    {
                        new(
                            FvdSectionChannel.NormalG,
                            new List<FvdSectionSample>
                            {
                                new(0.0, 1.0),
                                new(10.0, 3.0)
                            }),
                        new(
                            FvdSectionChannel.RollRateDegPerSec,
                            new List<FvdSectionSample>
                            {
                                new(0.0, 4.0),
                                new(10.0, 8.0)
                            })
                    })
            });

        var adapter = new FvdForceTargetProviderAdapter(graph, FvdFunctionDomain.Distance);

        bool returned = adapter.TryGetForceTargets(5.0, out ForceTargets targets);

        Assert.True(returned);
        Assert.InRange(System.Math.Abs(targets.NormalG - 2.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(targets.LateralG), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(targets.RollRateDegPerSec - 6.0), 0.0, ValueTolerance);
    }
}
