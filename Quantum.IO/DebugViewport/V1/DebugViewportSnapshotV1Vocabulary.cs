namespace Quantum.IO.DebugViewport.V1
{
    /// <summary>
    /// Stable renderer-neutral role and kind tokens for DebugViewportSnapshotV1 consumers.
    /// </summary>
    public static class DebugViewportSnapshotV1Vocabulary
    {
        public const string TrainBodyRole = "train.body";
        public const string TrainBodyBankingProfileRole = "train.body.banking-profile";
        public const string TrainBogieRole = "train.bogie";
        public const string TrainWheelRole = "train.wheel";

        public const string FrameAxisTangentKind = "frame.axis.tangent";
        public const string FrameAxisNormalKind = "frame.axis.normal";
        public const string FrameAxisBinormalKind = "frame.axis.binormal";
        public const string DiagnosticLineKind = "diagnostic.line";

        public static bool IsKnownBoxRole(string? role)
        {
            return role == TrainBodyRole ||
                   role == TrainBodyBankingProfileRole ||
                   role == TrainBogieRole ||
                   role == TrainWheelRole;
        }

        public static bool IsKnownLineKind(string? kind)
        {
            return kind == FrameAxisTangentKind ||
                   kind == FrameAxisNormalKind ||
                   kind == FrameAxisBinormalKind ||
                   kind == DiagnosticLineKind;
        }
    }
}
