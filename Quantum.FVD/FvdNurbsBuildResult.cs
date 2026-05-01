using System;
using Quantum.Splines;

namespace Quantum.FVD
{
    public sealed class FvdNurbsBuildResult
    {
        public NurbsCurve ParamCurve { get; }

        public ArcLengthCurveAdapter ArcCurve { get; }

        public FvdNurbsBuildResult(NurbsCurve paramCurve, ArcLengthCurveAdapter arcCurve)
        {
            ParamCurve = paramCurve ?? throw new ArgumentNullException(nameof(paramCurve));
            ArcCurve = arcCurve ?? throw new ArgumentNullException(nameof(arcCurve));
        }
    }
}
