using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    /// <summary>
    /// Evaluated bogie pose together with the wheel poses sampled from that bogie.
    /// </summary>
    public readonly struct TrainBogieWithWheelsTransform
    {
        private readonly WheelTransform[]? _wheelsSnapshot;
        private readonly IReadOnlyList<WheelTransform>? _wheelsReadOnly;

        /// <summary>
        /// Creates a bogie-and-wheels wrapper from an evaluated bogie and wheel array.
        /// </summary>
        /// <param name="bogie">Evaluated bogie transform.</param>
        /// <param name="wheels">Evaluated wheel transforms to copy into the snapshot.</param>
        public TrainBogieWithWheelsTransform(BogieTransform bogie, WheelTransform[] wheels)
        {
            if (wheels is null)
            {
                throw new ArgumentNullException(nameof(wheels));
            }

            Bogie = bogie;
            _wheelsSnapshot = CopyArray(wheels);
            _wheelsReadOnly = Array.AsReadOnly(_wheelsSnapshot);
        }

        /// <summary>
        /// Evaluated bogie transform.
        /// </summary>
        public BogieTransform Bogie { get; }

        /// <summary>
        /// Copy of the evaluated wheel transforms.
        /// </summary>
        public WheelTransform[] Wheels => CopyArray(_wheelsSnapshot)!;

        /// <summary>
        /// Read-only view of the evaluated wheel transforms.
        /// </summary>
        public IReadOnlyList<WheelTransform> WheelsReadOnly => _wheelsReadOnly!;

        private static WheelTransform[]? CopyArray(WheelTransform[]? source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new WheelTransform[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
