using System;
using UnityEngine;

namespace QuantumVisualizer
{
    public static class DebugViewportSnapshotV1JsonLoader
    {
        public const string ExpectedContract = "quantum.debug_viewport_snapshot";
        public const int ExpectedVersion = 1;

        public static bool TryLoad(TextAsset jsonAsset, out DebugViewportSnapshotV1Dto snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;

            if (jsonAsset == null)
            {
                error = "No JSON TextAsset assigned.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(jsonAsset.text))
            {
                error = "Assigned JSON TextAsset is empty.";
                return false;
            }

            try
            {
                snapshot = JsonUtility.FromJson<DebugViewportSnapshotV1Dto>(jsonAsset.text);
            }
            catch (Exception ex)
            {
                error = "Failed to parse JSON: " + ex.Message;
                return false;
            }

            if (snapshot == null)
            {
                error = "JSON parse returned null.";
                return false;
            }

            if (!string.Equals(snapshot.contract, ExpectedContract, StringComparison.Ordinal))
            {
                error = "Unexpected contract. Expected '" + ExpectedContract + "' but got '" + (snapshot.contract ?? "<null>") + "'.";
                return false;
            }

            if (snapshot.version != ExpectedVersion)
            {
                error = "Unexpected version. Expected " + ExpectedVersion + " but got " + snapshot.version + ".";
                return false;
            }

            return true;
        }
    }
}
