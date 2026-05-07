using Quantum.Math;

namespace Quantum.Track
{
    /// <summary>
    /// Articulated body transform derived from a car body and its paired bogies.
    /// <see cref="ArticulatedFrame"/> is the authoritative articulated pose basis.
    /// </summary>
    public readonly struct ArticulatedTrainCarTransform
    {
        public ArticulatedTrainCarTransform(
            TrainCarTransform originalBody,
            BogieTransform frontBogie,
            BogieTransform rearBogie,
            TrackFrame articulatedFrame,
            Matrix4x4d articulatedMatrix,
            double centerDistance)
        {
            OriginalBody = originalBody;
            FrontBogie = frontBogie;
            RearBogie = rearBogie;
            ArticulatedFrame = articulatedFrame;
            ArticulatedMatrix = articulatedMatrix;
            CenterDistance = centerDistance;
        }

        public TrainCarTransform OriginalBody { get; }

        public BogieTransform FrontBogie { get; }

        public BogieTransform RearBogie { get; }

        public TrackFrame ArticulatedFrame { get; }

        /// <summary>
        /// Articulated body matrix stored as <see cref="Matrix4x4d"/> (double precision).
        /// Current policy derives this from
        /// <c>Matrix4x4d.FromMatrix4x4(ArticulatedFrame.ToMatrix4x4())</c>.
        /// </summary>
        public Matrix4x4d ArticulatedMatrix { get; }

        /// <summary>
        /// Distance used for the articulated center sample.
        /// Currently mirrors <see cref="OriginalBody"/>.<see cref="TrainCarTransform.Distance"/>.
        /// </summary>
        public double CenterDistance { get; }
    }
}
