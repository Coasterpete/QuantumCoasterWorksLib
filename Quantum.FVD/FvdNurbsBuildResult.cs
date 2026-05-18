using System;
using Quantum.Splines;

namespace Quantum.FVD
{
    public sealed class FvdNurbsBuildResult
    {
        public NurbsCurve ParamCurve { get; }

        public ArcLengthCurveAdapter ArcCurve { get; }

        public IParamCurve RuntimeParamCurve { get; }

        public IArcLengthCurve RuntimeArcLengthCurve { get; }

        public FvdNurbsBuildResult(NurbsCurve paramCurve, ArcLengthCurveAdapter arcCurve)
            : this(paramCurve, arcCurve, paramCurve, arcCurve)
        {
        }

        public FvdNurbsBuildResult(
            NurbsCurve paramCurve,
            ArcLengthCurveAdapter arcCurve,
            IParamCurve runtimeParamCurve,
            IArcLengthCurve runtimeArcLengthCurve)
        {
            ParamCurve = paramCurve ?? throw new ArgumentNullException(nameof(paramCurve));
            ArcCurve = arcCurve ?? throw new ArgumentNullException(nameof(arcCurve));
            RuntimeParamCurve = runtimeParamCurve ?? throw new ArgumentNullException(nameof(runtimeParamCurve));
            RuntimeArcLengthCurve = runtimeArcLengthCurve ?? throw new ArgumentNullException(nameof(runtimeArcLengthCurve));
        }
    }
}
