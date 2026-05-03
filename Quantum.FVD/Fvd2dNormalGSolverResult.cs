using System;

namespace Quantum.FVD
{
    public sealed class Fvd2dNormalGSolverResult
    {
        public FvdGraph Graph { get; }

        public Fvd2dNormalGSolverStatus Status { get; }

        public double BeforeAbsoluteNormalGError { get; }

        public double AfterAbsoluteNormalGError { get; }

        public Fvd2dNormalGSolverResult(
            FvdGraph graph,
            Fvd2dNormalGSolverStatus status,
            double beforeAbsoluteNormalGError,
            double afterAbsoluteNormalGError)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Status = status;
            BeforeAbsoluteNormalGError = beforeAbsoluteNormalGError;
            AfterAbsoluteNormalGError = afterAbsoluteNormalGError;
        }
    }
}
