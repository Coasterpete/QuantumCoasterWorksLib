using System;

namespace Quantum.IO.TrackLayout.V1
{
    public static class TrackLayoutPackageV1Vocabulary
    {
        public const string StraightSectionKind = "straight";
        public const string ConstantCurvatureSectionKind = "constantCurvature";
        public const string CurvatureTransitionSectionKind = "curvatureTransition";
        public const string SpatialSectionKind = "spatial";

        public const string CurvatureInterpolationLinear = "linear";

        public const string BankingInterpolationConstant = "constant";
        public const string BankingInterpolationLinear = "linear";
        public const string BankingInterpolationSmoothStep = "smoothStep";

        public static bool IsKnownSectionKind(string? value)
        {
            return string.Equals(value, StraightSectionKind, StringComparison.Ordinal) ||
                   string.Equals(value, ConstantCurvatureSectionKind, StringComparison.Ordinal) ||
                   string.Equals(value, CurvatureTransitionSectionKind, StringComparison.Ordinal) ||
                   string.Equals(value, SpatialSectionKind, StringComparison.Ordinal);
        }

        public static bool IsKnownCurvatureTransitionInterpolation(string? value)
        {
            return string.Equals(value, CurvatureInterpolationLinear, StringComparison.Ordinal);
        }

        public static bool IsKnownBankingInterpolation(string? value)
        {
            return string.Equals(value, BankingInterpolationConstant, StringComparison.Ordinal) ||
                   string.Equals(value, BankingInterpolationLinear, StringComparison.Ordinal) ||
                   string.Equals(value, BankingInterpolationSmoothStep, StringComparison.Ordinal);
        }
    }
}
