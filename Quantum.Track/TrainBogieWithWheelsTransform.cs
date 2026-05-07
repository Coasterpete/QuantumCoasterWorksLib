using System;
using System.Collections.Generic;

namespace Quantum.Track
{
    public readonly struct TrainBogieWithWheelsTransform
    {
        private readonly WheelTransform[]? _wheelsSnapshot;
        private readonly IReadOnlyList<WheelTransform>? _wheelsReadOnly;

        public TrainBogieWithWheelsTransform(BogieTransform bogie, WheelTransform[] wheels)
        {
            Bogie = bogie;
            _wheelsSnapshot = CopyArray(wheels);
            _wheelsReadOnly = _wheelsSnapshot == null ? null : Array.AsReadOnly(_wheelsSnapshot);
        }

        public BogieTransform Bogie { get; }

        public WheelTransform[] Wheels => CopyArray(_wheelsSnapshot)!;

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
