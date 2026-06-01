using System;
using System.Collections.Generic;
using System.Numerics;
using Quantum.Math;
using Quantum.Track;
using ExportTrackFrame = Quantum.Track.TrackFrame;

namespace Quantum.Tests;

public sealed class TrainCarTransformProviderTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void GetCarTransforms_ReturnsRequestedCarCount()
    {
        TrackDocument document = BuildStraightTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance: 12.0,
            carSpacing: 2.0,
            carCount: 4);

        Assert.Equal(4, cars.Count);
    }

    [Fact]
    public void EvaluateCarTransforms_MatchesGetCarTransforms()
    {
        TrackDocument document = BuildSplineTrack(length: 28.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        const double leadDistance = 16.5;
        const double carSpacing = 2.25;
        const int carCount = 5;

        IReadOnlyList<TrainCarTransform> expected = provider.GetCarTransforms(
            leadDistance: leadDistance,
            carSpacing: carSpacing,
            carCount: carCount);
        IReadOnlyList<TrainCarTransform> actual = provider.EvaluateCarTransforms(
            leadDistance: leadDistance,
            carSpacing: carSpacing,
            carCount: carCount);

        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            AssertTrainCarTransformNear(expected[i], actual[i]);
        }
    }

    [Fact]
    public void GetCarTransforms_UsesExpectedSpacingDistances()
    {
        TrackDocument document = BuildStraightTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance: 10.0,
            carSpacing: 2.5,
            carCount: 4);

        AssertDoubleNear(10.0, cars[0].Distance);
        AssertDoubleNear(7.5, cars[1].Distance);
        AssertDoubleNear(5.0, cars[2].Distance);
        AssertDoubleNear(2.5, cars[3].Distance);
    }

    [Fact]
    public void GetCarTransforms_ProducesFiniteMatrices()
    {
        TrackDocument document = BuildSplineTrack(length: 15.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance: 12.0,
            carSpacing: 1.75,
            carCount: 5);

        for (int i = 0; i < cars.Count; i++)
        {
            AssertFiniteMatrix(cars[i].Matrix);
        }
    }

    [Fact]
    public void GetCarTransforms_FirstCarFrameMatchesLeadFrame()
    {
        TrackDocument document = BuildSplineTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        const double leadDistance = 8.25;

        ExportTrackFrame expectedLeadFrame = evaluator.EvaluateFrameAtDistance(leadDistance);
        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance,
            carSpacing: 1.5,
            carCount: 3);

        ExportTrackFrame actualLeadFrame = cars[0].Frame;

        AssertVectorNear(expectedLeadFrame.Position, actualLeadFrame.Position);
        AssertVectorNear(expectedLeadFrame.Tangent, actualLeadFrame.Tangent);
        AssertVectorNear(expectedLeadFrame.Normal, actualLeadFrame.Normal);
        AssertVectorNear(expectedLeadFrame.Binormal, actualLeadFrame.Binormal);
    }

    [Fact]
    public void GetCarTransforms_BodyMatrixMatchesFrameMatrixPolicy()
    {
        TrackDocument document = BuildSplineTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarTransform> cars = provider.GetCarTransforms(
            leadDistance: 10.0,
            carSpacing: 2.0,
            carCount: 4);

        for (int i = 0; i < cars.Count; i++)
        {
            Matrix4x4 expectedMatrix = cars[i].Frame.ToMatrix4x4();
            AssertMatrixNear(expectedMatrix, cars[i].Matrix);
        }
    }

    [Fact]
    public void GetCarTransforms_WhenAnyCarDistanceIsOutOfRange_ThrowsWithClearMessage()
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetCarTransforms(
            leadDistance: 1.0,
            carSpacing: 2.0,
            carCount: 2));

        Assert.Contains("car 1", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(6.1)]
    public void GetCarTransforms_WhenLeadDistanceIsOutOfRange_ThrowsWithClearMessage(double invalidLeadDistance)
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetCarTransforms(
            leadDistance: invalidLeadDistance,
            carSpacing: 1.0,
            carCount: 1));

        Assert.Contains("lead car distance", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateTrainWithBogies_ReturnsRequestedCarCount()
    {
        TrackDocument document = BuildStraightTrack(length: 30.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
            leadDistance: 14.0,
            carCount: 4,
            carSpacing: 2.0,
            bogieSpacing: 1.5);

        Assert.Equal(4, cars.Count);
    }

    [Fact]
    public void EvaluateTrainWithBogies_DefinitionOverload_MatchesPrimitiveOverload()
    {
        TrackDocument document = BuildSplineTrack(length: 36.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        const double leadDistance = 20.0;
        var definition = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.25,
            carLength: 3.0,
            carWidth: 1.4,
            carHeight: 1.8,
            bogieSpacing: 2.0);

        IReadOnlyList<TrainCarWithBogiesTransform> expected = provider.EvaluateTrainWithBogies(
            leadDistance: leadDistance,
            carCount: definition.CarCount,
            carSpacing: definition.CarSpacing,
            bogieSpacing: definition.BogieSpacing);
        IReadOnlyList<TrainCarWithBogiesTransform> actual = provider.EvaluateTrainWithBogies(
            leadDistance: leadDistance,
            definition: definition);

        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            AssertTrainCarWithBogiesNear(expected[i], actual[i]);
        }
    }

    [Fact]
    public void EvaluateTrainWithBogies_UsesExpectedBodyAndBogieDistances()
    {
        TrackDocument document = BuildStraightTrack(length: 30.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
            leadDistance: 15.0,
            carCount: 3,
            carSpacing: 3.0,
            bogieSpacing: 2.0);

        AssertDoubleNear(15.0, cars[0].Body.Distance);
        AssertDoubleNear(16.0, cars[0].FrontBogie.Distance);
        AssertDoubleNear(14.0, cars[0].RearBogie.Distance);

        AssertDoubleNear(12.0, cars[1].Body.Distance);
        AssertDoubleNear(13.0, cars[1].FrontBogie.Distance);
        AssertDoubleNear(11.0, cars[1].RearBogie.Distance);

        AssertDoubleNear(9.0, cars[2].Body.Distance);
        AssertDoubleNear(10.0, cars[2].FrontBogie.Distance);
        AssertDoubleNear(8.0, cars[2].RearBogie.Distance);
    }

    [Fact]
    public void EvaluateTrainWithBogies_BogieFramesMatchTrackEvaluator()
    {
        TrackDocument document = BuildSplineTrack(length: 30.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        const double leadDistance = 11.0;
        const double bogieSpacing = 2.4;

        IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
            leadDistance: leadDistance,
            carCount: 1,
            carSpacing: 2.0,
            bogieSpacing: bogieSpacing);

        TrainCarWithBogiesTransform car = cars[0];
        double expectedFrontDistance = leadDistance + (bogieSpacing * 0.5);
        double expectedRearDistance = leadDistance - (bogieSpacing * 0.5);

        ExportTrackFrame expectedFrontFrame = evaluator.EvaluateFrameAtDistance(expectedFrontDistance);
        ExportTrackFrame expectedRearFrame = evaluator.EvaluateFrameAtDistance(expectedRearDistance);

        AssertVectorNear(expectedFrontFrame.Position, car.FrontBogie.Frame.Position);
        AssertVectorNear(expectedFrontFrame.Tangent, car.FrontBogie.Frame.Tangent);
        AssertVectorNear(expectedFrontFrame.Normal, car.FrontBogie.Frame.Normal);
        AssertVectorNear(expectedFrontFrame.Binormal, car.FrontBogie.Frame.Binormal);

        AssertVectorNear(expectedRearFrame.Position, car.RearBogie.Frame.Position);
        AssertVectorNear(expectedRearFrame.Tangent, car.RearBogie.Frame.Tangent);
        AssertVectorNear(expectedRearFrame.Normal, car.RearBogie.Frame.Normal);
        AssertVectorNear(expectedRearFrame.Binormal, car.RearBogie.Frame.Binormal);
    }

    [Fact]
    public void EvaluateTrainWithBogies_BogieMatricesMatchFrameMatrices()
    {
        TrackDocument document = BuildSplineTrack(length: 24.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
            leadDistance: 16.0,
            carCount: 3,
            carSpacing: 2.5,
            bogieSpacing: 1.2);

        for (int i = 0; i < cars.Count; i++)
        {
            TrainCarWithBogiesTransform car = cars[i];

            Matrix4x4d expectedFrontMatrix = Matrix4x4d.FromMatrix4x4(car.FrontBogie.Frame.ToMatrix4x4());
            Matrix4x4d expectedRearMatrix = Matrix4x4d.FromMatrix4x4(car.RearBogie.Frame.ToMatrix4x4());

            AssertMatrixNear(expectedFrontMatrix, car.FrontBogie.Matrix);
            AssertMatrixNear(expectedRearMatrix, car.RearBogie.Matrix);
        }
    }

    [Fact]
    public void EvaluateTrainWithBogies_OnCurvedTrack_ProducesFiniteBogieTransforms()
    {
        TrackDocument document = BuildSplineTrack(length: 25.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        IReadOnlyList<TrainCarWithBogiesTransform> cars = provider.EvaluateTrainWithBogies(
            leadDistance: 18.0,
            carCount: 5,
            carSpacing: 2.2,
            bogieSpacing: 1.6);

        for (int i = 0; i < cars.Count; i++)
        {
            TrainCarWithBogiesTransform car = cars[i];

            AssertFiniteVector(car.FrontBogie.Frame.Position);
            AssertFiniteVector(car.FrontBogie.Frame.Tangent);
            AssertFiniteVector(car.FrontBogie.Frame.Normal);
            AssertFiniteVector(car.FrontBogie.Frame.Binormal);
            AssertFiniteMatrix(car.FrontBogie.Matrix);

            AssertFiniteVector(car.RearBogie.Frame.Position);
            AssertFiniteVector(car.RearBogie.Frame.Tangent);
            AssertFiniteVector(car.RearBogie.Frame.Normal);
            AssertFiniteVector(car.RearBogie.Frame.Binormal);
            AssertFiniteMatrix(car.RearBogie.Matrix);
        }
    }

    [Fact]
    public void EvaluateTrainWithBogies_WhenBogieSpacingIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        TrackDocument document = BuildStraightTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        double[] invalidBogieSpacings = { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.1 };

        foreach (double invalidBogieSpacing in invalidBogieSpacings)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.EvaluateTrainWithBogies(
                leadDistance: 10.0,
                carCount: 1,
                carSpacing: 1.0,
                bogieSpacing: invalidBogieSpacing));

            Assert.Equal("bogieSpacing", exception.ParamName);
            Assert.Contains("bogie spacing", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EvaluateTrainWithBogies_WhenFrontBogieDistanceIsOutOfRange_ThrowsWithClearMessage()
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.EvaluateTrainWithBogies(
            leadDistance: 5.5,
            carCount: 1,
            carSpacing: 1.0,
            bogieSpacing: 1.5));

        Assert.Contains("front bogie", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateTrainWithBogies_WhenFrontAndRearBogieDistancesAreOutOfRange_ThrowsFrontBogieFirst()
    {
        TrackDocument document = BuildStraightTrack(length: 1.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.EvaluateTrainWithBogies(
            leadDistance: 0.5,
            carCount: 1,
            carSpacing: 1.0,
            bogieSpacing: 3.0));

        Assert.Contains("front bogie", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rear bogie", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateTrainWithBogies_WhenRearBogieDistanceIsOutOfRange_ThrowsWithClearMessage()
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.EvaluateTrainWithBogies(
            leadDistance: 0.5,
            carCount: 1,
            carSpacing: 1.0,
            bogieSpacing: 2.0));

        Assert.Contains("rear bogie", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(6.1)]
    public void EvaluateTrainWithBogies_WhenLeadDistanceIsOutOfRange_ThrowsWithClearMessage(double invalidLeadDistance)
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.EvaluateTrainWithBogies(
            leadDistance: invalidLeadDistance,
            carCount: 1,
            carSpacing: 1.0,
            bogieSpacing: 1.0));

        Assert.Contains("lead car distance", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateTrainWithBogiesAndWheels_WhenWheelLayoutIsMissing_ThrowsInvalidOperationException()
    {
        TrackDocument document = BuildStraightTrack(length: 30.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 2.0,
            carLength: 4.0,
            carWidth: 1.4,
            carHeight: 1.8,
            bogieSpacing: 2.0);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: 12.0,
            definition: definition));

        Assert.Contains("wheel layout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateTrainWithBogiesAndWheels_ReturnsRequestedCarCount()
    {
        TrackDocument document = BuildStraightTrack(length: 40.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 4,
            wheelCountPerBogie: 4);

        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> cars = provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: 20.0,
            definition: definition);

        Assert.Equal(4, cars.Count);
    }

    [Fact]
    public void EvaluateTrainWithBogiesAndWheels_ReturnsRequestedWheelCountPerBogie()
    {
        TrackDocument document = BuildStraightTrack(length: 40.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 6);

        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> cars = provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: 18.0,
            definition: definition);

        for (int i = 0; i < cars.Count; i++)
        {
            Assert.Equal(6, cars[i].FrontBogie.Wheels.Length);
            Assert.Equal(6, cars[i].RearBogie.Wheels.Length);
        }
    }

    [Fact]
    public void EvaluateTrainWithBogiesAndWheels_WheelTransformsPreserveIndices()
    {
        TrackDocument document = BuildSplineTrack(length: 42.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 5);

        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> cars = provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: 22.0,
            definition: definition);

        for (int carIndex = 0; carIndex < cars.Count; carIndex++)
        {
            TrainCarWithBogiesAndWheelsTransform car = cars[carIndex];
            AssertWheelIndicesMatchBogie(car.FrontBogie);
            AssertWheelIndicesMatchBogie(car.RearBogie);
        }
    }

    [Fact]
    public void EvaluateTrainWithBogiesAndWheels_WheelFrameAndMatrixMatchBogieFrameAndMatrix()
    {
        TrackDocument document = BuildSplineTrack(length: 32.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 2,
            wheelCountPerBogie: 4);

        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> cars = provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: 17.0,
            definition: definition);

        for (int i = 0; i < cars.Count; i++)
        {
            AssertWheelFrameAndMatrixMatchBogie(cars[i].FrontBogie);
            AssertWheelFrameAndMatrixMatchBogie(cars[i].RearBogie);
        }
    }

    [Fact]
    public void EvaluateTrainWithBogiesAndWheels_LocalOffsetsAreFiniteAndDeterministic()
    {
        TrackDocument document = BuildSplineTrack(length: 36.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 4,
            wheelWidth: 0.6,
            axleSpacing: 1.2);

        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> expected = provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: 20.0,
            definition: definition);
        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> actual = provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: 20.0,
            definition: definition);

        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            AssertWheelOffsetsFiniteAndDeterministic(expected[i].FrontBogie.Wheels, actual[i].FrontBogie.Wheels);
            AssertWheelOffsetsFiniteAndDeterministic(expected[i].RearBogie.Wheels, actual[i].RearBogie.Wheels);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrain_WhenDefinitionIsNull_ThrowsArgumentNullException()
    {
        TrackDocument document = BuildStraightTrack(length: 20.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => provider.EvaluateArticulatedTrain(
            leadDistance: 10.0,
            definition: null!));

        Assert.Equal("definition", exception.ParamName);
    }

    [Fact]
    public void EvaluateArticulatedTrain_ReturnsRequestedCarCount()
    {
        TrackDocument document = BuildStraightTrack(length: 40.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.5,
            carLength: 5.0,
            carWidth: 1.4,
            carHeight: 1.8,
            bogieSpacing: 3.0);

        IReadOnlyList<ArticulatedTrainCarTransform> cars = provider.EvaluateArticulatedTrain(
            leadDistance: 20.0,
            definition: definition);

        Assert.Equal(definition.CarCount, cars.Count);
    }

    [Fact]
    public void EvaluateArticulatedTrain_FrontAndRearBogiesMatchEvaluateTrainWithBogies()
    {
        TrackDocument document = BuildSplineTrack(length: 36.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 3,
            carSpacing: 2.0,
            carLength: 6.0,
            carWidth: 1.5,
            carHeight: 1.9,
            bogieSpacing: 4.0);
        const double leadDistance = 18.0;

        IReadOnlyList<TrainCarWithBogiesTransform> bogieOnly = provider.EvaluateTrainWithBogies(
            leadDistance: leadDistance,
            definition: definition);
        IReadOnlyList<ArticulatedTrainCarTransform> articulated = provider.EvaluateArticulatedTrain(
            leadDistance: leadDistance,
            definition: definition);

        Assert.Equal(bogieOnly.Count, articulated.Count);

        for (int i = 0; i < articulated.Count; i++)
        {
            AssertTrainCarTransformNear(bogieOnly[i].Body, articulated[i].OriginalBody);
            AssertBogieTransformNear(bogieOnly[i].FrontBogie, articulated[i].FrontBogie);
            AssertBogieTransformNear(bogieOnly[i].RearBogie, articulated[i].RearBogie);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrain_CenterPositionIsMidpointOfFrontAndRearBogies()
    {
        TrackDocument document = BuildSplineTrack(length: 28.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 3,
            carSpacing: 2.1,
            carLength: 6.0,
            carWidth: 1.4,
            carHeight: 1.8,
            bogieSpacing: 3.6);

        IReadOnlyList<ArticulatedTrainCarTransform> cars = provider.EvaluateArticulatedTrain(
            leadDistance: 16.0,
            definition: definition);

        for (int i = 0; i < cars.Count; i++)
        {
            ArticulatedTrainCarTransform car = cars[i];
            Vector3d expectedMidpoint = (car.FrontBogie.Frame.Position + car.RearBogie.Frame.Position) * 0.5;
            AssertVectorNear(expectedMidpoint, car.ArticulatedFrame.Position);
            AssertDoubleNear(car.OriginalBody.Distance, car.CenterDistance);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrain_TangentPointsFromRearBogieToFrontBogie()
    {
        TrackDocument document = BuildSplineTrack(length: 32.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 2.5,
            carLength: 7.0,
            carWidth: 1.6,
            carHeight: 1.9,
            bogieSpacing: 4.5);

        IReadOnlyList<ArticulatedTrainCarTransform> cars = provider.EvaluateArticulatedTrain(
            leadDistance: 18.0,
            definition: definition);

        for (int i = 0; i < cars.Count; i++)
        {
            ArticulatedTrainCarTransform car = cars[i];
            Vector3d expectedDirection = (car.FrontBogie.Frame.Position - car.RearBogie.Frame.Position).Normalized();
            AssertVectorNear(expectedDirection, car.ArticulatedFrame.Tangent);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrain_ArticulatedFrameIsFiniteAndOrthonormal()
    {
        TrackDocument document = BuildSplineTrack(length: 34.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 2.0,
            carLength: 6.5,
            carWidth: 1.5,
            carHeight: 1.8,
            bogieSpacing: 4.2);

        IReadOnlyList<ArticulatedTrainCarTransform> cars = provider.EvaluateArticulatedTrain(
            leadDistance: 20.0,
            definition: definition);

        for (int i = 0; i < cars.Count; i++)
        {
            AssertTrackFrameFinite(cars[i].ArticulatedFrame);
            AssertTrackFrameOrthonormal(cars[i].ArticulatedFrame);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrain_ArticulatedMatrixMatchesArticulatedFrame()
    {
        TrackDocument document = BuildSplineTrack(length: 30.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 3,
            carSpacing: 2.3,
            carLength: 6.0,
            carWidth: 1.4,
            carHeight: 1.9,
            bogieSpacing: 3.8);

        IReadOnlyList<ArticulatedTrainCarTransform> cars = provider.EvaluateArticulatedTrain(
            leadDistance: 17.0,
            definition: definition);

        for (int i = 0; i < cars.Count; i++)
        {
            Matrix4x4d expectedMatrix = Matrix4x4d.FromMatrix4x4(cars[i].ArticulatedFrame.ToMatrix4x4());
            AssertMatrixNear(expectedMatrix, cars[i].ArticulatedMatrix);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrain_OnStraightTrack_MatchesOriginalBodyFrame()
    {
        TrackDocument document = BuildStraightTrack(length: 50.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 4,
            carSpacing: 3.0,
            carLength: 7.0,
            carWidth: 1.5,
            carHeight: 1.8,
            bogieSpacing: 4.0);

        IReadOnlyList<ArticulatedTrainCarTransform> cars = provider.EvaluateArticulatedTrain(
            leadDistance: 24.0,
            definition: definition);

        for (int i = 0; i < cars.Count; i++)
        {
            AssertTrackFrameNear(cars[i].OriginalBody.Frame, cars[i].ArticulatedFrame);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrain_OnCurvedTrack_DiffersFromSinglePointBodyFrame()
    {
        TrackDocument document = BuildSplineTrack(length: 28.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 1,
            carSpacing: 2.0,
            carLength: 8.0,
            carWidth: 1.6,
            carHeight: 1.9,
            bogieSpacing: 6.0);

        IReadOnlyList<ArticulatedTrainCarTransform> cars = provider.EvaluateArticulatedTrain(
            leadDistance: 14.0,
            definition: definition);
        ArticulatedTrainCarTransform car = cars[0];

        double positionDelta = (car.ArticulatedFrame.Position - car.OriginalBody.Frame.Position).Length;
        double tangentAlignmentDelta = System.Math.Abs(Vector3d.Dot(car.ArticulatedFrame.Tangent, car.OriginalBody.Frame.Tangent) - 1.0);

        Assert.True(positionDelta > 1e-6 || tangentAlignmentDelta > 1e-6);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(6.1)]
    public void EvaluateArticulatedTrain_WhenLeadDistanceIsOutOfRange_ThrowsWithClearMessage(double invalidLeadDistance)
    {
        TrackDocument document = BuildStraightTrack(length: 6.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 1,
            carSpacing: 1.0,
            carLength: 4.0,
            carWidth: 1.4,
            carHeight: 1.8,
            bogieSpacing: 2.0);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => provider.EvaluateArticulatedTrain(
            leadDistance: invalidLeadDistance,
            definition: definition));

        Assert.Contains("lead car distance", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateArticulatedTrainWithWheels_WhenWheelLayoutIsMissing_ThrowsInvalidOperationException()
    {
        TrackDocument document = BuildStraightTrack(length: 30.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 2.0,
            carLength: 4.0,
            carWidth: 1.4,
            carHeight: 1.8,
            bogieSpacing: 2.0);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: 12.0,
            definition: definition));

        Assert.Contains("wheel layout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateArticulatedTrainWithWheels_ReturnsRequestedCarCount()
    {
        TrackDocument document = BuildStraightTrack(length: 50.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 4,
            wheelCountPerBogie: 4);

        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: 24.0,
            definition: definition);

        Assert.Equal(definition.CarCount, cars.Count);
    }

    [Fact]
    public void EvaluateArticulatedTrainWithWheels_BodyMatchesEvaluateArticulatedTrainOutput()
    {
        TrackDocument document = BuildSplineTrack(length: 42.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 6);
        const double leadDistance = 22.0;

        IReadOnlyList<ArticulatedTrainCarTransform> expectedBodies = provider.EvaluateArticulatedTrain(
            leadDistance: leadDistance,
            definition: definition);
        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: leadDistance,
            definition: definition);

        Assert.Equal(expectedBodies.Count, cars.Count);

        for (int i = 0; i < cars.Count; i++)
        {
            AssertArticulatedTrainCarTransformNear(expectedBodies[i], cars[i].Body);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrainWithWheels_BogiesAndWheelsMatchEvaluateTrainWithBogiesAndWheelsOutput()
    {
        TrackDocument document = BuildSplineTrack(length: 48.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 5);
        const double leadDistance = 23.5;

        IReadOnlyList<TrainCarWithBogiesAndWheelsTransform> expectedWheelHierarchy = provider.EvaluateTrainWithBogiesAndWheels(
            leadDistance: leadDistance,
            definition: definition);
        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: leadDistance,
            definition: definition);

        Assert.Equal(expectedWheelHierarchy.Count, cars.Count);

        for (int i = 0; i < cars.Count; i++)
        {
            AssertBogieTransformNear(expectedWheelHierarchy[i].FrontBogie.Bogie, cars[i].FrontBogie.Bogie);
            AssertBogieTransformNear(expectedWheelHierarchy[i].RearBogie.Bogie, cars[i].RearBogie.Bogie);
            Assert.Equal(expectedWheelHierarchy[i].FrontBogie.Wheels.Length, cars[i].FrontBogie.Wheels.Length);
            Assert.Equal(expectedWheelHierarchy[i].RearBogie.Wheels.Length, cars[i].RearBogie.Wheels.Length);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrainWithWheels_PreservesCarBogieAndWheelIndices()
    {
        TrackDocument document = BuildSplineTrack(length: 44.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 4);

        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: 21.0,
            definition: definition);

        for (int carIndex = 0; carIndex < cars.Count; carIndex++)
        {
            ArticulatedTrainCarWithWheelsTransform car = cars[carIndex];

            Assert.Equal(carIndex, car.Body.OriginalBody.CarIndex);
            Assert.Equal(car.Body.OriginalBody.CarIndex, car.FrontBogie.Bogie.CarIndex);
            Assert.Equal(car.Body.OriginalBody.CarIndex, car.RearBogie.Bogie.CarIndex);
            Assert.Equal(0, car.FrontBogie.Bogie.BogieIndex);
            Assert.Equal(1, car.RearBogie.Bogie.BogieIndex);
            AssertWheelIndicesMatchBogie(car.FrontBogie);
            AssertWheelIndicesMatchBogie(car.RearBogie);
        }
    }

    [Fact]
    public void EvaluateArticulatedTrainWithWheels_WheelsReadOnlyMatchesWheelsArray()
    {
        TrackDocument document = BuildSplineTrack(length: 44.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 4);

        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: 21.0,
            definition: definition);

        for (int carIndex = 0; carIndex < cars.Count; carIndex++)
        {
            TrainBogieWithWheelsTransform frontBogie = cars[carIndex].FrontBogie;
            TrainBogieWithWheelsTransform rearBogie = cars[carIndex].RearBogie;

            Assert.Equal(frontBogie.Wheels.Length, frontBogie.WheelsReadOnly.Count);
            Assert.Equal(rearBogie.Wheels.Length, rearBogie.WheelsReadOnly.Count);

            for (int wheelIndex = 0; wheelIndex < frontBogie.Wheels.Length; wheelIndex++)
            {
                Assert.Equal(frontBogie.Wheels[wheelIndex], frontBogie.WheelsReadOnly[wheelIndex]);
            }

            for (int wheelIndex = 0; wheelIndex < rearBogie.Wheels.Length; wheelIndex++)
            {
                Assert.Equal(rearBogie.Wheels[wheelIndex], rearBogie.WheelsReadOnly[wheelIndex]);
            }
        }
    }

    [Fact]
    public void TrainBogieWithWheelsTransform_MutatingSourceWheelsArrayDoesNotAffectStoredSnapshot()
    {
        TrackDocument document = BuildSplineTrack(length: 44.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 4);

        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: 21.0,
            definition: definition);

        TrainBogieWithWheelsTransform sourceBogie = cars[0].FrontBogie;
        WheelTransform[] sourceWheels = sourceBogie.Wheels;
        WheelTransform expectedFirstWheel = sourceWheels[0];
        var snapshotBogie = new TrainBogieWithWheelsTransform(sourceBogie.Bogie, sourceWheels);

        sourceWheels[0] = sourceWheels[1];

        AssertWheelTransformNear(expectedFirstWheel, snapshotBogie.Wheels[0]);
        AssertWheelTransformNear(expectedFirstWheel, snapshotBogie.WheelsReadOnly[0]);
    }

    [Fact]
    public void TrainBogieWithWheelsTransform_MutatingReturnedWheelsArrayDoesNotAffectStoredSnapshot()
    {
        TrackDocument document = BuildSplineTrack(length: 44.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 4);

        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: 21.0,
            definition: definition);

        TrainBogieWithWheelsTransform bogie = cars[0].FrontBogie;
        WheelTransform expectedFirstWheel = bogie.WheelsReadOnly[0];
        WheelTransform[] exposedWheels = bogie.Wheels;

        exposedWheels[0] = exposedWheels[1];

        Assert.Equal(bogie.Wheels.Length, bogie.WheelsReadOnly.Count);
        AssertWheelTransformNear(expectedFirstWheel, bogie.Wheels[0]);
        AssertWheelTransformNear(expectedFirstWheel, bogie.WheelsReadOnly[0]);
    }

    [Fact]
    public void EvaluateTrainPose_StoresLeadDistanceAndDefinitionReference()
    {
        TrackDocument document = BuildStraightTrack(length: 40.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 3,
            wheelCountPerBogie: 4);
        const double leadDistance = 18.25;

        TrainPoseResult result = provider.EvaluateTrainPose(
            leadDistance: leadDistance,
            definition: definition);

        AssertDoubleNear(leadDistance, result.LeadDistance);
        Assert.Same(definition, result.Definition);
    }

    [Fact]
    public void EvaluateTrainPose_MultiSegmentTrack_FrameDistancesUseGlobalStationDistances()
    {
        TrackDocument document = BuildTwoSegmentSplineTrack();
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 2,
            wheelCountPerBogie: 4);
        const double leadDistance = 12.0;

        TrainPoseResult result = provider.EvaluateTrainPose(
            leadDistance: leadDistance,
            definition: definition);

        Assert.Equal(definition.CarCount, result.CarsReadOnly.Count);
        AssertTrainPoseFrameDistances(
            result.CarsReadOnly[0],
            bodyDistance: 12.0,
            frontBogieDistance: 13.0,
            rearBogieDistance: 11.0);
        AssertTrainPoseFrameDistances(
            result.CarsReadOnly[1],
            bodyDistance: 10.0,
            frontBogieDistance: 11.0,
            rearBogieDistance: 9.0);
    }

    [Fact]
    public void EvaluateTrainPose_CarsMatchEvaluateArticulatedTrainWithWheels()
    {
        TrackDocument document = BuildSplineTrack(length: 52.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 4,
            wheelCountPerBogie: 6);
        const double leadDistance = 26.5;

        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> expectedCars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: leadDistance,
            definition: definition);
        TrainPoseResult result = provider.EvaluateTrainPose(
            leadDistance: leadDistance,
            definition: definition);

        Assert.Equal(expectedCars.Count, result.Cars.Length);

        for (int i = 0; i < expectedCars.Count; i++)
        {
            AssertArticulatedTrainCarWithWheelsTransformNear(expectedCars[i], result.Cars[i]);
        }
    }

    [Fact]
    public void EvaluateTrainPose_CarsReadOnlyMatchesCarsArray()
    {
        TrackDocument document = BuildSplineTrack(length: 52.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 4,
            wheelCountPerBogie: 6);
        const double leadDistance = 26.5;

        TrainPoseResult result = provider.EvaluateTrainPose(
            leadDistance: leadDistance,
            definition: definition);

        Assert.Equal(result.Cars.Length, result.CarsReadOnly.Count);

        for (int i = 0; i < result.Cars.Length; i++)
        {
            AssertArticulatedTrainCarWithWheelsTransformNear(result.Cars[i], result.CarsReadOnly[i]);
        }
    }

    [Fact]
    public void TrainPoseResult_WhenDefinitionIsNull_ThrowsArgumentNullException()
    {
        var cars = Array.Empty<ArticulatedTrainCarWithWheelsTransform>();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => new TrainPoseResult(leadDistance: 0.0, definition: null!, cars: cars));

        Assert.Equal("definition", exception.ParamName);
    }

    [Fact]
    public void TrainPoseResult_WhenCarsIsNull_ThrowsArgumentNullException()
    {
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 1,
            wheelCountPerBogie: 4);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => new TrainPoseResult(leadDistance: 0.0, definition: definition, cars: null!));

        Assert.Equal("cars", exception.ParamName);
    }

    [Fact]
    public void TrainPoseResult_MutatingSourceCarsArrayDoesNotAffectStoredSnapshot()
    {
        TrackDocument document = BuildSplineTrack(length: 52.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 4,
            wheelCountPerBogie: 6);
        const double leadDistance = 26.5;

        IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> evaluatedCars = provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: leadDistance,
            definition: definition);
        var sourceCars = new ArticulatedTrainCarWithWheelsTransform[evaluatedCars.Count];

        for (int i = 0; i < evaluatedCars.Count; i++)
        {
            sourceCars[i] = evaluatedCars[i];
        }

        ArticulatedTrainCarWithWheelsTransform expectedFirstCar = sourceCars[0];
        var result = new TrainPoseResult(leadDistance, definition, sourceCars);

        sourceCars[0] = sourceCars[1];

        AssertArticulatedTrainCarWithWheelsTransformNear(expectedFirstCar, result.Cars[0]);
        AssertArticulatedTrainCarWithWheelsTransformNear(expectedFirstCar, result.CarsReadOnly[0]);
    }

    [Fact]
    public void EvaluateTrainPose_MutatingReturnedCarsArrayDoesNotAffectStoredSnapshot()
    {
        TrackDocument document = BuildSplineTrack(length: 52.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        TrainConsistDefinition definition = BuildConsistDefinitionWithWheels(
            carCount: 4,
            wheelCountPerBogie: 6);
        const double leadDistance = 26.5;

        TrainPoseResult result = provider.EvaluateTrainPose(
            leadDistance: leadDistance,
            definition: definition);

        ArticulatedTrainCarWithWheelsTransform expectedFirstCar = result.CarsReadOnly[0];
        ArticulatedTrainCarWithWheelsTransform[] exposedCars = result.Cars;
        exposedCars[0] = exposedCars[1];

        ArticulatedTrainCarWithWheelsTransform[] currentCars = result.Cars;
        Assert.Equal(currentCars.Length, result.CarsReadOnly.Count);
        AssertArticulatedTrainCarWithWheelsTransformNear(expectedFirstCar, currentCars[0]);
        AssertArticulatedTrainCarWithWheelsTransformNear(expectedFirstCar, result.CarsReadOnly[0]);

        for (int i = 0; i < currentCars.Length; i++)
        {
            AssertArticulatedTrainCarWithWheelsTransformNear(currentCars[i], result.CarsReadOnly[i]);
        }
    }

    [Fact]
    public void EvaluateTrainPose_WhenWheelLayoutIsMissing_ThrowsSameExceptionAsEvaluateArticulatedTrainWithWheels()
    {
        TrackDocument document = BuildStraightTrack(length: 30.0);
        var evaluator = new TrackEvaluator(document);
        var provider = new TrainCarTransformProvider(evaluator);
        var definition = new TrainConsistDefinition(
            carCount: 2,
            carSpacing: 2.0,
            carLength: 4.0,
            carWidth: 1.4,
            carHeight: 1.8,
            bogieSpacing: 2.0);
        const double leadDistance = 12.0;

        InvalidOperationException articulatedException = Assert.Throws<InvalidOperationException>(() => provider.EvaluateArticulatedTrainWithWheels(
            leadDistance: leadDistance,
            definition: definition));
        InvalidOperationException poseException = Assert.Throws<InvalidOperationException>(() => provider.EvaluateTrainPose(
            leadDistance: leadDistance,
            definition: definition));

        Assert.Equal(articulatedException.Message, poseException.Message);
    }

    private static TrackDocument BuildStraightTrack(double length)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(length: length)
        });
    }

    private static TrackDocument BuildSplineTrack(double length)
    {
        return new TrackDocument(new TrackSegment[]
        {
            new CurvedSegment(
                length: length,
                spline: new Quantum.Splines.CubicBezierCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(4.0, 1.0, 0.0),
                    new Vector3d(9.0, 3.0, 2.0),
                    new Vector3d(14.0, 6.0, 3.0)),
                rollRadians: 0.3)
        });
    }

    private static TrackDocument BuildTwoSegmentSplineTrack()
    {
        return new TrackDocument(new TrackSegment[]
        {
            new StraightSegment(
                length: 10.0,
                id: "first",
                spline: new Quantum.Splines.LineCurve(
                    new Vector3d(0.0, 0.0, 0.0),
                    new Vector3d(10.0, 0.0, 0.0))),
            new StraightSegment(
                length: 10.0,
                id: "second",
                spline: new Quantum.Splines.LineCurve(
                    new Vector3d(100.0, 5.0, 0.0),
                    new Vector3d(110.0, 5.0, 0.0)))
        });
    }

    private static void AssertFiniteMatrix(Matrix4x4 matrix)
    {
        AssertFinite(matrix.M11);
        AssertFinite(matrix.M12);
        AssertFinite(matrix.M13);
        AssertFinite(matrix.M14);
        AssertFinite(matrix.M21);
        AssertFinite(matrix.M22);
        AssertFinite(matrix.M23);
        AssertFinite(matrix.M24);
        AssertFinite(matrix.M31);
        AssertFinite(matrix.M32);
        AssertFinite(matrix.M33);
        AssertFinite(matrix.M34);
        AssertFinite(matrix.M41);
        AssertFinite(matrix.M42);
        AssertFinite(matrix.M43);
        AssertFinite(matrix.M44);
    }

    private static void AssertFinite(float value)
    {
        Assert.False(float.IsNaN(value));
        Assert.False(float.IsInfinity(value));
    }

    private static void AssertFinite(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
    }

    private static void AssertFiniteMatrix(Matrix4x4d matrix)
    {
        AssertFinite(matrix.M11);
        AssertFinite(matrix.M12);
        AssertFinite(matrix.M13);
        AssertFinite(matrix.M14);
        AssertFinite(matrix.M21);
        AssertFinite(matrix.M22);
        AssertFinite(matrix.M23);
        AssertFinite(matrix.M24);
        AssertFinite(matrix.M31);
        AssertFinite(matrix.M32);
        AssertFinite(matrix.M33);
        AssertFinite(matrix.M34);
        AssertFinite(matrix.M41);
        AssertFinite(matrix.M42);
        AssertFinite(matrix.M43);
        AssertFinite(matrix.M44);
    }

    private static void AssertFiniteVector(Vector3d vector)
    {
        AssertFinite(vector.X);
        AssertFinite(vector.Y);
        AssertFinite(vector.Z);
    }

    private static void AssertTrackFrameFinite(ExportTrackFrame frame)
    {
        AssertFiniteVector(frame.Position);
        AssertFiniteVector(frame.Tangent);
        AssertFiniteVector(frame.Normal);
        AssertFiniteVector(frame.Binormal);
    }

    private static void AssertTrackFrameOrthonormal(ExportTrackFrame frame)
    {
        Assert.InRange(System.Math.Abs(frame.Tangent.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(frame.Normal.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(frame.Binormal.Length - 1.0), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Normal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Tangent, frame.Binormal)), 0.0, Tolerance);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(frame.Normal, frame.Binormal)), 0.0, Tolerance);

        Vector3d expectedBinormal = Vector3d.Cross(frame.Tangent, frame.Normal);
        AssertVectorNear(expectedBinormal, frame.Binormal);
    }

    private static void AssertTrainCarWithBogiesNear(
        TrainCarWithBogiesTransform expected,
        TrainCarWithBogiesTransform actual)
    {
        AssertTrainCarTransformNear(expected.Body, actual.Body);
        AssertBogieTransformNear(expected.FrontBogie, actual.FrontBogie);
        AssertBogieTransformNear(expected.RearBogie, actual.RearBogie);
    }

    private static void AssertArticulatedTrainCarTransformNear(
        ArticulatedTrainCarTransform expected,
        ArticulatedTrainCarTransform actual)
    {
        AssertTrainCarTransformNear(expected.OriginalBody, actual.OriginalBody);
        AssertBogieTransformNear(expected.FrontBogie, actual.FrontBogie);
        AssertBogieTransformNear(expected.RearBogie, actual.RearBogie);
        AssertTrackFrameNear(expected.ArticulatedFrame, actual.ArticulatedFrame);
        AssertMatrixNear(expected.ArticulatedMatrix, actual.ArticulatedMatrix);
        AssertDoubleNear(expected.CenterDistance, actual.CenterDistance);
    }

    private static void AssertTrainCarTransformNear(TrainCarTransform expected, TrainCarTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertBogieTransformNear(BogieTransform expected, BogieTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertTrainBogieWithWheelsTransformNear(
        TrainBogieWithWheelsTransform expected,
        TrainBogieWithWheelsTransform actual)
    {
        AssertBogieTransformNear(expected.Bogie, actual.Bogie);
        Assert.Equal(expected.Wheels.Length, actual.Wheels.Length);

        for (int i = 0; i < expected.Wheels.Length; i++)
        {
            AssertWheelTransformNear(expected.Wheels[i], actual.Wheels[i]);
        }
    }

    private static void AssertArticulatedTrainCarWithWheelsTransformNear(
        ArticulatedTrainCarWithWheelsTransform expected,
        ArticulatedTrainCarWithWheelsTransform actual)
    {
        AssertArticulatedTrainCarTransformNear(expected.Body, actual.Body);
        AssertTrainBogieWithWheelsTransformNear(expected.FrontBogie, actual.FrontBogie);
        AssertTrainBogieWithWheelsTransformNear(expected.RearBogie, actual.RearBogie);
    }

    private static void AssertWheelTransformNear(WheelTransform expected, WheelTransform actual)
    {
        Assert.Equal(expected.CarIndex, actual.CarIndex);
        Assert.Equal(expected.BogieIndex, actual.BogieIndex);
        Assert.Equal(expected.WheelIndex, actual.WheelIndex);
        AssertDoubleNear(expected.LocalOffsetX, actual.LocalOffsetX);
        AssertDoubleNear(expected.LocalOffsetY, actual.LocalOffsetY);
        AssertDoubleNear(expected.LocalOffsetZ, actual.LocalOffsetZ);
        AssertTrackFrameNear(expected.Frame, actual.Frame);
        AssertMatrixNear(expected.Matrix, actual.Matrix);
    }

    private static void AssertTrainPoseFrameDistances(
        ArticulatedTrainCarWithWheelsTransform car,
        double bodyDistance,
        double frontBogieDistance,
        double rearBogieDistance)
    {
        AssertDoubleNear(bodyDistance, car.Body.OriginalBody.Distance);
        AssertDoubleNear(bodyDistance, car.Body.OriginalBody.Frame.Distance);
        AssertDoubleNear(bodyDistance, car.Body.CenterDistance);
        AssertDoubleNear(bodyDistance, car.Body.ArticulatedFrame.Distance);

        AssertBogieFrameDistances(car.Body.FrontBogie, car.FrontBogie, frontBogieDistance);
        AssertBogieFrameDistances(car.Body.RearBogie, car.RearBogie, rearBogieDistance);
    }

    private static void AssertBogieFrameDistances(
        BogieTransform articulatedBogie,
        TrainBogieWithWheelsTransform bogieWithWheels,
        double expectedDistance)
    {
        AssertDoubleNear(expectedDistance, articulatedBogie.Distance);
        AssertDoubleNear(expectedDistance, articulatedBogie.Frame.Distance);
        AssertDoubleNear(expectedDistance, bogieWithWheels.Bogie.Distance);
        AssertDoubleNear(expectedDistance, bogieWithWheels.Bogie.Frame.Distance);

        for (int i = 0; i < bogieWithWheels.WheelsReadOnly.Count; i++)
        {
            AssertDoubleNear(expectedDistance, bogieWithWheels.WheelsReadOnly[i].Frame.Distance);
        }
    }

    private static void AssertWheelIndicesMatchBogie(TrainBogieWithWheelsTransform bogieWithWheels)
    {
        BogieTransform bogie = bogieWithWheels.Bogie;

        for (int i = 0; i < bogieWithWheels.Wheels.Length; i++)
        {
            WheelTransform wheel = bogieWithWheels.Wheels[i];
            Assert.Equal(bogie.CarIndex, wheel.CarIndex);
            Assert.Equal(bogie.BogieIndex, wheel.BogieIndex);
            Assert.Equal(i, wheel.WheelIndex);
        }
    }

    private static void AssertWheelFrameAndMatrixMatchBogie(TrainBogieWithWheelsTransform bogieWithWheels)
    {
        BogieTransform bogie = bogieWithWheels.Bogie;

        for (int i = 0; i < bogieWithWheels.Wheels.Length; i++)
        {
            WheelTransform wheel = bogieWithWheels.Wheels[i];
            AssertTrackFrameNear(bogie.Frame, wheel.Frame);
            AssertMatrixNear(bogie.Matrix, wheel.Matrix);
        }
    }

    private static void AssertWheelOffsetsFiniteAndDeterministic(WheelTransform[] expected, WheelTransform[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            WheelTransform expectedWheel = expected[i];
            WheelTransform actualWheel = actual[i];

            AssertFinite(expectedWheel.LocalOffsetX);
            AssertFinite(expectedWheel.LocalOffsetY);
            AssertFinite(expectedWheel.LocalOffsetZ);
            AssertDoubleNear(expectedWheel.LocalOffsetX, actualWheel.LocalOffsetX);
            AssertDoubleNear(expectedWheel.LocalOffsetY, actualWheel.LocalOffsetY);
            AssertDoubleNear(expectedWheel.LocalOffsetZ, actualWheel.LocalOffsetZ);
        }
    }

    private static TrainConsistDefinition BuildConsistDefinitionWithWheels(
        int carCount,
        int wheelCountPerBogie,
        double wheelWidth = 0.5,
        double axleSpacing = 1.0)
    {
        return new TrainConsistDefinition(
            carCount: carCount,
            carSpacing: 2.0,
            carLength: 4.0,
            carWidth: 1.5,
            carHeight: 1.8,
            bogieSpacing: 2.0,
            wheelLayout: new TrainWheelLayout(
                wheelCountPerBogie: wheelCountPerBogie,
                wheelRadius: 0.45,
                wheelWidth: wheelWidth,
                axleSpacing: axleSpacing));
    }

    private static void AssertTrackFrameNear(ExportTrackFrame expected, ExportTrackFrame actual)
    {
        AssertDoubleNear(expected.Distance, actual.Distance);
        AssertVectorNear(expected.Position, actual.Position);
        AssertVectorNear(expected.Tangent, actual.Tangent);
        AssertVectorNear(expected.Normal, actual.Normal);
        AssertVectorNear(expected.Binormal, actual.Binormal);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        AssertDoubleNear(expected.M11, actual.M11);
        AssertDoubleNear(expected.M12, actual.M12);
        AssertDoubleNear(expected.M13, actual.M13);
        AssertDoubleNear(expected.M14, actual.M14);
        AssertDoubleNear(expected.M21, actual.M21);
        AssertDoubleNear(expected.M22, actual.M22);
        AssertDoubleNear(expected.M23, actual.M23);
        AssertDoubleNear(expected.M24, actual.M24);
        AssertDoubleNear(expected.M31, actual.M31);
        AssertDoubleNear(expected.M32, actual.M32);
        AssertDoubleNear(expected.M33, actual.M33);
        AssertDoubleNear(expected.M34, actual.M34);
        AssertDoubleNear(expected.M41, actual.M41);
        AssertDoubleNear(expected.M42, actual.M42);
        AssertDoubleNear(expected.M43, actual.M43);
        AssertDoubleNear(expected.M44, actual.M44);
    }

    private static void AssertMatrixNear(Matrix4x4d expected, Matrix4x4d actual)
    {
        AssertDoubleNear(expected.M11, actual.M11);
        AssertDoubleNear(expected.M12, actual.M12);
        AssertDoubleNear(expected.M13, actual.M13);
        AssertDoubleNear(expected.M14, actual.M14);
        AssertDoubleNear(expected.M21, actual.M21);
        AssertDoubleNear(expected.M22, actual.M22);
        AssertDoubleNear(expected.M23, actual.M23);
        AssertDoubleNear(expected.M24, actual.M24);
        AssertDoubleNear(expected.M31, actual.M31);
        AssertDoubleNear(expected.M32, actual.M32);
        AssertDoubleNear(expected.M33, actual.M33);
        AssertDoubleNear(expected.M34, actual.M34);
        AssertDoubleNear(expected.M41, actual.M41);
        AssertDoubleNear(expected.M42, actual.M42);
        AssertDoubleNear(expected.M43, actual.M43);
        AssertDoubleNear(expected.M44, actual.M44);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual)
    {
        AssertDoubleNear(expected.X, actual.X);
        AssertDoubleNear(expected.Y, actual.Y);
        AssertDoubleNear(expected.Z, actual.Z);
    }

    private static void AssertDoubleNear(double expected, double actual)
    {
        Assert.InRange(System.Math.Abs(expected - actual), 0.0, Tolerance);
    }
}
