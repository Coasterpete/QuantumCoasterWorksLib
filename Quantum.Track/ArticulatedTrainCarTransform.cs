using Quantum.Math;

namespace Quantum.Track
{
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

        public Matrix4x4d ArticulatedMatrix { get; }

        public double CenterDistance { get; }
    }
}
