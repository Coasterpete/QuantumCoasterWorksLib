namespace Quantum.Track
{
    /// <summary>
    /// Evaluated car body pose together with bogie poses that include wheel poses.
    /// </summary>
    public readonly struct TrainCarWithBogiesAndWheelsTransform
    {
        /// <summary>
        /// Creates a car-and-running-gear wrapper from evaluated body, bogie, and wheel transforms.
        /// </summary>
        /// <param name="body">Evaluated car body transform.</param>
        /// <param name="frontBogie">Evaluated front bogie and wheel transforms.</param>
        /// <param name="rearBogie">Evaluated rear bogie and wheel transforms.</param>
        public TrainCarWithBogiesAndWheelsTransform(
            TrainCarTransform body,
            TrainBogieWithWheelsTransform frontBogie,
            TrainBogieWithWheelsTransform rearBogie)
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
        /// Evaluated front bogie and wheel transforms.
        /// </summary>
        public TrainBogieWithWheelsTransform FrontBogie { get; }

        /// <summary>
        /// Evaluated rear bogie and wheel transforms.
        /// </summary>
        public TrainBogieWithWheelsTransform RearBogie { get; }
    }
}
