using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Quantum.Math;
using Quantum.Track;

namespace Quantum.IO.Fixtures.Csv
{
    /// <summary>
    /// Parser for the deliberately narrow Milestone 5 sampled-frame CSV fixture format.
    /// </summary>
    public static class CenterlineFrameCsvFixtureParser
    {
        public const string RequiredHeader =
            "distanceMeters,xMeters,yMeters,zMeters,tangentX,tangentY,tangentZ,normalX,normalY,normalZ,binormalX,binormalY,binormalZ";

        private const int ColumnCount = 13;

        public static CenterlineFrameCsvFixture Parse(string csv, string? sourceFixtureName = null)
        {
            if (csv == null)
            {
                throw new ArgumentNullException(nameof(csv));
            }

            using (var reader = new StringReader(csv))
            {
                return Parse(reader, sourceFixtureName);
            }
        }

        public static CenterlineFrameCsvFixture ParseFile(string path, string? sourceFixtureName = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            using (var reader = new StreamReader(path))
            {
                return Parse(reader, sourceFixtureName);
            }
        }

        public static CenterlineFrameCsvFixture Parse(TextReader reader, string? sourceFixtureName = null)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            string? header = reader.ReadLine();
            if (header == null)
            {
                throw new FormatException("Centerline frame CSV fixture is empty.");
            }

            header = RemoveUtf8Bom(header);
            if (!string.Equals(header, RequiredHeader, StringComparison.Ordinal))
            {
                throw new FormatException(
                    $"Invalid centerline frame CSV header. Expected '{RequiredHeader}'.");
            }

            var frames = new List<TrackFrame>();
            double previousDistance = 0.0;
            bool hasPreviousDistance = false;
            int rowNumber = 1;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                rowNumber++;

                string[] columns = line.Split(',');
                if (columns.Length != ColumnCount)
                {
                    throw new FormatException(
                        $"Invalid centerline frame CSV row {rowNumber}: expected {ColumnCount} columns, got {columns.Length}.");
                }

                double distance = ParseFinite(columns[0], rowNumber, "distanceMeters");
                if (distance < 0.0)
                {
                    throw new FormatException(
                        $"Invalid centerline frame CSV row {rowNumber}: distanceMeters must be non-negative.");
                }

                if (hasPreviousDistance && distance < previousDistance)
                {
                    throw new FormatException(
                        $"Invalid centerline frame CSV row {rowNumber}: distanceMeters must be monotonically increasing.");
                }

                var position = new Vector3d(
                    ParseFinite(columns[1], rowNumber, "xMeters"),
                    ParseFinite(columns[2], rowNumber, "yMeters"),
                    ParseFinite(columns[3], rowNumber, "zMeters"));
                var tangent = new Vector3d(
                    ParseFinite(columns[4], rowNumber, "tangentX"),
                    ParseFinite(columns[5], rowNumber, "tangentY"),
                    ParseFinite(columns[6], rowNumber, "tangentZ"));
                var normal = new Vector3d(
                    ParseFinite(columns[7], rowNumber, "normalX"),
                    ParseFinite(columns[8], rowNumber, "normalY"),
                    ParseFinite(columns[9], rowNumber, "normalZ"));
                var binormal = new Vector3d(
                    ParseFinite(columns[10], rowNumber, "binormalX"),
                    ParseFinite(columns[11], rowNumber, "binormalY"),
                    ParseFinite(columns[12], rowNumber, "binormalZ"));

                frames.Add(new TrackFrame(distance, position, tangent, normal, binormal));

                previousDistance = distance;
                hasPreviousDistance = true;
            }

            return new CenterlineFrameCsvFixture(frames, sourceFixtureName);
        }

        private static double ParseFinite(string value, int rowNumber, string columnName)
        {
            if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed))
            {
                throw new FormatException(
                    $"Invalid centerline frame CSV row {rowNumber}: {columnName} must be a numeric value.");
            }

            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
            {
                throw new FormatException(
                    $"Invalid centerline frame CSV row {rowNumber}: {columnName} must be finite.");
            }

            return parsed;
        }

        private static string RemoveUtf8Bom(string value)
        {
            if (value.Length > 0 && value[0] == '\uFEFF')
            {
                return value.Substring(1);
            }

            return value;
        }
    }
}
