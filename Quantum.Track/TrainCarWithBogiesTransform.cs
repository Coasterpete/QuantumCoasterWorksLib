namespace Quantum.Track
{
    public readonly struct TrainCarWithBogiesTransform
    {
        public TrainCarWithBogiesTransform(
            TrainCarTransform body,
            BogieTransform frontBogie,
            BogieTransform rearBogie)
        {
            Body = body;
            FrontBogie = frontBogie;
            RearBogie = rearBogie;
        }

        public TrainCarTransform Body { get; }

        public BogieTransform FrontBogie { get; }

        public BogieTransform RearBogie { get; }
    }
}
