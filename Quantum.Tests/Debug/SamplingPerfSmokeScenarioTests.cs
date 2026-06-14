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
        Assert.Equal(first.Document.TotalLength, first.Runtime.TotalLength);
        Assert.Equal(second.Document.TotalLength, second.Runtime.TotalLength);

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

        IReadOnlyList<TrainCarTransform> documentBodies = scenario.DocumentProvider.EvaluateCarTransforms(
            scenario.LeadDistance,
            scenario.CarSpacing,
            scenario.CarCount);
        IReadOnlyList<TrainCarTransform> runtimeBodies = scenario.RuntimeProvider.EvaluateCarTransforms(
            scenario.LeadDistance,
            scenario.CarSpacing,
            scenario.CarCount);
        IReadOnlyList<TrainCarWithBogiesTransform> documentBogies = scenario.DocumentProvider.EvaluateTrainWithBogies(
            scenario.LeadDistance,
            scenario.ConsistDefinition);
        IReadOnlyList<TrainCarWithBogiesTransform> runtimeBogies = scenario.RuntimeProvider.EvaluateTrainWithBogies(
            scenario.LeadDistance,
            scenario.ConsistDefinition);
        TrainPoseResult documentPose = scenario.DocumentProvider.EvaluateTrainPose(
            scenario.LeadDistance,
            scenario.ConsistDefinition);
        TrainPoseResult runtimePose = scenario.RuntimeProvider.EvaluateTrainPose(
            scenario.LeadDistance,
            scenario.ConsistDefinition);

        Assert.Equal(scenario.CarCount, documentBodies.Count);
        Assert.Equal(scenario.CarCount, runtimeBodies.Count);
        Assert.Equal(scenario.CarCount, documentBogies.Count);
        Assert.Equal(scenario.CarCount, runtimeBogies.Count);
        Assert.Equal(scenario.CarCount, documentPose.CarsReadOnly.Count);
        Assert.Equal(scenario.CarCount, runtimePose.CarsReadOnly.Count);
    }
}
