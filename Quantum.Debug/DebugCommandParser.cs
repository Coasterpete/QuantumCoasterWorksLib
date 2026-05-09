using System;
using System.Collections.Generic;

namespace Quantum.Debug
{
    public enum DebugCommandKind
    {
        Validate,
        SamplingPerf
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

            if (string.Equals(args[0], "sampling-perf", StringComparison.OrdinalIgnoreCase))
            {
                command = DebugCommandKind.SamplingPerf;
                return true;
            }

            command = default;
            return false;
        }
    }
}
