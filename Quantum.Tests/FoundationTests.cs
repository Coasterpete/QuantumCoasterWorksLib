using System;
using System.Collections.Generic;
using System.Reflection;
using Quantum.Math;
using Quantum.Physics;
using Quantum.Splines;
using Xunit;

namespace Quantum.Tests;

public class FoundationTests
{
    private const double LengthTolerance = 1e-6;
    private const double ValueTolerance = 1e-6;

    [Fact]
    public void Evaluate_ReturnsValidPositions_ForCurrentCurves()
    {
        foreach (var (_, curve) in BuildCurves())
        {
            foreach (double t in SampleTs())
            {
                Vector3d pos = curve.Evaluate(t);
                AssertFinite(pos);
            }
        }
    }

    [Fact]
    public void Tangent_ReturnsNonZeroNormalizedVectors_ForCurrentCurves()
    {
        foreach (var (_, curve) in BuildCurves())
        {
            foreach (double t in SampleTs())
            {
                Vector3d tan = curve.Tangent(t);
                AssertFinite(tan);
                AssertNormalizedNonZero(tan);
            }
        }
    }

    [Fact]
    public void ArcLengthCurveAdapter_MapsDistanceToValidSamples()
    {
        IParamCurve cubic = new CubicBezierCurve(
            new Vector3d(0, 0, 0),
            new Vector3d(3, 6, 0),
            new Vector3d(7, -6, 0),
            new Vector3d(10, 0, 0));

        IArcLengthCurve adapter = new ArcLengthCurveAdapter(cubic, samples: 200);

        double[] sValues = { -1.0, 0.0, adapter.Length * 0.33, adapter.Length, adapter.Length + 1.0 };

        Vector3d startPos = adapter.EvaluateByLength(0.0);
        Vector3d endPos = adapter.EvaluateByLength(adapter.Length);

        foreach (double s in sValues)
        {
            Vector3d pos = adapter.EvaluateByLength(s);
            Vector3d tan = adapter.TangentByLength(s);

            AssertFinite(pos);
            AssertFinite(tan);
            AssertNormalizedNonZero(tan);
        }

        AssertVectorNear(startPos, adapter.EvaluateByLength(-1.0), ValueTolerance);
        AssertVectorNear(endPos, adapter.EvaluateByLength(adapter.Length + 1.0), ValueTolerance);
    }

    [Fact]
    public void ArcLengthLut_HandlesDegenerateIntervalsWithoutNaNOrInfinity()
    {
        IParamCurve curve = new NearDegenerateCurve();
        var lut = new ArcLengthLUT(curve, samples: 100);

        // Chosen to land in a near-zero arc-length interval after a non-degenerate segment.
        double s = 0.500000000000005;

        double mappedT = lut.MapS2T(s);

        Assert.False(double.IsNaN(mappedT));
        Assert.False(double.IsInfinity(mappedT));
        Assert.InRange(mappedT, 0.0, 1.0);
    }

