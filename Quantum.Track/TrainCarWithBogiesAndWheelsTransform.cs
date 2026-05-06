namespace Quantum.Track
{
    public readonly struct TrainCarWithBogiesAndWheelsTransform
    {
        public TrainCarWithBogiesAndWheelsTransform(
            TrainCarTransform body,
            TrainBogieWithWheelsTransform frontBogie,
            TrainBogieWithWheelsTransform rearBogie)
        {
            Body = body;
            FrontBogie = frontBogie;
            RearBogie = rearBogie;
        }

        public TrainCarTransform Body { get; }

        public TrainBogieWithWheelsTransform FrontBogie { get; }

        public TrainBogieWithWheelsTransform RearBogie { get; }
    }
}
