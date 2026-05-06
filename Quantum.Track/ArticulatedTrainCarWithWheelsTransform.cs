namespace Quantum.Track
{
    public readonly struct ArticulatedTrainCarWithWheelsTransform
    {
        public ArticulatedTrainCarWithWheelsTransform(
            ArticulatedTrainCarTransform body,
            TrainBogieWithWheelsTransform frontBogie,
            TrainBogieWithWheelsTransform rearBogie)
        {
            Body = body;
            FrontBogie = frontBogie;
            RearBogie = rearBogie;
        }

        public ArticulatedTrainCarTransform Body { get; }

        public TrainBogieWithWheelsTransform FrontBogie { get; }

        public TrainBogieWithWheelsTransform RearBogie { get; }
    }
}
