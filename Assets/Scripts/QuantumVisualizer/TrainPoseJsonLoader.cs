using System;
using UnityEngine;

namespace QuantumVisualizer
{
    public static class TrainPoseJsonLoader
    {
        public const string ExpectedContract = "quantum.train_pose";
        public const int ExpectedVersion = 1;

        public static bool TryLoad(TextAsset jsonAsset, out TrainPoseExportV1Dto pose, out string error)
        {
            pose = null;
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
                pose = JsonUtility.FromJson<TrainPoseExportV1Dto>(jsonAsset.text);
            }
            catch (Exception ex)
            {
                error = "Failed to parse JSON: " + ex.Message;
                return false;
            }

            if (pose == null)
            {
                error = "JSON parse returned null.";
                return false;
            }

            if (!string.Equals(pose.contract, ExpectedContract, StringComparison.Ordinal))
            {
                error = "Unexpected contract. Expected '" + ExpectedContract + "' but got '" + (pose.contract ?? "<null>") + "'.";
                return false;
            }

            if (pose.version != ExpectedVersion)
            {
                error = "Unexpected version. Expected " + ExpectedVersion + " but got " + pose.version + ".";
                return false;
            }

            return true;
        }
    }
}
