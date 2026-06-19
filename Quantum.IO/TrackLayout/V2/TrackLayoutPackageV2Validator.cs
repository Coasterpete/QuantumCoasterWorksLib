using System;
using System.Collections.Generic;
using System.Globalization;

namespace Quantum.IO.TrackLayout.V2
{
    public enum TrackLayoutPackageV2ValidationCode
    {
        InvalidContract = 0,
        InvalidVersion = 1,
        MissingMetadata = 2,
        MissingStartPose = 3,
        MissingSections = 4,
        EmptySections = 5,
        MissingObject = 6,
        MissingRequiredField = 7,
        MissingSectionId = 8,
        DuplicateSectionId = 9,
        UnknownSectionKind = 10,
        UnexpectedSectionField = 11,
        NonFiniteNumber = 12,
        NonPositiveLength = 13,
        InvalidRadius = 14,
        InvalidCurvatureInterpolation = 15,
        InvalidStartPoseBasis = 16,
        InvalidSpatialDegree = 17,
        InvalidSpatialControlPoints = 18,
        InvalidSpatialWeights = 19,
        InvalidSpatialStartContract = 20,
        InvalidBankingKeyCount = 21,
        InvalidBankingKeyOrder = 22,
        InvalidBankingInterpolation = 23,
        InvalidBankingDomain = 24,
        InvalidHeartlineKind = 25,
        InvalidHeartlineDistanceDomain = 26,
        InvalidHeartlineAxisSource = 27,
        MalformedJson = 28,
        MappingFailed = 29
    }

    public sealed class TrackLayoutPackageV2ValidationDiagnostic
    {
        public TrackLayoutPackageV2ValidationDiagnostic(
            TrackLayoutPackageV2ValidationCode code,
            string path,
            string message,
            double? value = null,
            double? expected = null,
            double? tolerance = null)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Value = value;
            Expected = expected;
            Tolerance = tolerance;
        }

        public TrackLayoutPackageV2ValidationCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public double? Value { get; }

        public double? Expected { get; }

