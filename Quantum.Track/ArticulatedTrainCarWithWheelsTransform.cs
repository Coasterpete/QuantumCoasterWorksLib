namespace Quantum.Track
{
    /// <summary>
    /// Evaluated articulated car pose with front and rear bogies including wheel poses.
    /// </summary>
    public readonly struct ArticulatedTrainCarWithWheelsTransform
    {
        /// <summary>
        /// Creates an articulated car-and-running-gear wrapper from evaluated transforms.
        /// </summary>
        /// <param name="body">Evaluated articulated body transform.</param>
        /// <param name="frontBogie">Evaluated front bogie and wheel transforms.</param>
        /// <param name="rearBogie">Evaluated rear bogie and wheel transforms.</param>
        public ArticulatedTrainCarWithWheelsTransform(
            ArticulatedTrainCarTransform body,
            TrainBogieWithWheelsTransform frontBogie,
            TrainBogieWithWheelsTransform rearBogie)
        {
            Body = body;
            FrontBogie = frontBogie;
            RearBogie = rearBogie;
        }

        /// <summary>
        /// Evaluated articulated body transform.
        /// </summary>
        public ArticulatedTrainCarTransform Body { get; }

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
