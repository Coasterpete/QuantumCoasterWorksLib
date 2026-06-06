using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Immutable UI-friendly snapshot of a resolved distance-domain section.
    /// </summary>
    public sealed class DistanceSectionInspection
    {
        private readonly List<SectionChannel> _channels;
        private readonly IReadOnlyList<SectionChannel> _channelsView;

        public DistanceSectionInspection(
            SectionKind kind,
            SectionDomain domain,
            double startX,
            double endX,
            IReadOnlyList<SectionChannel> channels,
            SectionEvaluationDiagnostic diagnostic)
        {
            if (channels is null)
            {
                throw new ArgumentNullException(nameof(channels));
            }

            Kind = kind;
            Domain = domain;
            StartX = startX;
            EndX = endX;
            Diagnostic = diagnostic;

            _channels = new List<SectionChannel>(channels.Count);
            for (int i = 0; i < channels.Count; i++)
            {
                _channels.Add(channels[i]);
            }

            _channelsView = _channels.AsReadOnly();
        }

        public SectionKind Kind { get; }

        public SectionDomain Domain { get; }

        public double StartX { get; }

        public double EndX { get; }

        public IReadOnlyList<SectionChannel> Channels => _channelsView;

        public SectionEvaluationDiagnostic Diagnostic { get; }
    }
}
