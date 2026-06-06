using System;
using System.Collections.Generic;
using Quantum.Track;

namespace Quantum.IO.DistanceInspection.V1
{
    /// <summary>
    /// Maps distance inspection snapshots into the stable UI-facing JSON DTO contract.
    /// </summary>
    public static class DistanceInspectionSnapshotV1Mapper
    {
        public static DistanceInspectionSnapshotV1Dto Export(DistanceInspectionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new DistanceInspectionSnapshotV1Dto
            {
                Contract = DistanceInspectionSnapshotV1Dto.ContractName,
                Version = DistanceInspectionSnapshotV1Dto.ContractVersion,
                Distance = snapshot.Distance,
                Sections = MapSections(snapshot.Sections)
            };
        }

        private static DistanceInspectionSectionV1Dto[] MapSections(
            IReadOnlyList<DistanceSectionInspection> sections)
        {
            var result = new DistanceInspectionSectionV1Dto[sections.Count];

            for (int i = 0; i < sections.Count; i++)
            {
                DistanceSectionInspection section = sections[i];
                result[i] = new DistanceInspectionSectionV1Dto
                {
                    Kind = section.Kind.ToString(),
                    Domain = section.Domain.ToString(),
                    StartX = section.StartX,
                    EndX = section.EndX,
                    Diagnostic = section.Diagnostic.ToString(),
                    Channels = MapChannels(section.Channels),
                    ChannelValues = MapChannelValues(section.ChannelValues)
                };
            }

            return result;
        }

        private static string[] MapChannels(IReadOnlyList<SectionChannel> channels)
        {
            var result = new string[channels.Count];

            for (int i = 0; i < channels.Count; i++)
            {
                result[i] = channels[i].ToString();
            }

            return result;
        }

        private static DistanceInspectionChannelValueV1Dto[] MapChannelValues(
            IReadOnlyList<DistanceSectionChannelInspection> channelValues)
        {
            var result = new DistanceInspectionChannelValueV1Dto[channelValues.Count];

            for (int i = 0; i < channelValues.Count; i++)
            {
                DistanceSectionChannelInspection channelValue = channelValues[i];
                result[i] = new DistanceInspectionChannelValueV1Dto
                {
                    Channel = channelValue.Channel.ToString(),
                    Value = channelValue.Value
                };
            }

            return result;
        }
    }
}
