using System;

namespace Quantum.Track
{
    [Flags]
    public enum SectionEvaluationDiagnostic
    {
        None = 0,
        NoSection = 1 << 0,
        OutsideSectionCoverage = 1 << 1,
        MissingChannel = 1 << 2
    }
}
