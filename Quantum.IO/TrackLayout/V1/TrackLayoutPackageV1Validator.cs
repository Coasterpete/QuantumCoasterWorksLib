using System;
using System.Collections.Generic;

namespace Quantum.IO.TrackLayout.V1
{
    public enum TrackLayoutPackageV1ValidationCode
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
        MalformedJson = 25,
        MappingFailed = 26
    }

    public sealed class TrackLayoutPackageV1ValidationDiagnostic
    {
        public TrackLayoutPackageV1ValidationDiagnostic(
            TrackLayoutPackageV1ValidationCode code,
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

        public TrackLayoutPackageV1ValidationCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public double? Value { get; }

        public double? Expected { get; }

        public double? Tolerance { get; }
    }

    public static class TrackLayoutPackageV1Validator
    {
        private const double MinimumAxisMagnitude = 1e-9;
        private const double UnitLengthTolerance = 1e-9;
        private const double OrthogonalityTolerance = 1e-9;
        private const double HandednessTolerance = 1e-9;
        private const double MinimumSpatialStartTangentMagnitude = 1e-9;
        private const double SpatialStartTangentAlignmentTolerance = 1e-9;

        public static IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> Validate(
            TrackLayoutPackageV1Dto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            var diagnostics = new List<TrackLayoutPackageV1ValidationDiagnostic>();

            ValidateContract(dto, diagnostics);
            ValidateMetadata(dto.Metadata, diagnostics);
            ValidateStartPose(dto.StartPose, diagnostics);

            double totalLength = 0.0;
            bool totalLengthValid = ValidateSections(dto.Sections, diagnostics, out totalLength);
            ValidateBanking(dto.Banking, totalLength, totalLengthValid, diagnostics);

            return diagnostics.ToArray();
        }

        public static bool TryValidate(
            TrackLayoutPackageV1Dto dto,
            out IReadOnlyList<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            diagnostics = Validate(dto);
            return diagnostics.Count == 0;
        }

        private static void ValidateContract(
            TrackLayoutPackageV1Dto dto,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (!string.Equals(dto.Contract, TrackLayoutPackageV1Dto.ContractName, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidContract,
                        "contract",
                        "Contract name must match TrackLayoutPackageV1."));
            }

            if (dto.Version != TrackLayoutPackageV1Dto.ContractVersion)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidVersion,
                        "version",
                        "Version must match TrackLayoutPackageV1.",
                        dto.Version,
                        TrackLayoutPackageV1Dto.ContractVersion));
            }
        }

        private static void ValidateMetadata(
            TrackLayoutMetadataV1Dto? metadata,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (metadata == null)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.MissingMetadata,
                        "metadata",
                        "Metadata object is required."));
            }
        }

        private static void ValidateStartPose(
            TrackStartPoseV1Dto? startPose,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (startPose == null)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.MissingStartPose,
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
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidStartPoseBasis,
                        "startPose.handedness",
                        "Start pose basis must satisfy binormal ~= tangent x normal.",
                        handedness,
                        1.0,
                        HandednessTolerance));
            }
        }

        private static bool ValidateSections(
            TrackLayoutSectionV1Dto[]? sections,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics,
            out double totalLength)
        {
            totalLength = 0.0;
            bool totalLengthValid = true;

            if (sections == null)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.MissingSections,
                        "sections",
                        "Sections collection is required."));
                return false;
            }

            if (sections.Length == 0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.EmptySections,
                        "sections",
                        "At least one section is required."));
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sections.Length; i++)
            {
                string path = "sections[" + i + "]";
                TrackLayoutSectionV1Dto? section = sections[i];
                if (section == null)
                {
                    AddMissingObject(path, diagnostics);
                    continue;
                }

                ValidateSectionCommon(section, path, ids, diagnostics);
                ValidateSectionByKind(section, path, diagnostics);

                if (IsFinite(section.Length))
                {
                    totalLength += section.Length;
                    if (!IsFinite(totalLength))
                    {
                        totalLengthValid = false;
                        diagnostics.Add(
                            new TrackLayoutPackageV1ValidationDiagnostic(
                                TrackLayoutPackageV1ValidationCode.NonFiniteNumber,
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
            TrackLayoutSectionV1Dto section,
            string path,
            HashSet<string> ids,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(section.Kind))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.MissingRequiredField,
                        path + ".kind",
                        "Section kind is required."));
            }

            if (string.IsNullOrWhiteSpace(section.Id))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.MissingSectionId,
                        path + ".id",
                        "Section ID is required and must not be blank."));
            }
            else if (!ids.Add(section.Id))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.DuplicateSectionId,
                        path + ".id",
                        "Section IDs must be unique using ordinal comparison."));
            }

            bool lengthFinite = ValidateFinite(section.Length, path + ".length", diagnostics);
            if (lengthFinite && section.Length <= 0.0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.NonPositiveLength,
                        path + ".length",
                        "Section length must be finite and greater than zero.",
                        section.Length,
                        0.0));
            }

            ValidateFinite(section.RollRadians, path + ".rollRadians", diagnostics);
        }

        private static void ValidateSectionByKind(
            TrackLayoutSectionV1Dto section,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            switch (section.Kind)
            {
                case TrackLayoutPackageV1Vocabulary.StraightSectionKind:
                    ValidateStraightSection(section, path, diagnostics);
                    return;

                case TrackLayoutPackageV1Vocabulary.ConstantCurvatureSectionKind:
                    ValidateConstantCurvatureSection(section, path, diagnostics);
                    return;

                case TrackLayoutPackageV1Vocabulary.CurvatureTransitionSectionKind:
                    ValidateCurvatureTransitionSection(section, path, diagnostics);
                    return;

                case TrackLayoutPackageV1Vocabulary.SpatialSectionKind:
                    ValidateSpatialSection(section, path, diagnostics);
                    return;

                default:
                    diagnostics.Add(
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.UnknownSectionKind,
                            path + ".kind",
                            "Section kind must use the TrackLayoutPackageV1 stable vocabulary."));
                    return;
            }
        }

        private static void ValidateStraightSection(
            TrackLayoutSectionV1Dto section,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            ValidateFieldAbsent(section.Radius, path + ".radius", diagnostics);
            ValidateFieldAbsent(section.StartCurvature, path + ".startCurvature", diagnostics);
            ValidateFieldAbsent(section.EndCurvature, path + ".endCurvature", diagnostics);
            ValidateFieldAbsent(section.InterpolationMode, path + ".interpolationMode", diagnostics);
            ValidateFieldAbsent(section.Degree, path + ".degree", diagnostics);
            ValidateFieldAbsent(section.ControlPoints, path + ".controlPoints", diagnostics);
            ValidateFieldAbsent(section.Weights, path + ".weights", diagnostics);
        }

        private static void ValidateConstantCurvatureSection(
            TrackLayoutSectionV1Dto section,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (!section.Radius.HasValue)
            {
                AddMissingField(path + ".radius", "Constant-curvature section radius is required.", diagnostics);
            }
            else
            {
                bool finite = ValidateFinite(section.Radius.Value, path + ".radius", diagnostics);
                if (finite && section.Radius.Value == 0.0)
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.InvalidRadius,
                            path + ".radius",
                            "Constant-curvature section radius must be non-zero.",
                            section.Radius.Value));
                }

                if (finite && IsFinite(section.Length))
                {
                    double curvature = 1.0 / section.Radius.Value;
                    double sweep = section.Length * curvature;
                    if (!IsFinite(curvature) || !IsFinite(sweep))
                    {
                        diagnostics.Add(
                            new TrackLayoutPackageV1ValidationDiagnostic(
                                TrackLayoutPackageV1ValidationCode.InvalidRadius,
                                path + ".radius",
                                "Constant-curvature section radius must produce finite curvature and sweep values.",
                                section.Radius.Value));
                    }
                }
            }

            ValidateFieldAbsent(section.StartCurvature, path + ".startCurvature", diagnostics);
            ValidateFieldAbsent(section.EndCurvature, path + ".endCurvature", diagnostics);
            ValidateFieldAbsent(section.InterpolationMode, path + ".interpolationMode", diagnostics);
            ValidateFieldAbsent(section.Degree, path + ".degree", diagnostics);
            ValidateFieldAbsent(section.ControlPoints, path + ".controlPoints", diagnostics);
            ValidateFieldAbsent(section.Weights, path + ".weights", diagnostics);
        }

        private static void ValidateCurvatureTransitionSection(
            TrackLayoutSectionV1Dto section,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            ValidateFieldAbsent(section.Radius, path + ".radius", diagnostics);
            ValidateFieldAbsent(section.Degree, path + ".degree", diagnostics);
            ValidateFieldAbsent(section.ControlPoints, path + ".controlPoints", diagnostics);
            ValidateFieldAbsent(section.Weights, path + ".weights", diagnostics);

            bool startValid = ValidateRequiredFinite(
                section.StartCurvature,
                path + ".startCurvature",
                "Curvature-transition start curvature is required.",
                diagnostics);
            bool endValid = ValidateRequiredFinite(
                section.EndCurvature,
                path + ".endCurvature",
                "Curvature-transition end curvature is required.",
                diagnostics);

            if (string.IsNullOrWhiteSpace(section.InterpolationMode))
            {
                AddMissingField(
                    path + ".interpolationMode",
                    "Curvature-transition interpolation mode is required.",
                    diagnostics);
            }
            else if (!TrackLayoutPackageV1Vocabulary.IsKnownCurvatureTransitionInterpolation(
                         section.InterpolationMode))
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidCurvatureInterpolation,
                        path + ".interpolationMode",
                        "Curvature-transition interpolation mode must be linear."));
            }

            if (startValid && endValid && IsFinite(section.Length))
            {
                double curvatureDelta = section.EndCurvature!.Value - section.StartCurvature!.Value;
                double headingSweep = section.Length *
                    (section.StartCurvature.Value + (0.5 * curvatureDelta));
                if (!IsFinite(curvatureDelta))
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.NonFiniteNumber,
                            path + ".curvatureDelta",
                            "Curvature range must be finite.",
                            curvatureDelta));
                }

                if (!IsFinite(headingSweep))
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.NonFiniteNumber,
                            path + ".headingSweep",
                            "Transition curvature must produce a finite heading sweep.",
                            headingSweep));
                }
            }
        }

        private static void ValidateSpatialSection(
            TrackLayoutSectionV1Dto section,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            ValidateFieldAbsent(section.Radius, path + ".radius", diagnostics);
            ValidateFieldAbsent(section.StartCurvature, path + ".startCurvature", diagnostics);
            ValidateFieldAbsent(section.EndCurvature, path + ".endCurvature", diagnostics);
            ValidateFieldAbsent(section.InterpolationMode, path + ".interpolationMode", diagnostics);

            if (!section.Degree.HasValue)
            {
                AddMissingField(path + ".degree", "Spatial section degree is required.", diagnostics);
            }
            else if (section.Degree.Value < 1)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidSpatialDegree,
                        path + ".degree",
                        "Spatial section degree must be at least 1.",
                        section.Degree.Value,
                        1.0));
            }

            TrackLayoutVector3dV1Dto[]? controlPoints = section.ControlPoints;
            if (controlPoints == null)
            {
                AddMissingField(
                    path + ".controlPoints",
                    "Spatial section control points are required.",
                    diagnostics);
            }
            else
            {
                ValidateSpatialControlPoints(controlPoints, section.Degree, path + ".controlPoints", diagnostics);
            }

            double[]? weights = section.Weights;
            if (weights == null)
            {
                AddMissingField(path + ".weights", "Spatial section weights are required.", diagnostics);
            }
            else
            {
                ValidateSpatialWeights(weights, controlPoints, path + ".weights", diagnostics);
            }

            if (controlPoints != null)
            {
                ValidateSpatialStartContract(controlPoints, path + ".controlPoints", diagnostics);
            }
        }

        private static void ValidateSpatialControlPoints(
            TrackLayoutVector3dV1Dto[] controlPoints,
            int? degree,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (controlPoints.Length == 0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidSpatialControlPoints,
                        path,
                        "Spatial section control points cannot be empty."));
            }

            if (degree.HasValue && degree.Value >= 1 && controlPoints.Length <= degree.Value)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidSpatialControlPoints,
                        path,
                        "Spatial section control point count must be at least degree + 1.",
                        controlPoints.Length,
                        degree.Value + 1.0));
            }

            for (int i = 0; i < controlPoints.Length; i++)
            {
                ValidateFiniteVector(controlPoints[i], path + "[" + i + "]", diagnostics);
            }
        }

        private static void ValidateSpatialWeights(
            double[] weights,
            TrackLayoutVector3dV1Dto[]? controlPoints,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (controlPoints != null && weights.Length != controlPoints.Length)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidSpatialWeights,
                        path,
                        "Spatial section weight count must match control point count.",
                        weights.Length,
                        controlPoints.Length));
            }

            for (int i = 0; i < weights.Length; i++)
            {
                bool finite = ValidateFinite(weights[i], path + "[" + i + "]", diagnostics);
                if (finite && weights[i] <= 0.0)
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.InvalidSpatialWeights,
                            path + "[" + i + "]",
                            "Spatial section weights must be finite and greater than zero.",
                            weights[i],
                            0.0));
                }
            }
        }

        private static void ValidateSpatialStartContract(
            TrackLayoutVector3dV1Dto[] controlPoints,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (controlPoints.Length < 2 || controlPoints[0] == null || controlPoints[1] == null)
            {
                return;
            }

            TrackLayoutVector3dV1Dto start = controlPoints[0];
            TrackLayoutVector3dV1Dto next = controlPoints[1];
            if (!IsFiniteVector(start) || !IsFiniteVector(next))
            {
                return;
            }

            if (start.X != 0.0 || start.Y != 0.0 || start.Z != 0.0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidSpatialStartContract,
                        path + "[0]",
                        "Spatial section local start point must be the origin."));
            }

            double dx = next.X - start.X;
            double dy = next.Y - start.Y;
            double dz = next.Z - start.Z;
            double directionLength = System.Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            if (directionLength <= MinimumSpatialStartTangentMagnitude)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidSpatialStartContract,
                        path + "[1]",
                        "Spatial section local start tangent must have non-zero magnitude and point along positive X.",
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
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidSpatialStartContract,
                        path + "[1]",
                        "Spatial section local start tangent must point along positive X.",
                        tangentX,
                        1.0,
                        SpatialStartTangentAlignmentTolerance));
            }
        }

        private static void ValidateBanking(
            TrackBankingV1Dto? banking,
            double totalLength,
            bool totalLengthValid,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (banking == null)
            {
                return;
            }

            TrackBankingKeyV1Dto[]? keys = banking.Keys;
            if (keys == null)
            {
                AddMissingField("banking.keys", "Banking keys collection is required.", diagnostics);
                return;
            }

            if (keys.Length < 2)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidBankingKeyCount,
                        "banking.keys",
                        "Authored banking requires at least two keys.",
                        keys.Length,
                        2.0));
            }

            bool keyDistancesValid = true;
            double previousDistance = double.NegativeInfinity;
            for (int i = 0; i < keys.Length; i++)
            {
                string path = "banking.keys[" + i + "]";
                TrackBankingKeyV1Dto? key = keys[i];
                if (key == null)
                {
                    AddMissingObject(path, diagnostics);
                    keyDistancesValid = false;
                    continue;
                }

                bool distanceFinite = ValidateFinite(key.Distance, path + ".distance", diagnostics);
                keyDistancesValid &= distanceFinite;
                ValidateFinite(key.RollRadians, path + ".rollRadians", diagnostics);

                if (string.IsNullOrWhiteSpace(key.InterpolationToNext))
                {
                    AddMissingField(
                        path + ".interpolationToNext",
                        "Banking interpolationToNext is required.",
                        diagnostics);
                }
                else if (!TrackLayoutPackageV1Vocabulary.IsKnownBankingInterpolation(key.InterpolationToNext))
                {
                    diagnostics.Add(
                        new TrackLayoutPackageV1ValidationDiagnostic(
                            TrackLayoutPackageV1ValidationCode.InvalidBankingInterpolation,
                            path + ".interpolationToNext",
                            "Banking interpolationToNext must use the TrackLayoutPackageV1 stable vocabulary."));
                }

                if (distanceFinite)
                {
                    if (key.Distance <= previousDistance)
                    {
                        diagnostics.Add(
                            new TrackLayoutPackageV1ValidationDiagnostic(
                                TrackLayoutPackageV1ValidationCode.InvalidBankingKeyOrder,
                                path + ".distance",
                                "Banking key distances must be strictly increasing.",
                                key.Distance,
                                previousDistance));
                    }

                    previousDistance = key.Distance;
                }
            }

            if (!totalLengthValid || !keyDistancesValid || keys.Length < 2)
            {
                return;
            }

            TrackBankingKeyV1Dto first = keys[0];
            TrackBankingKeyV1Dto last = keys[keys.Length - 1];
            if (first == null || last == null)
            {
                return;
            }

            if (first.Distance != 0.0)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidBankingDomain,
                        "banking.keys[0].distance",
                        "Authored banking must start exactly at distance 0.",
                        first.Distance,
                        0.0));
            }

            if (last.Distance != totalLength)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidBankingDomain,
                        "banking.keys[" + (keys.Length - 1) + "].distance",
                        "Authored banking must end exactly at total section length.",
                        last.Distance,
                        totalLength));
            }
        }

        private static bool ValidateRequiredFinite(
            double? value,
            string path,
            string missingMessage,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (!value.HasValue)
            {
                AddMissingField(path, missingMessage, diagnostics);
                return false;
            }

            return ValidateFinite(value.Value, path, diagnostics);
        }

        private static void ValidateFieldAbsent(
            object? value,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (value == null)
            {
                return;
            }

            diagnostics.Add(
                new TrackLayoutPackageV1ValidationDiagnostic(
                    TrackLayoutPackageV1ValidationCode.UnexpectedSectionField,
                    path,
                    "Section field is not valid for the selected section kind."));
        }

        private static void AddMissingField(
            string path,
            string message,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            diagnostics.Add(
                new TrackLayoutPackageV1ValidationDiagnostic(
                    TrackLayoutPackageV1ValidationCode.MissingRequiredField,
                    path,
                    message));
        }

        private static void AddMissingObject(
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            diagnostics.Add(
                new TrackLayoutPackageV1ValidationDiagnostic(
                    TrackLayoutPackageV1ValidationCode.MissingObject,
                    path,
                    "Required object is missing."));
        }

        private static bool ValidateBasisVector(
            TrackLayoutVector3dV1Dto? vector,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (!ValidateFiniteVector(vector, path, diagnostics))
            {
                return false;
            }

            double length = Length(vector!);
            if (length <= MinimumAxisMagnitude)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidStartPoseBasis,
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
            TrackLayoutVector3dV1Dto vector,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            double length = Length(vector);
            double delta = System.Math.Abs(length - 1.0);
            if (delta > UnitLengthTolerance)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidStartPoseBasis,
                        path,
                        "Start pose basis axes must be unit length; imports do not normalize inputs.",
                        length,
                        1.0,
                        UnitLengthTolerance));
            }
        }

        private static void ValidateOrthogonality(
            TrackLayoutVector3dV1Dto first,
            TrackLayoutVector3dV1Dto second,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            double dot = Dot(first, second);
            if (System.Math.Abs(dot) > OrthogonalityTolerance)
            {
                diagnostics.Add(
                    new TrackLayoutPackageV1ValidationDiagnostic(
                        TrackLayoutPackageV1ValidationCode.InvalidStartPoseBasis,
                        path,
                        "Start pose basis axes must be mutually orthogonal.",
                        dot,
                        0.0,
                        OrthogonalityTolerance));
            }
        }

        private static bool ValidateFiniteVector(
            TrackLayoutVector3dV1Dto? vector,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (vector == null)
            {
                AddMissingObject(path, diagnostics);
                return false;
            }

            bool finite = true;
            finite &= ValidateFinite(vector.X, path + ".x", diagnostics);
            finite &= ValidateFinite(vector.Y, path + ".y", diagnostics);
            finite &= ValidateFinite(vector.Z, path + ".z", diagnostics);
            return finite;
        }

        private static bool ValidateFinite(
            double value,
            string path,
            List<TrackLayoutPackageV1ValidationDiagnostic> diagnostics)
        {
            if (IsFinite(value))
            {
                return true;
            }

            diagnostics.Add(
                new TrackLayoutPackageV1ValidationDiagnostic(
                    TrackLayoutPackageV1ValidationCode.NonFiniteNumber,
                    path,
                    "Numeric value must be finite.",
                    value));
            return false;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteVector(TrackLayoutVector3dV1Dto? vector)
        {
            return vector != null &&
                   IsFinite(vector.X) &&
                   IsFinite(vector.Y) &&
                   IsFinite(vector.Z);
        }

        private static double Length(TrackLayoutVector3dV1Dto vector)
        {
            return System.Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        }

        private static double Dot(TrackLayoutVector3dV1Dto first, TrackLayoutVector3dV1Dto second)
        {
            return (first.X * second.X) + (first.Y * second.Y) + (first.Z * second.Z);
        }

        private static TrackLayoutVector3dV1Dto Cross(
            TrackLayoutVector3dV1Dto first,
            TrackLayoutVector3dV1Dto second)
        {
            return new TrackLayoutVector3dV1Dto
            {
                X = (first.Y * second.Z) - (first.Z * second.Y),
                Y = (first.Z * second.X) - (first.X * second.Z),
                Z = (first.X * second.Y) - (first.Y * second.X)
            };
        }
    }
}
