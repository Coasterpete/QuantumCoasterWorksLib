using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Read-only resolver and evaluator for normalized section definitions.
    /// Evaluation is intentionally distance-domain only; time-domain definitions
    /// may be stored and overlap-checked here, but are data-only until elapsed-time
    /// integration is explicitly wired into runtime callers.
    /// </summary>
    public sealed class NormalizedSectionEvaluator
    {
        private readonly List<SectionDefinition> _sections;
        private readonly IReadOnlyList<SectionDefinition> _sectionsView;

        public NormalizedSectionEvaluator(IReadOnlyList<SectionDefinition> sections)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            _sections = new List<SectionDefinition>(sections.Count);

            for (int i = 0; i < sections.Count; i++)
            {
                SectionDefinition section = sections[i] ?? throw new ArgumentException(
                    $"Section at index {i} cannot be null.",
                    nameof(sections));

                ValidateSectionOverlap(section, i);
                _sections.Add(section);
            }

            _sectionsView = _sections.AsReadOnly();
        }

        public IReadOnlyList<SectionDefinition> Sections => _sectionsView;

        public SectionDefinition ResolveDistanceSection(SectionKind kind, double distance)
        {
            if (TryResolveDistanceSection(kind, distance, out SectionDefinition section))
            {
                return section;
            }

            throw new InvalidOperationException(
                $"No distance-domain section exists for kind '{kind}' at distance {distance}.");
        }

        public bool TryResolveDistanceSection(
            SectionKind kind,
            double distance,
            out SectionDefinition section)
        {
            return TryResolveDistanceSection(kind, distance, out section, out _);
        }

        public bool TryResolveDistanceSection(
            SectionKind kind,
            double distance,
            out SectionDefinition section,
            out SectionEvaluationDiagnostic diagnostic)
        {
            return TryResolveSection(
                kind,
                SectionDomain.Distance,
                distance,
                out section,
                out diagnostic);
        }

        public double EvaluateDistanceChannelAt(
            SectionKind kind,
            SectionChannel channel,
            double distance)
        {
            ValidateChannel(channel);
            SectionDefinition section = ResolveDistanceSection(kind, distance);
            return section.EvaluateAt(channel, distance);
        }

        public bool TryEvaluateDistanceChannelAt(
            SectionKind kind,
            SectionChannel channel,
            double distance,
            out double value)
        {
            return TryEvaluateDistanceChannelAt(kind, channel, distance, out value, out _);
        }

        public bool TryEvaluateDistanceChannelAt(
            SectionKind kind,
            SectionChannel channel,
            double distance,
            out double value,
            out SectionEvaluationDiagnostic diagnostic)
        {
            ValidateChannel(channel);

            if (!TryResolveDistanceSection(kind, distance, out SectionDefinition section, out diagnostic))
            {
                value = default;
                return false;
            }

            if (!section.TryGetFunction(channel, out SectionFunction? function))
            {
                value = default;
                diagnostic = SectionEvaluationDiagnostic.MissingChannel;
                return false;
            }

            value = function!.EvaluateAt(distance);
            diagnostic = SectionEvaluationDiagnostic.None;
            return true;
        }

        public IReadOnlyList<SectionChannelEvaluation> EvaluateDistanceAllAt(
            SectionKind kind,
            double distance)
        {
            SectionDefinition section = ResolveDistanceSection(kind, distance);
            return section.EvaluateAllAt(distance);
        }

        public bool TryEvaluateDistanceAllAt(
            SectionKind kind,
            double distance,
            out IReadOnlyList<SectionChannelEvaluation> evaluations)
        {
            return TryEvaluateDistanceAllAt(kind, distance, out evaluations, out _);
        }

        public bool TryEvaluateDistanceAllAt(
            SectionKind kind,
            double distance,
            out IReadOnlyList<SectionChannelEvaluation> evaluations,
            out SectionEvaluationDiagnostic diagnostic)
        {
            if (!TryResolveDistanceSection(kind, distance, out SectionDefinition section, out diagnostic))
            {
                evaluations = Array.Empty<SectionChannelEvaluation>();
                return false;
            }

            evaluations = section.EvaluateAllAt(distance);
            diagnostic = SectionEvaluationDiagnostic.None;
            return true;
        }

        private bool TryResolveSection(
            SectionKind kind,
            SectionDomain domain,
            double x,
            out SectionDefinition section,
            out SectionEvaluationDiagnostic diagnostic)
        {
            ValidateKind(kind);
            ValidateDomain(domain);
            ValidateEvaluationX(x);

            SectionDefinition? finalSection = null;
            bool hasMatchingSection = false;

            for (int i = 0; i < _sections.Count; i++)
            {
                SectionDefinition candidate = _sections[i];
                if (candidate.Kind != kind || candidate.Domain != domain)
                {
                    continue;
                }

                hasMatchingSection = true;

                if (finalSection is null || candidate.EndX > finalSection.EndX)
                {
                    finalSection = candidate;
                }

                if (x >= candidate.StartX && x < candidate.EndX)
                {
                    section = candidate;
                    diagnostic = SectionEvaluationDiagnostic.None;
                    return true;
                }
            }

            if (finalSection != null && x == finalSection.EndX && x >= finalSection.StartX)
            {
                section = finalSection;
                diagnostic = SectionEvaluationDiagnostic.None;
                return true;
            }

            section = null!;
            diagnostic = hasMatchingSection
                ? SectionEvaluationDiagnostic.OutsideSectionCoverage
                : SectionEvaluationDiagnostic.NoSection;
            return false;
        }

        private void ValidateSectionOverlap(SectionDefinition candidate, int candidateIndex)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                SectionDefinition existing = _sections[i];

                if (existing.Kind != candidate.Kind || existing.Domain != candidate.Domain)
                {
                    continue;
                }

                bool overlaps = candidate.StartX < existing.EndX
                                && existing.StartX < candidate.EndX;

                if (!overlaps)
                {
                    continue;
                }

                throw new ArgumentException(
                    $"Section at index {candidateIndex} overlaps section at index {i} for kind '{candidate.Kind}' and domain '{candidate.Domain}'.",
                    nameof(candidate));
            }
        }

        private static void ValidateKind(SectionKind kind)
        {
            if (kind != SectionKind.Force && kind != SectionKind.Geometry)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported section kind.");
            }
        }

        private static void ValidateDomain(SectionDomain domain)
        {
            if (domain != SectionDomain.Distance && domain != SectionDomain.Time)
            {
                throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unsupported section domain.");
            }
        }

        private static void ValidateChannel(SectionChannel channel)
        {
            if (channel != SectionChannel.NormalG
                && channel != SectionChannel.LateralG
                && channel != SectionChannel.LongitudinalG
                && channel != SectionChannel.RollRateDegPerSec
                && channel != SectionChannel.Curvature
                && channel != SectionChannel.Roll)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unsupported section channel.");
            }
        }

        private static void ValidateEvaluationX(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "Evaluation X must be finite.");
            }
        }
    }
}
