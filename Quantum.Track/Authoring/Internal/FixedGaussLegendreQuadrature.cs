using System;

namespace Quantum.Track.Authoring.Internal
{
    internal static class FixedGaussLegendreQuadrature
    {
        private static readonly double[] PositiveNodes =
        {
            0.09501250983763744,
            0.2816035507792589,
            0.4580167776572274,
            0.6178762444026438,
            0.755404408355003,
            0.8656312023878318,
            0.9445750230732326,
            0.9894009349916499
        };

        private static readonly double[] PositiveWeights =
        {
            0.1894506104550685,
            0.1826034150449236,
            0.16915651939500254,
            0.14959598881657673,
            0.12462897125553387,
            0.09515851168249278,
            0.06225352393864789,
            0.027152459411754095
        };

        public static double Integrate(Func<double, double> function, double start, double end)
        {
            if (function is null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            if (start == end)
            {
                return 0.0;
            }

            double midpoint = 0.5 * (start + end);
            double halfWidth = 0.5 * (end - start);
            double weightedSum = 0.0;

            for (int i = 0; i < PositiveNodes.Length; i++)
            {
                double offset = halfWidth * PositiveNodes[i];
                weightedSum += PositiveWeights[i] *
                    (function(midpoint - offset) + function(midpoint + offset));
            }

            return halfWidth * weightedSum;
        }
    }
}