    [Fact]
    public void TrainFollowerState_ClampsWhenLoopDisabled()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 9.0, speed: 5.0, loopEnabled: false);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
    }

    [Fact]
    public void TrainFollowerState_WrapsWhenLoopEnabled()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 9.0, speed: 5.0, loopEnabled: true);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 4.0 - ValueTolerance, 4.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 4.0 - ValueTolerance, 4.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 9.0 - ValueTolerance, 9.0 + ValueTolerance);
    }

    [Fact]
    public void EasedParamCurve_EndpointsMatchInnerCurve_AndTangentsStayNormalized()
    {
        IParamCurve baseCurve = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));

        Func<double, double> easeInQuadratic = t => t * t;
        IParamCurve easedCurve = new EasedParamCurve(baseCurve, easeInQuadratic);

        Vector3d expectedStart = baseCurve.Evaluate(0.0);
        Vector3d expectedEnd = baseCurve.Evaluate(1.0);

        Vector3d actualStart = easedCurve.Evaluate(0.0);
        Vector3d actualEnd = easedCurve.Evaluate(1.0);

        AssertVectorNear(expectedStart, actualStart, ValueTolerance);
        AssertVectorNear(expectedEnd, actualEnd, ValueTolerance);

        Vector3d startTangent = easedCurve.Tangent(0.0);
        Vector3d endTangent = easedCurve.Tangent(1.0);

        AssertFinite(startTangent);
        AssertFinite(endTangent);
        AssertNormalizedNonZero(startTangent);
        AssertNormalizedNonZero(endTangent);
    }

    [Fact]
    public void CurveEasing_QuarticTransitions_StayFiniteAndWithinUnitInterval()
    {
        foreach (double t in DenseUnitSamples())
        {
            AssertFiniteUnitInterval(CurveEasing.EaseInQuart(t, tension: 1.8));
            AssertFiniteUnitInterval(CurveEasing.EaseOutQuart(t, tension: 1.8));
            AssertFiniteUnitInterval(CurveEasing.EaseInOutQuart(t, center: 0.35, tension: 1.8));
        }

        Assert.InRange(CurveEasing.EaseInQuart(0.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseInQuart(1.0) - 1.0), 0.0, ValueTolerance);
        Assert.InRange(CurveEasing.EaseOutQuart(0.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseOutQuart(1.0) - 1.0), 0.0, ValueTolerance);
        Assert.InRange(CurveEasing.EaseInOutQuart(0.0, center: 0.35, tension: 1.8), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseInOutQuart(1.0, center: 0.35, tension: 1.8) - 1.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void CurveEasing_QuinticTransitions_StayFiniteAndWithinUnitInterval()
    {
        foreach (double t in DenseUnitSamples())
        {
            AssertFiniteUnitInterval(CurveEasing.EaseInQuint(t, tension: 1.6));
            AssertFiniteUnitInterval(CurveEasing.EaseOutQuint(t, tension: 1.6));
            AssertFiniteUnitInterval(CurveEasing.EaseInOutQuint(t, center: 0.65, tension: 1.6));
        }

        Assert.InRange(CurveEasing.EaseInQuint(0.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseInQuint(1.0) - 1.0), 0.0, ValueTolerance);
        Assert.InRange(CurveEasing.EaseOutQuint(0.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseOutQuint(1.0) - 1.0), 0.0, ValueTolerance);
        Assert.InRange(CurveEasing.EaseInOutQuint(0.0, center: 0.65, tension: 1.6), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseInOutQuint(1.0, center: 0.65, tension: 1.6) - 1.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void CurveEasing_SinusoidalTransitions_StayFiniteAndWithinUnitInterval()
    {
        foreach (double t in DenseUnitSamples())
        {
            AssertFiniteUnitInterval(CurveEasing.EaseInSine(t, tension: 1.4));
            AssertFiniteUnitInterval(CurveEasing.EaseOutSine(t, tension: 1.4));
            AssertFiniteUnitInterval(CurveEasing.EaseInOutSine(t, center: 0.4, tension: 1.4));
        }

        Assert.InRange(CurveEasing.EaseInSine(0.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseInSine(1.0) - 1.0), 0.0, ValueTolerance);
        Assert.InRange(CurveEasing.EaseOutSine(0.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseOutSine(1.0) - 1.0), 0.0, ValueTolerance);
        Assert.InRange(CurveEasing.EaseInOutSine(0.0, center: 0.4, tension: 1.4), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseInOutSine(1.0, center: 0.4, tension: 1.4) - 1.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void CurveEasing_MonotonicBehavior_HoldsForExpectedTransitions()
    {
        AssertNonDecreasing(t => CurveEasing.EaseInQuart(t));
        AssertNonDecreasing(t => CurveEasing.EaseOutQuart(t));
        AssertNonDecreasing(t => CurveEasing.EaseInOutQuart(t, center: 0.4, tension: 1.5));

        AssertNonDecreasing(t => CurveEasing.EaseInQuint(t));
        AssertNonDecreasing(t => CurveEasing.EaseOutQuint(t));
        AssertNonDecreasing(t => CurveEasing.EaseInOutQuint(t, center: 0.6, tension: 1.5));

        AssertNonDecreasing(t => CurveEasing.EaseInSine(t));
        AssertNonDecreasing(t => CurveEasing.EaseOutSine(t));
        AssertNonDecreasing(t => CurveEasing.EaseInOutSine(t, center: 0.45, tension: 1.5));

        AssertNonDecreasing(t => CurveEasing.Plateau(t, plateauAmount: 0.4, center: 0.5, tension: 1.2));
    }

    [Fact]
    public void CurveEasing_Center_ShiftsMidpointBehavior()
    {
        double leftPivot = CurveEasing.EaseInOutQuint(0.5, center: 0.3, tension: 1.0);
        double rightPivot = CurveEasing.EaseInOutQuint(0.5, center: 0.7, tension: 1.0);

        Assert.True(leftPivot > 0.5, $"Expected center=0.3 to advance output at t=0.5, got {leftPivot}.");
        Assert.True(rightPivot < 0.5, $"Expected center=0.7 to delay output at t=0.5, got {rightPivot}.");
    }

    [Fact]
    public void CurveEasing_Tension_ChangesSharpness_WithoutBreakingEndpoints()
    {
        double softLow = CurveEasing.EaseInOutQuart(0.25, center: 0.5, tension: 0.6);
        double hardLow = CurveEasing.EaseInOutQuart(0.25, center: 0.5, tension: 3.0);
        double softHigh = CurveEasing.EaseInOutQuart(0.75, center: 0.5, tension: 0.6);
        double hardHigh = CurveEasing.EaseInOutQuart(0.75, center: 0.5, tension: 3.0);

        Assert.True(hardLow < softLow, $"Expected higher tension to sharpen lower-half transition ({hardLow} < {softLow}).");
        Assert.True(hardHigh > softHigh, $"Expected higher tension to sharpen upper-half transition ({hardHigh} > {softHigh}).");

        Assert.InRange(CurveEasing.EaseInOutQuart(0.0, center: 0.5, tension: 3.0), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.EaseInOutQuart(1.0, center: 0.5, tension: 3.0) - 1.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void CurveEasing_PlateauAmount_ExpandsHeldRegion_WithoutBreakingEndpoints()
    {
        double noPlateau = CurveEasing.Plateau(0.4, plateauAmount: 0.0, center: 0.5, tension: 1.0);
        double widePlateau = CurveEasing.Plateau(0.4, plateauAmount: 0.6, center: 0.5, tension: 1.0);

        Assert.True(
            System.Math.Abs(widePlateau - 0.5) < System.Math.Abs(noPlateau - 0.5),
            $"Expected wider plateau to hold output nearer 0.5 at t=0.4 ({widePlateau} vs {noPlateau}).");

        foreach (double t in DenseUnitSamples())
        {
            AssertFiniteUnitInterval(CurveEasing.Plateau(t, plateauAmount: 0.6, center: 0.5, tension: 1.3));
        }

        Assert.InRange(CurveEasing.Plateau(0.0, plateauAmount: 0.6, center: 0.5, tension: 1.3), 0.0, ValueTolerance);
        Assert.InRange(System.Math.Abs(CurveEasing.Plateau(1.0, plateauAmount: 0.6, center: 0.5, tension: 1.3) - 1.0), 0.0, ValueTolerance);
    }

    [Fact]
    public void NurbsCurve_Exists_AndEvaluatesFinitePositions()
    {
        IParamCurve nurbs = CreateNurbsOrFail(
            new List<Vector3d>
            {
                new Vector3d(0, 0, 0),
                new Vector3d(5, 0, 0),
                new Vector3d(10, 5, 0),
                new Vector3d(15, 0, 0)
            },
            new List<double> { 1.0, 0.8, 1.2, 1.0 },
            degree: 3);

        foreach (double t in SampleTs())
        {
            Vector3d pos = nurbs.Evaluate(t);
            AssertFinite(pos);
        }
    }

    [Fact]
    public void NurbsCurve_Tangent_IsFiniteAndNormalized()
    {
        IParamCurve nurbs = CreateNurbsOrFail(
            new List<Vector3d>
            {
                new Vector3d(0, 0, 0),
                new Vector3d(5, 0, 0),
                new Vector3d(10, 5, 0),
                new Vector3d(15, 0, 0)
            },
            new List<double> { 1.0, 0.8, 1.2, 1.0 },
            degree: 3);

        foreach (double t in SampleTs())
        {
            Vector3d tan = nurbs.Tangent(t);
            AssertFinite(tan);
            AssertNormalizedNonZero(tan);
        }
    }

    [Fact]
    public void NurbsCurve_UnitWeights_MatchEquivalentBSplineCurve_WithinTolerance()
    {
        var controlPoints = new List<Vector3d>
        {
            new Vector3d(0, 0, 0),
            new Vector3d(5, 0, 0),
            new Vector3d(10, 5, 0),
            new Vector3d(15, 0, 0)
        };

        const int degree = 3;
        var weights = new List<double> { 1.0, 1.0, 1.0, 1.0 };
        IParamCurve bspline = new BSplineCurve(controlPoints, degree);
        IParamCurve nurbs = CreateNurbsOrFail(controlPoints, weights, degree);

        foreach (double t in DenseUnitSamples())
        {
            Vector3d expected = bspline.Evaluate(t);
            Vector3d actual = nurbs.Evaluate(t);
            AssertVectorNear(expected, actual, ValueTolerance);
        }
    }

    [Fact]
    public void NurbsCurve_ParameterOutsideRange_ClampsSafely()
    {
        IParamCurve nurbs = CreateNurbsOrFail(
            new List<Vector3d>
            {
                new Vector3d(0, 0, 0),
                new Vector3d(5, 0, 0),
                new Vector3d(10, 5, 0),
                new Vector3d(15, 0, 0)
            },
            new List<double> { 1.0, 0.8, 1.2, 1.0 },
            degree: 3);

        Vector3d start = nurbs.Evaluate(0.0);
        Vector3d end = nurbs.Evaluate(1.0);
        Vector3d beforeStart = nurbs.Evaluate(-0.25);
        Vector3d afterEnd = nurbs.Evaluate(1.25);

        AssertVectorNear(start, beforeStart, ValueTolerance);
        AssertVectorNear(end, afterEnd, ValueTolerance);

        Vector3d tanBeforeStart = nurbs.Tangent(-0.25);
        Vector3d tanAfterEnd = nurbs.Tangent(1.25);
        AssertFinite(tanBeforeStart);
        AssertFinite(tanAfterEnd);
        AssertNormalizedNonZero(tanBeforeStart);
        AssertNormalizedNonZero(tanAfterEnd);
    }

    [Fact]
    public void NurbsCurve_InvalidWeights_Throw_ForCountMismatchAndNonPositiveValues()
    {
        _ = RequireNurbsType();

        var controlPoints = new List<Vector3d>
        {
            new Vector3d(0, 0, 0),
            new Vector3d(5, 0, 0),
            new Vector3d(10, 5, 0),
            new Vector3d(15, 0, 0)
        };

        Assert.ThrowsAny<Exception>(() =>
            CreateNurbsOrFail(controlPoints, new List<double> { 1.0, 1.0, 1.0 }, degree: 3));

        Assert.ThrowsAny<Exception>(() =>
            CreateNurbsOrFail(controlPoints, new List<double> { 1.0, 0.0, 1.0, 1.0 }, degree: 3));

        Assert.ThrowsAny<Exception>(() =>
            CreateNurbsOrFail(controlPoints, new List<double> { 1.0, -1.0, 1.0, 1.0 }, degree: 3));
    }

    [Fact]
    public void NurbsCurve_InvalidCustomKnots_Throw_ForWrongCountAndDecreasingSequence()
    {
        _ = RequireNurbsType();

        var controlPoints = new List<Vector3d>
        {
            new Vector3d(0, 0, 0),
            new Vector3d(5, 0, 0),
            new Vector3d(10, 5, 0),
            new Vector3d(15, 0, 0)
        };
        var weights = new List<double> { 1.0, 1.0, 1.0, 1.0 };

        Assert.ThrowsAny<Exception>(() =>
            CreateNurbsWithKnotsOrFail(
                controlPoints,
                weights,
                degree: 3,
                knots: new List<double> { 0.0, 0.0, 0.0, 0.5, 1.0, 1.0, 1.0 }));

        Assert.ThrowsAny<Exception>(() =>
            CreateNurbsWithKnotsOrFail(
                controlPoints,
                weights,
                degree: 3,
                knots: new List<double> { 0.0, 0.0, 0.0, 0.6, 0.5, 1.0, 1.0, 1.0 }));
    }

    [Fact]
    public void TrainFollowerState_MovingBackward_ClampsAtStart_WhenLoopDisabled()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 1.0, speed: -5.0, loopEnabled: false);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 0.0 - ValueTolerance, 0.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 0.0 - ValueTolerance, 0.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);
    }

    [Fact]
    public void TrainFollowerState_MovingBackward_Wraps_WhenLoopEnabled()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 1.0, speed: -5.0, loopEnabled: true);

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 6.0 - ValueTolerance, 6.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 6.0 - ValueTolerance, 6.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);
    }

    [Fact]
    public void TrainFollowerState_UsesConstantAcceleration_ForDistanceAndVelocity()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 2.0, speed: 3.0, loopEnabled: false);

        const double acceleration = -1.5;
        const double deltaTime = 2.0;

        follower.Acceleration = acceleration;
        follower.Update(deltaTime);

        double expectedDistance = 2.0 + (3.0 * deltaTime) + (0.5 * acceleration * deltaTime * deltaTime);
        double expectedSpeed = 3.0 + (acceleration * deltaTime);

        Assert.InRange(follower.Distance, expectedDistance - ValueTolerance, expectedDistance + ValueTolerance);
        Assert.InRange(follower.Position.X, expectedDistance - ValueTolerance, expectedDistance + ValueTolerance);
        Assert.InRange(follower.Speed, expectedSpeed - ValueTolerance, expectedSpeed + ValueTolerance);
    }

    [Fact]
    public void TrainFollowerState_AccelerationZero_PreservesLegacyConstantSpeedBehavior()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 2.0, speed: 3.0, loopEnabled: false)
        {
            Acceleration = 0.0
        };

        follower.Update(2.0);

        Assert.InRange(follower.Distance, 8.0 - ValueTolerance, 8.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 8.0 - ValueTolerance, 8.0 + ValueTolerance);
        Assert.InRange(follower.Speed, 3.0 - ValueTolerance, 3.0 + ValueTolerance);
    }

    [Fact]
    public void TrainFollowerState_Acceleration_WrapsAcrossLoopBoundary()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 9.0, speed: 3.0, loopEnabled: true)
        {
            Acceleration = 2.0
        };

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 3.0 - ValueTolerance, 3.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 3.0 - ValueTolerance, 3.0 + ValueTolerance);
        Assert.InRange(follower.Speed, 5.0 - ValueTolerance, 5.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);
    }

    [Fact]
    public void TrainFollowerState_Acceleration_ClampsAtStart_WhenLoopDisabled()
    {
        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 1.0, speed: -2.0, loopEnabled: false)
        {
            Acceleration = -2.0
        };

        follower.Update(1.0);

        Assert.InRange(follower.Distance, 0.0 - ValueTolerance, 0.0 + ValueTolerance);
        Assert.InRange(follower.Position.X, 0.0 - ValueTolerance, 0.0 + ValueTolerance);
        Assert.InRange(follower.Speed, -4.0 - ValueTolerance, -4.0 + ValueTolerance);
        AssertNormalizedNonZero(follower.Tangent);
    }

    [Fact]
    public void TrackFrameSampler_SampleByLength_ClampsToEndpoints_AndReturnsFiniteNormalizedTangent()
    {
        Type samplerType = RequireTrackFrameSamplerType();
        Type sampleType = RequireArcLengthSampleType();

        MethodInfo? sampleByLength = samplerType.GetMethod(
            "SampleByLength",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(IArcLengthCurve), typeof(double) },
            modifiers: null);

        Assert.True(
            sampleByLength is not null,
            "Expected method: TrackFrameSampler.SampleByLength(IArcLengthCurve, double).");

        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));

        object? beforeStartObject = sampleByLength!.Invoke(null, new object[] { track, -2.0 });
        object? afterEndObject = sampleByLength.Invoke(null, new object[] { track, track.Length + 2.0 });

        Assert.True(beforeStartObject is not null, "Expected SampleByLength to return a sample for s below start.");
        Assert.True(afterEndObject is not null, "Expected SampleByLength to return a sample for s above end.");
        Assert.Equal(sampleType, beforeStartObject!.GetType());
        Assert.Equal(sampleType, afterEndObject!.GetType());

        Vector3d beforeStartPosition = GetVector3dMember(beforeStartObject, "Position");
        Vector3d beforeStartTangent = GetVector3dMember(beforeStartObject, "Tangent");
        Vector3d afterEndPosition = GetVector3dMember(afterEndObject, "Position");
        Vector3d afterEndTangent = GetVector3dMember(afterEndObject, "Tangent");

        Assert.InRange(beforeStartPosition.X, 0.0 - ValueTolerance, 0.0 + ValueTolerance);
        Assert.InRange(afterEndPosition.X, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
        AssertFinite(beforeStartTangent);
        AssertFinite(afterEndTangent);
        AssertNormalizedNonZero(beforeStartTangent);
        AssertNormalizedNonZero(afterEndTangent);
    }

    [Fact]
    public void TrackFrameSampler_SampleFrameByLength_ReturnsOrthonormalBasis()
    {
        Type samplerType = RequireTrackFrameSamplerType();
        Type frameType = RequireTrackFrameType();

        MethodInfo? sampleFrameByLength = samplerType.GetMethod(
            "SampleFrameByLength",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(IArcLengthCurve), typeof(double), typeof(Vector3d) },
            modifiers: null);

        Assert.True(
            sampleFrameByLength is not null,
            "Expected method: TrackFrameSampler.SampleFrameByLength(IArcLengthCurve, double, Vector3d).");

        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 10, 0));
        object? frameObject = sampleFrameByLength!.Invoke(null, new object[] { track, track.Length * 0.5, Vector3d.UnitZ });

        Assert.True(frameObject is not null, "Expected SampleFrameByLength to return a frame.");
        Assert.Equal(frameType, frameObject!.GetType());

        Vector3d tangent = GetVector3dMember(frameObject, "Tangent");
        Vector3d right = GetVector3dMember(frameObject, "Right");
        Vector3d up = GetVector3dMember(frameObject, "Up");

        AssertNormalizedNonZero(tangent);
        AssertNormalizedNonZero(right);
        AssertNormalizedNonZero(up);

        Assert.InRange(System.Math.Abs(Vector3d.Dot(tangent, right)), 0.0, 1e-6);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(tangent, up)), 0.0, 1e-6);
        Assert.InRange(System.Math.Abs(Vector3d.Dot(right, up)), 0.0, 1e-6);
    }

    [Fact]
    public void TrackFrameSampler_SampleFrameByLength_HandlesReferenceUpParallelToTangent_WithoutNaNOrInfinity()
    {
        Type samplerType = RequireTrackFrameSamplerType();

        MethodInfo? sampleFrameByLength = samplerType.GetMethod(
            "SampleFrameByLength",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(IArcLengthCurve), typeof(double), typeof(Vector3d) },
            modifiers: null);

        Assert.True(
            sampleFrameByLength is not null,
            "Expected method: TrackFrameSampler.SampleFrameByLength(IArcLengthCurve, double, Vector3d).");

        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(0, 10, 0));
        object? frameObject = sampleFrameByLength!.Invoke(null, new object[] { track, track.Length * 0.5, Vector3d.UnitY });

        Assert.True(frameObject is not null, "Expected SampleFrameByLength to return a frame.");

        Vector3d tangent = GetVector3dMember(frameObject!, "Tangent");
        Vector3d right = GetVector3dMember(frameObject, "Right");
        Vector3d up = GetVector3dMember(frameObject, "Up");

        AssertFinite(tangent);
        AssertFinite(right);
        AssertFinite(up);
        AssertNormalizedNonZero(tangent);
        AssertNormalizedNonZero(right);
        AssertNormalizedNonZero(up);
    }

    [Fact]
    public void TrackFrameSampler_SampleFramesUniform_IncludesStartAndEnd_WhenLengthNotDivisibleByStepLength()
    {
        Type samplerType = RequireTrackFrameSamplerType();

        MethodInfo? sampleFramesUniform = samplerType.GetMethod(
            "SampleFramesUniform",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(IArcLengthCurve), typeof(double), typeof(Vector3d) },
            modifiers: null);

        Assert.True(
            sampleFramesUniform is not null,
            "Expected method: TrackFrameSampler.SampleFramesUniform(IArcLengthCurve, double, Vector3d).");

        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        object? framesObject = sampleFramesUniform!.Invoke(null, new object[] { track, 3.0, Vector3d.UnitY });

        Assert.True(framesObject is not null, "Expected SampleFramesUniform to return an enumerable of frames.");
        Assert.True(
            framesObject is System.Collections.IEnumerable,
            "Expected SampleFramesUniform return value to implement IEnumerable.");

        var frames = new List<object>();
        foreach (object? frame in (System.Collections.IEnumerable)framesObject!)
        {
            Assert.True(frame is not null, "Expected all sampled frames to be non-null.");
            frames.Add(frame!);
        }

        Assert.True(frames.Count >= 2, "Expected at least start and end frames.");

        Vector3d startPosition = GetVector3dMember(frames[0], "Position");
        Vector3d endPosition = GetVector3dMember(frames[frames.Count - 1], "Position");

        Assert.InRange(startPosition.X, 0.0 - ValueTolerance, 0.0 + ValueTolerance);
        Assert.InRange(endPosition.X, 10.0 - ValueTolerance, 10.0 + ValueTolerance);
    }

    [Fact]
    public void TrainFollowerState_ExposesTrackFrame_AtCurrentDistance()
    {
        Type frameType = RequireTrackFrameType();
        PropertyInfo? frameProperty = typeof(TrainFollowerState).GetProperty("Frame", BindingFlags.Public | BindingFlags.Instance);

        Assert.True(frameProperty is not null, "Expected TrainFollowerState to expose a public Frame property.");
        Assert.Equal(frameType, frameProperty!.PropertyType);

        IArcLengthCurve track = new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));
        var follower = new TrainFollowerState(track, initialDistance: 4.0, speed: 0.0, loopEnabled: false);

        object? initialFrameObject = frameProperty.GetValue(follower);
        Assert.True(initialFrameObject is not null, "Expected Frame to be initialized in constructor.");

        Vector3d initialPosition = GetVector3dMember(initialFrameObject!, "Position");
        Assert.InRange(initialPosition.X, 4.0 - ValueTolerance, 4.0 + ValueTolerance);

        follower.Speed = 2.0;
        follower.Update(1.0);

        object? updatedFrameObject = frameProperty.GetValue(follower);
        Assert.True(updatedFrameObject is not null, "Expected Frame to remain available after Update.");

        Vector3d updatedPosition = GetVector3dMember(updatedFrameObject!, "Position");
        Assert.InRange(updatedPosition.X, 6.0 - ValueTolerance, 6.0 + ValueTolerance);
    }

    private static IEnumerable<(string Name, IParamCurve Curve)> BuildCurves()
    {
        yield return ("Line", new LineCurve(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0)));

        yield return (
            "Quadratic",
            new QuadraticBezierCurve(
                new Vector3d(0, 0, 0),
                new Vector3d(5, 5, 0),
                new Vector3d(10, 0, 0)));

        yield return (
            "Cubic",
            new CubicBezierCurve(
                new Vector3d(0, 0, 0),
                new Vector3d(3, 6, 0),
                new Vector3d(7, -6, 0),
                new Vector3d(10, 0, 0)));

        yield return (
            "BSpline",
            new BSplineCurve(
                new List<Vector3d>
                {
                    new Vector3d(0, 0, 0),
                    new Vector3d(5, 0, 0),
                    new Vector3d(10, 5, 0),
                    new Vector3d(15, 0, 0)
                },
                degree: 3));
    }

    private static IEnumerable<double> SampleTs()
    {
        yield return 0.0;
        yield return 0.25;
        yield return 0.5;
        yield return 0.75;
        yield return 1.0;
    }

    private static void AssertFinite(Vector3d value)
    {
        Assert.False(double.IsNaN(value.X) || double.IsNaN(value.Y) || double.IsNaN(value.Z));
        Assert.False(double.IsInfinity(value.X) || double.IsInfinity(value.Y) || double.IsInfinity(value.Z));
    }

    private static void AssertFiniteUnitInterval(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
        Assert.InRange(value, 0.0, 1.0);
    }

    private static void AssertNonDecreasing(Func<double, double> function)
    {
        double previous = double.NegativeInfinity;

        foreach (double t in DenseUnitSamples())
        {
            double current = function(t);
            AssertFiniteUnitInterval(current);

            if (!double.IsNegativeInfinity(previous))
            {
                Assert.True(
                    current + ValueTolerance >= previous,
                    $"Expected non-decreasing sequence at t={t:0.###}, current={current}, previous={previous}.");
            }

            previous = current;
        }
    }

    private static IEnumerable<double> DenseUnitSamples()
    {
        for (int i = 0; i <= 20; i++)
            yield return i / 20.0;
    }

    private static void AssertNormalizedNonZero(Vector3d tangent)
    {
        double len = tangent.Length;
        Assert.False(double.IsNaN(len) || double.IsInfinity(len));
        Assert.True(len > MathUtil.Epsilon, $"Expected non-zero tangent length, got {len}.");
        Assert.InRange(System.Math.Abs(len - 1.0), 0.0, LengthTolerance);
    }

    private static void AssertVectorNear(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.InRange(System.Math.Abs(expected.X - actual.X), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Y - actual.Y), 0.0, tolerance);
        Assert.InRange(System.Math.Abs(expected.Z - actual.Z), 0.0, tolerance);
    }

    private static IParamCurve CreateNurbsOrFail(
        List<Vector3d> controlPoints,
        List<double> weights,
        int degree)
    {
        Type nurbsType = RequireNurbsType();
        ConstructorInfo? ctor = nurbsType.GetConstructor(new[] { typeof(List<Vector3d>), typeof(List<double>), typeof(int) });

        Assert.True(
            ctor is not null,
            "Expected NurbsCurve constructor: NurbsCurve(List<Vector3d>, List<double>, int).");

        object? instance = ctor!.Invoke(new object[] { controlPoints, weights, degree });
        return Assert.IsAssignableFrom<IParamCurve>(instance);
    }

    private static IParamCurve CreateNurbsWithKnotsOrFail(
        List<Vector3d> controlPoints,
        List<double> weights,
        int degree,
        List<double> knots)
    {
        Type nurbsType = RequireNurbsType();
        ConstructorInfo? ctor = nurbsType.GetConstructor(
            new[] { typeof(List<Vector3d>), typeof(List<double>), typeof(int), typeof(List<double>) });

        Assert.True(
            ctor is not null,
            "Expected NurbsCurve constructor: NurbsCurve(List<Vector3d>, List<double>, int, List<double>)." );

        object? instance = ctor!.Invoke(new object[] { controlPoints, weights, degree, knots });
        return Assert.IsAssignableFrom<IParamCurve>(instance);
    }

    private static Type RequireNurbsType()
    {
        Type? type = typeof(IParamCurve).Assembly.GetType("Quantum.Splines.NurbsCurve");
        Assert.True(type is not null, "Expected Quantum.Splines.NurbsCurve to exist.");
        return type!;
    }

    private static Type RequireArcLengthSampleType()
    {
        Type? type = typeof(IParamCurve).Assembly.GetType("Quantum.Splines.ArcLengthSample");
        Assert.True(type is not null, "Expected Quantum.Splines.ArcLengthSample to exist.");
        return type!;
    }

    private static Type RequireTrackFrameType()
    {
        Type? type = typeof(IParamCurve).Assembly.GetType("Quantum.Splines.TrackFrame");
        Assert.True(type is not null, "Expected Quantum.Splines.TrackFrame to exist.");
        return type!;
    }

    private static Type RequireTrackFrameSamplerType()
    {
        Type? type = typeof(IParamCurve).Assembly.GetType("Quantum.Splines.TrackFrameSampler");
        Assert.True(type is not null, "Expected Quantum.Splines.TrackFrameSampler to exist.");
        return type!;
    }

    private static Vector3d GetVector3dMember(object instance, string memberName)
    {
        Type type = instance.GetType();

        PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property is not null)
        {
            object? value = property.GetValue(instance);
            Assert.True(value is Vector3d, $"Expected {type.FullName}.{memberName} to be a Vector3d property.");
            return (Vector3d)value!;
        }

        FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null)
        {
            object? value = field.GetValue(instance);
            Assert.True(value is Vector3d, $"Expected {type.FullName}.{memberName} to be a Vector3d field.");
            return (Vector3d)value!;
        }

        throw new Xunit.Sdk.XunitException($"Expected {type.FullName} to contain public member '{memberName}'.");
    }

    private sealed class NearDegenerateCurve : IParamCurve
    {
        public Vector3d Evaluate(double t)
        {
            if (t <= 0.5)
                return new Vector3d(t, 0.0, 0.0);

            // Extremely small motion creates near-zero arc-length intervals.
            return new Vector3d(0.5 + (t - 0.5) * 1e-12, 0.0, 0.0);
        }

        public Vector3d Tangent(double t)
        {
            return Vector3d.UnitX;
        }
    }
}

