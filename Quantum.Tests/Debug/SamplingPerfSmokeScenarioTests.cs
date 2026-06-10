using Quantum.Debug;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class SamplingPerfSmokeScenarioTests
{
    [Fact]
    public void CreateDeterministic_ProducesStableScenarioShape()
    {
        SamplingPerfSmokeScenario first = SamplingPerfSmokeScenario.CreateDeterministic();
        SamplingPerfSmokeScenario second = SamplingPerfSmokeScenario.CreateDeterministic();

        Assert.Equal(3, first.Document.Segments.Count);
        Assert.Equal(3, second.Document.Segments.Count);
        Assert.Equal(first.Document.TotalLength, second.Document.TotalLength);
        Assert.Equal(8, first.CarCount);
        Assert.Equal(8, second.CarCount);
        Assert.Equal(6.0, first.CarSpacing);
        Assert.Equal(6.0, second.CarSpacing);
        Assert.Equal(150.0, first.LeadDistance);
        Assert.Equal(150.0, second.LeadDistance);

        Assert.Equal(512, first.Distances.Length);
        Assert.Equal(first.Distances.Length, second.Distances.Length);

        for (int i = 0; i < first.Distances.Length; i++)
        {
            Assert.Equal(first.Distances[i], second.Distances[i]);
        }
    }

    [Fact]
    public void CreateDeterministic_ProducesRangeSafeTrainInputs()
    {
        SamplingPerfSmokeScenario scenario = SamplingPerfSmokeScenario.CreateDeterministic();
        double trackLength = scenario.Document.TotalLength;

        Assert.InRange(scenario.LeadDistance, 0.0, trackLength);

        IReadOnlyList<TrainCarTransform> bodies = scenario.Provider.EvaluateCarTransforms(
            scenario.LeadDistance,
            scenario.CarSpacing,
            scenario.CarCount);
        IReadOnlyList<TrainCarWithBogiesTransform> bogies = scenario.Provider.EvaluateTrainWithBogies(
            scenario.LeadDistance,
            scenario.ConsistDefinition);

        Assert.Equal(scenario.CarCount, bodies.Count);
        Assert.Equal(scenario.CarCount, bogies.Count);
    }
}
