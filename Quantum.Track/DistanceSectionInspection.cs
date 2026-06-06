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
        private readonly List<DistanceSectionChannelInspection> _channelValues;
        private readonly IReadOnlyList<DistanceSectionChannelInspection> _channelValuesView;

        public DistanceSectionInspection(
            SectionKind kind,
            SectionDomain domain,
            double startX,
            double endX,
            IReadOnlyList<SectionChannel> channels,
            SectionEvaluationDiagnostic diagnostic)
            : this(
                kind,
                domain,
                startX,
                endX,
                channels,
                Array.Empty<DistanceSectionChannelInspection>(),
                diagnostic)
        {
        }

        public DistanceSectionInspection(
            SectionKind kind,
            SectionDomain domain,
            double startX,
            double endX,
            IReadOnlyList<SectionChannel> channels,
            IReadOnlyList<DistanceSectionChannelInspection> channelValues,
            SectionEvaluationDiagnostic diagnostic)
        {
            if (channels is null)
            {
                throw new ArgumentNullException(nameof(channels));
            }

            if (channelValues is null)
            {
                throw new ArgumentNullException(nameof(channelValues));
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

            _channelValues = new List<DistanceSectionChannelInspection>(channelValues.Count);
            for (int i = 0; i < channelValues.Count; i++)
            {
                _channelValues.Add(channelValues[i]);
            }

            _channelValuesView = _channelValues.AsReadOnly();
        }

        public SectionKind Kind { get; }

        public SectionDomain Domain { get; }

        public double StartX { get; }

        public double EndX { get; }

        public IReadOnlyList<SectionChannel> Channels => _channelsView;

        public IReadOnlyList<DistanceSectionChannelInspection> ChannelValues => _channelValuesView;

        public SectionEvaluationDiagnostic Diagnostic { get; }
    }
}