        public double? Tolerance { get; }
    }

    internal static class TrackLayoutPackageV2DiagnosticMessages
    {
        public static string Section(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            string detail)
        {
            return WithContext(detail, SectionContext(section, sectionIndex));
        }

        public static string DuplicateSectionId(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            int previousSectionIndex)
        {
            return WithContext(
                "Section IDs must be unique using ordinal comparison.",
                SectionContext(section, sectionIndex)) +
                " Previous section index " +
                previousSectionIndex.ToString(CultureInfo.InvariantCulture) +
                " used the same id.";
        }

        public static string SectionContext(
            TrackLayoutSectionV2Dto section,
            int sectionIndex)
        {
            string context = "Section index " + sectionIndex.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(section.Id))
            {
                context += ", id '" + section.Id + "'";
            }

            if (!string.IsNullOrWhiteSpace(section.Kind))
            {
                context += ", kind '" + section.Kind + "'";
            }

            return context + ".";
        }

        public static string BankingKey(int keyIndex, string detail)
        {
            return WithContext(detail, BankingKeyContext(keyIndex));
        }

        public static string BankingKeyOrder(
            int keyIndex,
            int previousKeyIndex,
            double previousDistance)
        {
            return WithContext(
                "Banking key distances must be strictly increasing.",
                "Banking key index " +
                keyIndex.ToString(CultureInfo.InvariantCulture) +
                "; previous key index " +
                previousKeyIndex.ToString(CultureInfo.InvariantCulture) +
                " distance " +
                FormatDouble(previousDistance) +
                ".");
        }

        public static string BankingFinalDomain(
            int keyIndex,
            double expectedTotalLength)
        {
            return WithContext(
                "Authored banking must end exactly at total section length.",
                "Banking key index " +
                keyIndex.ToString(CultureInfo.InvariantCulture) +
                "; expected total length " +
                FormatDouble(expectedTotalLength) +
                ".");
        }

        public static string WithContext(string detail, string? context)
        {
            if (string.IsNullOrWhiteSpace(context))
            {
                return detail;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return context;
            }

            return detail + " " + context;
        }

        public static string BankingKeyContext(int keyIndex)
        {
            return "Banking key index " + keyIndex.ToString(CultureInfo.InvariantCulture) + ".";
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public static class TrackLayoutPackageV2Validator
    {
        private const double MinimumAxisMagnitude = 1e-9;
        private const double UnitLengthTolerance = 1e-9;
        private const double OrthogonalityTolerance = 1e-9;
        private const double HandednessTolerance = 1e-9;
        private const double MinimumSpatialStartTangentMagnitude = 1e-9;
        private const double SpatialStartTangentAlignmentTolerance = 1e-9;

        public static IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> Validate(
            TrackLayoutPackageV2Dto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            var diagnostics = new List<TrackLayoutPackageV2ValidationDiagnostic>();

            ValidateContract(dto, diagnostics);
            ValidateMetadata(dto.Metadata, diagnostics);
            ValidateStartPose(dto.StartPose, diagnostics);

            double totalLength = 0.0;
            bool totalLengthValid = ValidateSections(dto.Sections, diagnostics, out totalLength);
            ValidateBanking(dto.Banking, totalLength, totalLengthValid, diagnostics);
            ValidateHeartline(dto.Heartline, diagnostics);

            return diagnostics.ToArray();
        }

        public static bool TryValidate(
            TrackLayoutPackageV2Dto dto,
            out IReadOnlyList<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            diagnostics = Validate(dto);
            return diagnostics.Count == 0;
        }

        private static void ValidateContract(
            TrackLayoutPackageV2Dto dto,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (!string.Equals(dto.Contract, TrackLayoutPackageV2Dto.ContractName, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidContract,
                        "contract",
                        "Contract name must match TrackLayoutPackageV2."));
            }

            if (dto.Version != TrackLayoutPackageV2Dto.ContractVersion)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidVersion,
                        "version",
                        "Version must match TrackLayoutPackageV2.",
                        dto.Version,
                        TrackLayoutPackageV2Dto.ContractVersion));
            }
        }

        private static void ValidateMetadata(
            TrackLayoutMetadataV2Dto? metadata,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (metadata == null)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.MissingMetadata,
                        "metadata",
                        "Metadata object is required."));
                return;
            }

            if (string.IsNullOrWhiteSpace(metadata.Units))
            {
                AddMissingField(
                    "metadata.units",
                    "Metadata units are required and must not be blank.",
                    diagnostics);
            }
        }

        private static void ValidateStartPose(
            TrackStartPoseV2Dto? startPose,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (startPose == null)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.MissingStartPose,
                        "startPose",
                        "Start pose object is required."));
                return;
            }

            ValidateFiniteVector(startPose.Position, "startPose.position", diagnostics);
            bool tangentValid = ValidateBasisVector(startPose.Tangent, "startPose.tangent", diagnostics);
            bool normalValid = ValidateBasisVector(startPose.Normal, "startPose.normal", diagnostics);
            bool binormalValid = ValidateBasisVector(startPose.Binormal, "startPose.binormal", diagnostics);

            if (!tangentValid || !normalValid || !binormalValid)
            {
                return;
            }

            ValidateUnitLength(startPose.Tangent!, "startPose.tangent", diagnostics);
            ValidateUnitLength(startPose.Normal!, "startPose.normal", diagnostics);
            ValidateUnitLength(startPose.Binormal!, "startPose.binormal", diagnostics);
            ValidateOrthogonality(
                startPose.Tangent!,
                startPose.Normal!,
                "startPose.tangentNormalDot",
                diagnostics);
            ValidateOrthogonality(
                startPose.Tangent!,
                startPose.Binormal!,
                "startPose.tangentBinormalDot",
                diagnostics);
            ValidateOrthogonality(
                startPose.Normal!,
                startPose.Binormal!,
                "startPose.normalBinormalDot",
                diagnostics);

            double handedness = Dot(Cross(startPose.Tangent!, startPose.Normal!), startPose.Binormal!);
            if (handedness < 1.0 - HandednessTolerance)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
                        "startPose.handedness",
                        "Start pose basis must satisfy binormal ~= tangent x normal.",
                        handedness,
                        1.0,
                        HandednessTolerance));
            }
        }

        private static bool ValidateSections(
            TrackLayoutSectionV2Dto[]? sections,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            out double totalLength)
        {
            totalLength = 0.0;
            bool totalLengthValid = true;

            if (sections == null)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.MissingSections,
                        "sections",
                        "Sections collection is required."));
                return false;
            }

            if (sections.Length == 0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.EmptySections,
                        "sections",
                        "At least one section is required."));
                return false;
            }

            var sectionIdIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < sections.Length; i++)
            {
                string path = "sections[" + i + "]";
                TrackLayoutSectionV2Dto? section = sections[i];
                if (section == null)
                {
                    AddMissingObject(path, diagnostics);
                    continue;
                }

                ValidateSectionCommon(section, i, path, sectionIdIndexes, diagnostics);
                ValidateSectionByKind(section, i, path, diagnostics);

                if (IsFinite(section.Length))
                {
                    totalLength += section.Length;
                    if (!IsFinite(totalLength))
                    {
                        totalLengthValid = false;
                        diagnostics.Add(
                            new TrackLayoutPackageV2ValidationDiagnostic(
                                TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
                                "sections.totalLength",
                                "Combined section length must be finite.",
                                totalLength));
                    }
                }
                else
                {
                    totalLengthValid = false;
                }
            }

            return totalLengthValid;
        }

        private static void ValidateSectionCommon(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            string path,
            Dictionary<string, int> sectionIdIndexes,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(section.Kind))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.MissingRequiredField,
                        path + ".kind",
                        TrackLayoutPackageV2DiagnosticMessages.Section(
                            section,
                            sectionIndex,
                            "Section kind is required.")));
            }

            if (string.IsNullOrWhiteSpace(section.Id))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.MissingSectionId,
                        path + ".id",
                        TrackLayoutPackageV2DiagnosticMessages.Section(
                            section,
                            sectionIndex,
                            "Section ID is required and must not be blank.")));
            }
            else if (sectionIdIndexes.TryGetValue(section.Id, out int previousSectionIndex))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.DuplicateSectionId,
                        path + ".id",
                        TrackLayoutPackageV2DiagnosticMessages.DuplicateSectionId(
                            section,
                            sectionIndex,
                            previousSectionIndex)));
            }
            else
            {
                sectionIdIndexes.Add(section.Id, sectionIndex);
            }

            string sectionContext = TrackLayoutPackageV2DiagnosticMessages.SectionContext(section, sectionIndex);
            bool lengthFinite = ValidateFinite(section.Length, path + ".length", diagnostics, sectionContext);
            if (lengthFinite && section.Length <= 0.0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.NonPositiveLength,
                        path + ".length",
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Section length must be finite and greater than zero.",
                            sectionContext),
                        section.Length,
                        0.0));
            }

            ValidateFinite(section.RollRadians, path + ".rollRadians", diagnostics, sectionContext);
        }

        private static void ValidateSectionByKind(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            switch (section.Kind)
            {
                case TrackLayoutPackageV2Vocabulary.StraightSectionKind:
                    ValidateStraightSection(section, sectionIndex, path, diagnostics);
                    return;

                case TrackLayoutPackageV2Vocabulary.ConstantCurvatureSectionKind:
                    ValidateConstantCurvatureSection(section, sectionIndex, path, diagnostics);
                    return;

                case TrackLayoutPackageV2Vocabulary.CurvatureTransitionSectionKind:
                    ValidateCurvatureTransitionSection(section, sectionIndex, path, diagnostics);
                    return;

                case TrackLayoutPackageV2Vocabulary.SpatialSectionKind:
                    ValidateSpatialSection(section, sectionIndex, path, diagnostics);
                    return;

                default:
                    diagnostics.Add(
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.UnknownSectionKind,
                            path + ".kind",
                            TrackLayoutPackageV2DiagnosticMessages.Section(
                                section,
                                sectionIndex,
                                "Section kind must use the TrackLayoutPackageV2 stable vocabulary.")));
                    return;
            }
        }

        private static void ValidateStraightSection(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            string sectionContext = TrackLayoutPackageV2DiagnosticMessages.SectionContext(section, sectionIndex);
            ValidateFieldAbsent(section.Radius, path + ".radius", diagnostics, sectionContext);
            ValidateFieldAbsent(section.StartCurvature, path + ".startCurvature", diagnostics, sectionContext);
            ValidateFieldAbsent(section.EndCurvature, path + ".endCurvature", diagnostics, sectionContext);
            ValidateFieldAbsent(section.InterpolationMode, path + ".interpolationMode", diagnostics, sectionContext);
            ValidateFieldAbsent(section.Degree, path + ".degree", diagnostics, sectionContext);
            ValidateFieldAbsent(section.ControlPoints, path + ".controlPoints", diagnostics, sectionContext);
            ValidateFieldAbsent(section.Weights, path + ".weights", diagnostics, sectionContext);
        }

        private static void ValidateConstantCurvatureSection(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            string sectionContext = TrackLayoutPackageV2DiagnosticMessages.SectionContext(section, sectionIndex);
            if (!section.Radius.HasValue)
            {
                AddMissingField(
                    path + ".radius",
                    "Constant-curvature section radius is required.",
                    diagnostics,
                    sectionContext);
            }
            else
            {
                bool finite = ValidateFinite(section.Radius.Value, path + ".radius", diagnostics, sectionContext);
                if (finite && section.Radius.Value == 0.0)
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.InvalidRadius,
                            path + ".radius",
                            TrackLayoutPackageV2DiagnosticMessages.WithContext(
                                "Constant-curvature section radius must be non-zero.",
                                sectionContext),
                            section.Radius.Value));
                }

                if (finite && IsFinite(section.Length))
                {
                    double curvature = 1.0 / section.Radius.Value;
                    double sweep = section.Length * curvature;
                    if (!IsFinite(curvature) || !IsFinite(sweep))
                    {
                        diagnostics.Add(
                            new TrackLayoutPackageV2ValidationDiagnostic(
                                TrackLayoutPackageV2ValidationCode.InvalidRadius,
                                path + ".radius",
                                TrackLayoutPackageV2DiagnosticMessages.WithContext(
                                    "Constant-curvature section radius must produce finite curvature and sweep values.",
                                    sectionContext),
                                section.Radius.Value));
                    }
                }
            }

            ValidateFieldAbsent(section.StartCurvature, path + ".startCurvature", diagnostics, sectionContext);
            ValidateFieldAbsent(section.EndCurvature, path + ".endCurvature", diagnostics, sectionContext);
            ValidateFieldAbsent(section.InterpolationMode, path + ".interpolationMode", diagnostics, sectionContext);
            ValidateFieldAbsent(section.Degree, path + ".degree", diagnostics, sectionContext);
            ValidateFieldAbsent(section.ControlPoints, path + ".controlPoints", diagnostics, sectionContext);
            ValidateFieldAbsent(section.Weights, path + ".weights", diagnostics, sectionContext);
        }

        private static void ValidateCurvatureTransitionSection(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            string sectionContext = TrackLayoutPackageV2DiagnosticMessages.SectionContext(section, sectionIndex);
            ValidateFieldAbsent(section.Radius, path + ".radius", diagnostics, sectionContext);
            ValidateFieldAbsent(section.Degree, path + ".degree", diagnostics, sectionContext);
            ValidateFieldAbsent(section.ControlPoints, path + ".controlPoints", diagnostics, sectionContext);
            ValidateFieldAbsent(section.Weights, path + ".weights", diagnostics, sectionContext);

            bool startValid = ValidateRequiredFinite(
                section.StartCurvature,
                path + ".startCurvature",
                "Curvature-transition start curvature is required.",
                diagnostics,
                sectionContext);
            bool endValid = ValidateRequiredFinite(
                section.EndCurvature,
                path + ".endCurvature",
                "Curvature-transition end curvature is required.",
                diagnostics,
                sectionContext);

            if (string.IsNullOrWhiteSpace(section.InterpolationMode))
            {
                AddMissingField(
                    path + ".interpolationMode",
                    "Curvature-transition interpolation mode is required.",
                    diagnostics,
                    sectionContext);
            }
            else if (!TrackLayoutPackageV2Vocabulary.IsKnownCurvatureTransitionInterpolation(
                         section.InterpolationMode))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidCurvatureInterpolation,
                        path + ".interpolationMode",
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Curvature-transition interpolation mode must be linear.",
                            sectionContext)));
            }

            if (startValid && endValid && IsFinite(section.Length))
            {
                double curvatureDelta = section.EndCurvature!.Value - section.StartCurvature!.Value;
                double headingSweep = section.Length *
                    (section.StartCurvature.Value + (0.5 * curvatureDelta));
                if (!IsFinite(curvatureDelta))
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
                            path + ".curvatureDelta",
                            TrackLayoutPackageV2DiagnosticMessages.WithContext(
                                "Curvature range must be finite.",
                                sectionContext),
                            curvatureDelta));
                }

                if (!IsFinite(headingSweep))
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
                            path + ".headingSweep",
                            TrackLayoutPackageV2DiagnosticMessages.WithContext(
                                "Transition curvature must produce a finite heading sweep.",
                                sectionContext),
                            headingSweep));
                }
            }
        }

        private static void ValidateSpatialSection(
            TrackLayoutSectionV2Dto section,
            int sectionIndex,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            string sectionContext = TrackLayoutPackageV2DiagnosticMessages.SectionContext(section, sectionIndex);
            ValidateFieldAbsent(section.Radius, path + ".radius", diagnostics, sectionContext);
            ValidateFieldAbsent(section.StartCurvature, path + ".startCurvature", diagnostics, sectionContext);
            ValidateFieldAbsent(section.EndCurvature, path + ".endCurvature", diagnostics, sectionContext);
            ValidateFieldAbsent(section.InterpolationMode, path + ".interpolationMode", diagnostics, sectionContext);

            if (!section.Degree.HasValue)
            {
                AddMissingField(path + ".degree", "Spatial section degree is required.", diagnostics, sectionContext);
            }
            else if (section.Degree.Value < 1)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidSpatialDegree,
                        path + ".degree",
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Spatial section degree must be at least 1.",
                            sectionContext),
                        section.Degree.Value,
                        1.0));
            }

            TrackLayoutVector3dV2Dto[]? controlPoints = section.ControlPoints;
            if (controlPoints == null)
            {
                AddMissingField(
                    path + ".controlPoints",
                    "Spatial section control points are required.",
                    diagnostics,
                    sectionContext);
            }
            else
            {
                ValidateSpatialControlPoints(
                    controlPoints,
                    section.Degree,
                    path + ".controlPoints",
                    diagnostics,
                    sectionContext);
            }

            double[]? weights = section.Weights;
            if (weights == null)
            {
                AddMissingField(path + ".weights", "Spatial section weights are required.", diagnostics, sectionContext);
            }
            else
            {
                ValidateSpatialWeights(weights, controlPoints, path + ".weights", diagnostics, sectionContext);
            }

            if (controlPoints != null)
            {
                ValidateSpatialStartContract(controlPoints, path + ".controlPoints", diagnostics, sectionContext);
            }
        }

        private static void ValidateSpatialControlPoints(
            TrackLayoutVector3dV2Dto[] controlPoints,
            int? degree,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string sectionContext)
        {
            if (controlPoints.Length == 0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidSpatialControlPoints,
                        path,
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Spatial section control points cannot be empty.",
                            sectionContext)));
            }

            if (degree.HasValue && degree.Value >= 1 && controlPoints.Length <= degree.Value)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidSpatialControlPoints,
                        path,
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Spatial section control point count must be at least degree + 1.",
                            sectionContext),
                        controlPoints.Length,
                        degree.Value + 1.0));
            }

            for (int i = 0; i < controlPoints.Length; i++)
            {
                ValidateFiniteVector(controlPoints[i], path + "[" + i + "]", diagnostics, sectionContext);
            }
        }

        private static void ValidateSpatialWeights(
            double[] weights,
            TrackLayoutVector3dV2Dto[]? controlPoints,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string sectionContext)
        {
            if (controlPoints != null && weights.Length != controlPoints.Length)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidSpatialWeights,
                        path,
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Spatial section weight count must match control point count.",
                            sectionContext),
                        weights.Length,
                        controlPoints.Length));
            }

            for (int i = 0; i < weights.Length; i++)
            {
                bool finite = ValidateFinite(weights[i], path + "[" + i + "]", diagnostics, sectionContext);
                if (finite && weights[i] <= 0.0)
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.InvalidSpatialWeights,
                            path + "[" + i + "]",
                            TrackLayoutPackageV2DiagnosticMessages.WithContext(
                                "Spatial section weights must be finite and greater than zero.",
                                sectionContext),
                            weights[i],
                            0.0));
                }
            }
        }

        private static void ValidateSpatialStartContract(
            TrackLayoutVector3dV2Dto[] controlPoints,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string sectionContext)
        {
            if (controlPoints.Length < 2 || controlPoints[0] == null || controlPoints[1] == null)
            {
                return;
            }

            TrackLayoutVector3dV2Dto start = controlPoints[0];
            TrackLayoutVector3dV2Dto next = controlPoints[1];
            if (!IsFiniteVector(start) || !IsFiniteVector(next))
            {
                return;
            }

            if (start.X != 0.0 || start.Y != 0.0 || start.Z != 0.0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidSpatialStartContract,
                        path + "[0]",
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Spatial section local start point must be the origin.",
                            sectionContext)));
            }

            double dx = next.X - start.X;
            double dy = next.Y - start.Y;
            double dz = next.Z - start.Z;
            double directionLength = System.Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            if (directionLength <= MinimumSpatialStartTangentMagnitude)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidSpatialStartContract,
                        path + "[1]",
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Spatial section local start tangent must have non-zero magnitude and point along positive X.",
                            sectionContext),
                        directionLength,
                        MinimumSpatialStartTangentMagnitude,
                        MinimumSpatialStartTangentMagnitude));
                return;
            }

            double tangentX = dx / directionLength;
            double tangentY = dy / directionLength;
            double tangentZ = dz / directionLength;
            if (tangentX < 1.0 - SpatialStartTangentAlignmentTolerance ||
                System.Math.Abs(tangentY) > SpatialStartTangentAlignmentTolerance ||
                System.Math.Abs(tangentZ) > SpatialStartTangentAlignmentTolerance)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidSpatialStartContract,
                        path + "[1]",
                        TrackLayoutPackageV2DiagnosticMessages.WithContext(
                            "Spatial section local start tangent must point along positive X.",
                            sectionContext),
                        tangentX,
                        1.0,
                        SpatialStartTangentAlignmentTolerance));
            }
        }

        private static void ValidateBanking(
            TrackBankingV2Dto? banking,
            double totalLength,
            bool totalLengthValid,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (banking == null)
            {
                return;
            }

            TrackBankingKeyV2Dto[]? keys = banking.Keys;
            if (keys == null)
            {
                AddMissingField("banking.keys", "Banking keys collection is required.", diagnostics);
                return;
            }

            if (keys.Length < 2)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidBankingKeyCount,
                        "banking.keys",
                        "Authored banking requires at least two keys.",
                        keys.Length,
                        2.0));
            }

            bool keyDistancesValid = true;
            double previousDistance = double.NegativeInfinity;
            int previousDistanceIndex = -1;
            for (int i = 0; i < keys.Length; i++)
            {
                string path = "banking.keys[" + i + "]";
                TrackBankingKeyV2Dto? key = keys[i];
                if (key == null)
                {
                    AddMissingObject(
                        path,
                        diagnostics,
                        TrackLayoutPackageV2DiagnosticMessages.BankingKeyContext(i));
                    keyDistancesValid = false;
                    continue;
                }

                bool distanceFinite = ValidateFinite(
                    key.Distance,
                    path + ".distance",
                    diagnostics,
                    TrackLayoutPackageV2DiagnosticMessages.BankingKeyContext(i));
                keyDistancesValid &= distanceFinite;
                ValidateFinite(
                    key.RollRadians,
                    path + ".rollRadians",
                    diagnostics,
                    TrackLayoutPackageV2DiagnosticMessages.BankingKeyContext(i));

                if (string.IsNullOrWhiteSpace(key.InterpolationToNext))
                {
                    AddMissingField(
                        path + ".interpolationToNext",
                        "Banking interpolationToNext is required.",
                        diagnostics,
                        TrackLayoutPackageV2DiagnosticMessages.BankingKeyContext(i));
                }
                else if (!TrackLayoutPackageV2Vocabulary.IsKnownBankingInterpolation(key.InterpolationToNext))
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV2ValidationDiagnostic(
                            TrackLayoutPackageV2ValidationCode.InvalidBankingInterpolation,
                            path + ".interpolationToNext",
                            TrackLayoutPackageV2DiagnosticMessages.BankingKey(
                                i,
                                "Banking interpolationToNext must use the TrackLayoutPackageV2 stable vocabulary.")));
                }

                if (distanceFinite)
                {
                    if (key.Distance <= previousDistance)
                    {
                        diagnostics.Add(
                            new TrackLayoutPackageV2ValidationDiagnostic(
                                TrackLayoutPackageV2ValidationCode.InvalidBankingKeyOrder,
                                path + ".distance",
                                TrackLayoutPackageV2DiagnosticMessages.BankingKeyOrder(
                                    i,
                                    previousDistanceIndex,
                                    previousDistance),
                                key.Distance,
                                previousDistance));
                    }

                    previousDistance = key.Distance;
                    previousDistanceIndex = i;
                }
            }

            if (!totalLengthValid || !keyDistancesValid || keys.Length < 2)
            {
                return;
            }

            TrackBankingKeyV2Dto first = keys[0];
            TrackBankingKeyV2Dto last = keys[keys.Length - 1];
            if (first == null || last == null)
            {
                return;
            }

            if (first.Distance != 0.0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidBankingDomain,
                        "banking.keys[0].distance",
                        TrackLayoutPackageV2DiagnosticMessages.BankingKey(
                            0,
                            "Authored banking must start exactly at distance 0."),
                        first.Distance,
                        0.0));
            }

            if (last.Distance != totalLength)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidBankingDomain,
                        "banking.keys[" + (keys.Length - 1) + "].distance",
                        TrackLayoutPackageV2DiagnosticMessages.BankingFinalDomain(
                            keys.Length - 1,
                            totalLength),
                        last.Distance,
                        totalLength));
            }
        }

        private static void ValidateHeartline(
            TrackHeartlineV2Dto? heartline,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (heartline == null)
            {
                return;
            }

            if (!TrackLayoutPackageV2Vocabulary.IsKnownHeartlineKind(heartline.Kind))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidHeartlineKind,
                        "heartline.kind",
                        "Heartline kind must be constantOffset."));
            }

            if (!TrackLayoutPackageV2Vocabulary.IsKnownHeartlineDistanceDomain(heartline.DistanceDomain))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidHeartlineDistanceDomain,
                        "heartline.distanceDomain",
                        "Heartline distanceDomain must be centerlineStation."));
            }

            if (!TrackLayoutPackageV2Vocabulary.IsKnownHeartlineAxisSource(heartline.AxisSource))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidHeartlineAxisSource,
                        "heartline.axisSource",
                        "Heartline axisSource must be sampledFrame."));
            }

            ValidateFinite(heartline.NormalOffset, "heartline.normalOffset", diagnostics);
            ValidateFinite(heartline.LateralOffset, "heartline.lateralOffset", diagnostics);
        }

        private static bool ValidateRequiredFinite(
            double? value,
            string path,
            string missingMessage,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string? context = null)
        {
            if (!value.HasValue)
            {
                AddMissingField(path, missingMessage, diagnostics, context);
                return false;
            }

            return ValidateFinite(value.Value, path, diagnostics, context);
        }

        private static void ValidateFieldAbsent(
            object? value,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string? context = null)
        {
            if (value == null)
            {
                return;
            }

            diagnostics.Add(
                new TrackLayoutPackageV2ValidationDiagnostic(
                    TrackLayoutPackageV2ValidationCode.UnexpectedSectionField,
                    path,
                    TrackLayoutPackageV2DiagnosticMessages.WithContext(
                        "Section field is not valid for the selected section kind.",
                        context)));
        }

        private static void AddMissingField(
            string path,
            string message,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string? context = null)
        {
            diagnostics.Add(
                new TrackLayoutPackageV2ValidationDiagnostic(
                    TrackLayoutPackageV2ValidationCode.MissingRequiredField,
                    path,
                    TrackLayoutPackageV2DiagnosticMessages.WithContext(message, context)));
        }

        private static void AddMissingObject(
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string? context = null)
        {
            diagnostics.Add(
                new TrackLayoutPackageV2ValidationDiagnostic(
                    TrackLayoutPackageV2ValidationCode.MissingObject,
                    path,
                    TrackLayoutPackageV2DiagnosticMessages.WithContext(
                        "Required object is missing.",
                        context)));
        }

        private static bool ValidateBasisVector(
            TrackLayoutVector3dV2Dto? vector,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            if (!ValidateFiniteVector(vector, path, diagnostics))
            {
                return false;
            }

            double length = Length(vector!);
            if (length <= MinimumAxisMagnitude)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
                        path,
                        "Start pose basis axes must not have near-zero magnitude.",
                        length,
                        MinimumAxisMagnitude,
                        MinimumAxisMagnitude));
                return false;
            }

            return true;
        }

        private static void ValidateUnitLength(
            TrackLayoutVector3dV2Dto vector,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            double length = Length(vector);
            double delta = System.Math.Abs(length - 1.0);
            if (delta > UnitLengthTolerance)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
                        path,
                        "Start pose basis axes must be unit length; imports do not normalize inputs.",
                        length,
                        1.0,
                        UnitLengthTolerance));
            }
        }

        private static void ValidateOrthogonality(
            TrackLayoutVector3dV2Dto first,
            TrackLayoutVector3dV2Dto second,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics)
        {
            double dot = Dot(first, second);
            if (System.Math.Abs(dot) > OrthogonalityTolerance)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV2ValidationDiagnostic(
                        TrackLayoutPackageV2ValidationCode.InvalidStartPoseBasis,
                        path,
                        "Start pose basis axes must be mutually orthogonal.",
                        dot,
                        0.0,
                        OrthogonalityTolerance));
            }
        }

        private static bool ValidateFiniteVector(
            TrackLayoutVector3dV2Dto? vector,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string? context = null)
        {
            if (vector == null)
            {
                AddMissingObject(path, diagnostics, context);
                return false;
            }

            bool finite = true;
            finite &= ValidateFinite(vector.X, path + ".x", diagnostics, context);
            finite &= ValidateFinite(vector.Y, path + ".y", diagnostics, context);
            finite &= ValidateFinite(vector.Z, path + ".z", diagnostics, context);
            return finite;
        }

        private static bool ValidateFinite(
            double value,
            string path,
            List<TrackLayoutPackageV2ValidationDiagnostic> diagnostics,
            string? context = null)
        {
            if (IsFinite(value))
            {
                return true;
            }

            diagnostics.Add(
                new TrackLayoutPackageV2ValidationDiagnostic(
                    TrackLayoutPackageV2ValidationCode.NonFiniteNumber,
                    path,
                    TrackLayoutPackageV2DiagnosticMessages.WithContext(
                        "Numeric value must be finite.",
                        context),
                    value));
            return false;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteVector(TrackLayoutVector3dV2Dto? vector)
        {
            return vector != null &&
                   IsFinite(vector.X) &&
                   IsFinite(vector.Y) &&
                   IsFinite(vector.Z);
        }

        private static double Length(TrackLayoutVector3dV2Dto vector)
        {
            return System.Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        }

        private static double Dot(TrackLayoutVector3dV2Dto first, TrackLayoutVector3dV2Dto second)
        {
            return (first.X * second.X) + (first.Y * second.Y) + (first.Z * second.Z);
        }

        private static TrackLayoutVector3dV2Dto Cross(
            TrackLayoutVector3dV2Dto first,
            TrackLayoutVector3dV2Dto second)
        {
            return new TrackLayoutVector3dV2Dto
            {
                X = (first.Y * second.Z) - (first.Z * second.Y),
                Y = (first.Z * second.X) - (first.X * second.Z),
                Z = (first.X * second.Y) - (first.Y * second.X)
            };
        }
    }
}
