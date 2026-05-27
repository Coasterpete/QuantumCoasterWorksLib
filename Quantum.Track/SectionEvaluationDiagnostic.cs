using System;

namespace Quantum.Track
{
    /// <summary>
    /// Diagnostic flags for permissive normalized section evaluation.
    /// </summary>
    [Flags]
    public enum SectionEvaluationDiagnostic
    {
        /// <summary>
        /// Evaluation succeeded.
        /// </summary>
        None = 0,

        /// <summary>
        /// No section exists for the requested kind and domain.
        /// </summary>
        NoSection = 1 << 0,

        /// <summary>
        /// Sections exist for the requested kind and domain, but the requested coordinate
        /// is outside their covered intervals.
        /// </summary>
        OutsideSectionCoverage = 1 << 1,

        /// <summary>
        /// A section was resolved, but it does not contain the requested channel.
        /// </summary>
        MissingChannel = 1 << 2
    }
}
