using System;
using System.Collections.Generic;
using Quantum.Track;

namespace Quantum.IO.Fixtures.Csv
{
    /// <summary>
    /// Parsed sampled centerline frame fixture data in backend meters.
    /// </summary>
    public sealed class CenterlineFrameCsvFixture
    {
        public CenterlineFrameCsvFixture(
            IReadOnlyList<TrackFrame> frames,
            string? sourceFixtureName = null,
            string units = "meters")
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            var copiedFrames = new TrackFrame[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                copiedFrames[i] = frames[i];
            }

            Frames = copiedFrames;
            SourceFixtureName = sourceFixtureName;
            Units = string.IsNullOrWhiteSpace(units) ? "meters" : units;
        }

        public IReadOnlyList<TrackFrame> Frames { get; }

        public string? SourceFixtureName { get; }

        public string Units { get; }
    }
}
