namespace Quantum.Track
{
    /// <summary>
    /// Evaluated car body pose together with its front and rear bogie poses.
    /// </summary>
    public readonly struct TrainCarWithBogiesTransform
    {
        /// <summary>
        /// Creates a car-and-bogies wrapper from evaluated body and bogie transforms.
        /// </summary>
        public TrainCarWithBogiesTransform(
            TrainCarTransform body,
            BogieTransform frontBogie,
            BogieTransform rearBogie)
        {
            Body = body;
            FrontBogie = frontBogie;
            RearBogie = rearBogie;
        }

        /// <summary>
        /// Evaluated car body transform.
        /// </summary>
        public TrainCarTransform Body { get; }

        /// <summary>
        /// Evaluated front bogie transform.
        /// </summary>
        public BogieTransform FrontBogie { get; }

        /// <summary>
        /// Evaluated rear bogie transform.
        /// </summary>
        public BogieTransform RearBogie { get; }
    }
}
