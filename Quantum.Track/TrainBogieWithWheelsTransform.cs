using System.Collections.Generic;

namespace Quantum.Track
{
    public readonly struct TrainBogieWithWheelsTransform
    {
        public TrainBogieWithWheelsTransform(BogieTransform bogie, WheelTransform[] wheels)
        {
            Bogie = bogie;
            Wheels = wheels;
        }

        public BogieTransform Bogie { get; }

        public WheelTransform[] Wheels { get; }

        public IReadOnlyList<WheelTransform> WheelsReadOnly => Wheels;
    }
}
