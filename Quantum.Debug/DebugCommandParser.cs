using System;
using System.Collections.Generic;

namespace Quantum.Debug
{
    public enum DebugCommandKind
    {
        Validate,
        Help,
        SamplingPerf,
        TrainPoseExportV1,
        DebugViewportSnapshotV1,
        DebugViewportSnapshotV1FromCsv,
        DebugViewportSnapshotV1Validate,
        LongitudinalForcePreview,
        LongitudinalSpeedPreview
    }

    public static class DebugCommandParser
    {
        public static bool TryParse(
            IReadOnlyList<string> args,
            out DebugCommandKind command)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (args.Count == 0)
            {
                command = DebugCommandKind.Validate;
                return true;
            }

            if (DebugCommandHelp.IsHelpToken(args[0]))
            {
                command = DebugCommandKind.Help;
                return true;
            }

            if (string.Equals(args[0], "sampling-perf", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.SamplingPerf;
                return true;
            }

            if (string.Equals(args[0], "train-pose-export-v1", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.TrainPoseExportV1;
                return true;
            }

            if (string.Equals(args[0], "debug-viewport-snapshot-v1", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.DebugViewportSnapshotV1;
                return true;
            }

            if (string.Equals(args[0], "debug-viewport-snapshot-v1-from-csv", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.DebugViewportSnapshotV1FromCsv;
                return true;
            }

            if (string.Equals(args[0], "debug-viewport-snapshot-v1-validate", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.DebugViewportSnapshotV1Validate;
                return true;
            }

            if (string.Equals(args[0], "longitudinal-force-preview", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.LongitudinalForcePreview;
                return true;
            }

            if (string.Equals(args[0], "longitudinal-speed-preview", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.LongitudinalSpeedPreview;
                return true;
            }

            command = default;
            return false;
        }
    }
}
