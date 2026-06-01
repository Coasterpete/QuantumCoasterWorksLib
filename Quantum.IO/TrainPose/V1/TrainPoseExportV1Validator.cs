using System;
using System.Collections.Generic;
using Quantum.Math;

namespace Quantum.IO.TrainPose.V1
{
    public enum TrainPoseExportV1ValidationCode
    {
        NonFiniteNumber = 0,
        ZeroLengthBasisVector = 1,
        NonOrthonormalBasis = 2,
        InvalidMatrixBottomRow = 3,
        NegativeGeometryDimension = 4,
        NegativeWheelRadius = 5,
        NegativeWheelWidth = 6,
        MatrixFrameMismatch = 7,
        InvalidBasisHandedness = 8
    }

    public sealed class TrainPoseExportV1ValidationDiagnostic
    {
        public TrainPoseExportV1ValidationDiagnostic(
            TrainPoseExportV1ValidationCode code,
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

        public TrainPoseExportV1ValidationCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public double? Value { get; }

        public double? Expected { get; }

        public double? Tolerance { get; }
    }

    public sealed class TrainPoseExportV1ValidationOptions
    {
        public double ZeroVectorLengthTolerance { get; set; } = MathUtil.Epsilon;

        public double UnitVectorLengthTolerance { get; set; } = 1e-6;

        public double OrthogonalityTolerance { get; set; } = 1e-6;

        public double HandednessTolerance { get; set; } = 1e-6;

        public bool ValidateMatrixBottomRow { get; set; } = false;

        public double MatrixBottomRowTolerance { get; set; } = 1e-6;

        public bool ValidateMatrixFrameConsistency { get; set; } = false;

        public double MatrixFrameTolerance { get; set; } = 1e-5;
    }

    public static class TrainPoseExportV1Validator
    {
        public static IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> Validate(
            TrainPoseExportV1Dto dto,
            TrainPoseExportV1ValidationOptions? options = null)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            TrainPoseExportV1ValidationOptions effectiveOptions = options ?? new TrainPoseExportV1ValidationOptions();
            ValidateOptions(effectiveOptions);

            var diagnostics = new List<TrainPoseExportV1ValidationDiagnostic>();

            ValidateFinite(dto.LeadDistance, "leadDistance", diagnostics);
            ValidateDefinition(dto.Definition, "definition", diagnostics);
            ValidateCars(dto.Cars, "cars", diagnostics, effectiveOptions);

            return diagnostics.ToArray();
        }

        public static bool TryValidate(
            TrainPoseExportV1Dto dto,
            out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions? options = null)
        {
            diagnostics = Validate(dto, options);
            return diagnostics.Count == 0;
        }

        private static void ValidateOptions(TrainPoseExportV1ValidationOptions options)
        {
            if (!IsFinite(options.ZeroVectorLengthTolerance) || options.ZeroVectorLengthTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.ZeroVectorLengthTolerance,
                    "Zero vector length tolerance must be finite and non-negative.");
            }

            if (!IsFinite(options.UnitVectorLengthTolerance) || options.UnitVectorLengthTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.UnitVectorLengthTolerance,
                    "Unit vector length tolerance must be finite and non-negative.");
            }

            if (!IsFinite(options.OrthogonalityTolerance) || options.OrthogonalityTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.OrthogonalityTolerance,
                    "Orthogonality tolerance must be finite and non-negative.");
            }

            if (!IsFinite(options.HandednessTolerance) || options.HandednessTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.HandednessTolerance,
                    "Handedness tolerance must be finite and non-negative.");
            }

            if (!IsFinite(options.MatrixBottomRowTolerance) || options.MatrixBottomRowTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.MatrixBottomRowTolerance,
                    "Matrix bottom-row tolerance must be finite and non-negative.");
            }

            if (!IsFinite(options.MatrixFrameTolerance) || options.MatrixFrameTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.MatrixFrameTolerance,
                    "Matrix/frame consistency tolerance must be finite and non-negative.");
            }
        }

        private static void ValidateDefinition(
            TrainConsistDefinitionV1Dto? definition,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics)
        {
            if (definition == null)
            {
                return;
            }

            ValidateFinite(definition.CarSpacing, path + ".carSpacing", diagnostics);
            ValidateGeometry(definition.CarGeometry, path + ".carGeometry", diagnostics);

            if (definition.BogieLayout != null)
            {
                ValidateFinite(definition.BogieLayout.BogieSpacing, path + ".bogieLayout.bogieSpacing", diagnostics);
            }

            ValidateWheelLayout(definition.WheelLayout, path + ".wheelLayout", diagnostics);
        }

        private static void ValidateGeometry(
            TrainCarGeometryV1Dto? geometry,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics)
        {
            if (geometry == null)
            {
                return;
            }

            ValidateFinite(geometry.Length, path + ".length", diagnostics);
            ValidateFinite(geometry.Width, path + ".width", diagnostics);
            ValidateFinite(geometry.Height, path + ".height", diagnostics);

            ValidateNonNegativeGeometryValue(geometry.Length, path + ".length", diagnostics);
            ValidateNonNegativeGeometryValue(geometry.Width, path + ".width", diagnostics);
            ValidateNonNegativeGeometryValue(geometry.Height, path + ".height", diagnostics);
        }

        private static void ValidateNonNegativeGeometryValue(
            double value,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics)
        {
            if (value < 0.0)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.NegativeGeometryDimension,
                        path,
                        "Geometry dimension must not be negative.",
                        value));
            }
        }

        private static void ValidateWheelLayout(
            TrainWheelLayoutV1Dto? wheelLayout,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics)
        {
            if (wheelLayout == null)
            {
                return;
            }

            ValidateFinite(wheelLayout.WheelRadius, path + ".wheelRadius", diagnostics);
            ValidateFinite(wheelLayout.WheelWidth, path + ".wheelWidth", diagnostics);
            ValidateFinite(wheelLayout.AxleSpacing, path + ".axleSpacing", diagnostics);

            if (wheelLayout.WheelRadius < 0.0)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.NegativeWheelRadius,
                        path + ".wheelRadius",
                        "Wheel radius must not be negative.",
                        wheelLayout.WheelRadius));
            }

            if (wheelLayout.WheelWidth < 0.0)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.NegativeWheelWidth,
                        path + ".wheelWidth",
                        "Wheel width must not be negative.",
                        wheelLayout.WheelWidth));
            }
        }

        private static void ValidateCars(
            ArticulatedTrainCarWithWheelsV1Dto[]? cars,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (cars == null)
            {
                return;
            }

            for (int i = 0; i < cars.Length; i++)
            {
                ArticulatedTrainCarWithWheelsV1Dto? car = cars[i];
                if (car == null)
                {
                    continue;
                }

                string carPath = path + "[" + i + "]";
                ValidateArticulatedBody(car.Body, carPath + ".body", diagnostics, options);
                ValidateTrainBogieWithWheels(car.FrontBogie, carPath + ".frontBogie", diagnostics, options);
                ValidateTrainBogieWithWheels(car.RearBogie, carPath + ".rearBogie", diagnostics, options);
            }
        }

        private static void ValidateArticulatedBody(
            ArticulatedTrainCarV1Dto? body,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (body == null)
            {
                return;
            }

            ValidateTrainCarTransform(body.OriginalBody, path + ".originalBody", diagnostics, options);
            ValidateBogieTransform(body.FrontBogie, path + ".frontBogie", diagnostics, options);
            ValidateBogieTransform(body.RearBogie, path + ".rearBogie", diagnostics, options);
            ValidateTrackFrame(body.ArticulatedFrame, path + ".articulatedFrame", diagnostics, options);
            ValidateMatrix(body.ArticulatedMatrix, path + ".articulatedMatrix", diagnostics, options);
            ValidateMatrixFrameConsistency(
                body.ArticulatedFrame,
                body.ArticulatedMatrix,
                path + ".articulatedMatrix",
                diagnostics,
                options);
            ValidateFinite(body.CenterDistance, path + ".centerDistance", diagnostics);
        }

        private static void ValidateTrainCarTransform(
            TrainCarTransformV1Dto? transform,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (transform == null)
            {
                return;
            }

            ValidateFinite(transform.Distance, path + ".distance", diagnostics);
            ValidateTrackFrame(transform.Frame, path + ".frame", diagnostics, options);
            ValidateMatrix(transform.Matrix, path + ".matrix", diagnostics, options);
            ValidateMatrixFrameConsistency(transform.Frame, transform.Matrix, path + ".matrix", diagnostics, options);
        }

        private static void ValidateTrainBogieWithWheels(
            TrainBogieWithWheelsV1Dto? transform,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (transform == null)
            {
                return;
            }

            ValidateBogieTransform(transform.Bogie, path + ".bogie", diagnostics, options);

            WheelTransformV1Dto[]? wheels = transform.Wheels;
            if (wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                ValidateWheelTransform(wheels[i], path + ".wheels[" + i + "]", diagnostics, options);
            }
        }

        private static void ValidateBogieTransform(
            BogieTransformV1Dto? transform,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (transform == null)
            {
                return;
            }

            ValidateFinite(transform.Distance, path + ".distance", diagnostics);
            ValidateTrackFrame(transform.Frame, path + ".frame", diagnostics, options);
            ValidateMatrix(transform.Matrix, path + ".matrix", diagnostics, options);
            ValidateMatrixFrameConsistency(transform.Frame, transform.Matrix, path + ".matrix", diagnostics, options);
        }

        private static void ValidateWheelTransform(
            WheelTransformV1Dto? transform,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (transform == null)
            {
                return;
            }

            ValidateFinite(transform.LocalOffsetX, path + ".localOffsetX", diagnostics);
            ValidateFinite(transform.LocalOffsetY, path + ".localOffsetY", diagnostics);
            ValidateFinite(transform.LocalOffsetZ, path + ".localOffsetZ", diagnostics);
            ValidateTrackFrame(transform.Frame, path + ".frame", diagnostics, options);
            ValidateMatrix(transform.Matrix, path + ".matrix", diagnostics, options);
            ValidateMatrixFrameConsistency(transform.Frame, transform.Matrix, path + ".matrix", diagnostics, options);
        }

        private static void ValidateTrackFrame(
            TrackFrameV1Dto? frame,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (frame == null)
            {
                return;
            }

            ValidateFinite(frame.Distance, path + ".distance", diagnostics);
            ValidateFiniteVector(frame.Position, path + ".position", diagnostics);

            bool tangentValid = ValidateBasisVector(frame.Tangent, path + ".tangent", diagnostics, options);
            bool normalValid = ValidateBasisVector(frame.Normal, path + ".normal", diagnostics, options);
            bool binormalValid = ValidateBasisVector(frame.Binormal, path + ".binormal", diagnostics, options);

            if (tangentValid && normalValid && binormalValid)
            {
                ValidateOrthonormalBasis(frame, path, diagnostics, options);
            }
        }

        private static bool ValidateBasisVector(
            Vector3dV1Dto? vector,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (!ValidateFiniteVector(vector, path, diagnostics))
            {
                return false;
            }

            double length = System.Math.Sqrt(vector!.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
            if (length <= options.ZeroVectorLengthTolerance)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.ZeroLengthBasisVector,
                        path,
                        "Basis vector length must be greater than tolerance.",
                        length,
                        options.ZeroVectorLengthTolerance,
                        options.ZeroVectorLengthTolerance));
                return false;
            }

            return true;
        }

        private static void ValidateOrthonormalBasis(
            TrackFrameV1Dto frame,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            double tangentLength = Length(frame.Tangent);
            double normalLength = Length(frame.Normal);
            double binormalLength = Length(frame.Binormal);

            ValidateUnitLength(
                tangentLength,
                path + ".tangent",
                diagnostics,
                options.UnitVectorLengthTolerance);
            ValidateUnitLength(
                normalLength,
                path + ".normal",
                diagnostics,
                options.UnitVectorLengthTolerance);
            ValidateUnitLength(
                binormalLength,
                path + ".binormal",
                diagnostics,
                options.UnitVectorLengthTolerance);

            ValidateOrthogonality(
                Dot(frame.Tangent, frame.Normal),
                path + ".tangentNormalDot",
                diagnostics,
                options.OrthogonalityTolerance);
            ValidateOrthogonality(
                Dot(frame.Tangent, frame.Binormal),
                path + ".tangentBinormalDot",
                diagnostics,
                options.OrthogonalityTolerance);
            ValidateOrthogonality(
                Dot(frame.Normal, frame.Binormal),
                path + ".normalBinormalDot",
                diagnostics,
                options.OrthogonalityTolerance);

            double handedness = Dot(Cross(frame.Tangent, frame.Normal), frame.Binormal);
            double handednessDelta = System.Math.Abs(handedness - 1.0);
            if (handednessDelta > options.HandednessTolerance)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.InvalidBasisHandedness,
                        path + ".handedness",
                        "Basis must satisfy binormal ~= tangent x normal.",
                        handedness,
                        1.0,
                        options.HandednessTolerance));
            }
        }

        private static void ValidateUnitLength(
            double length,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            double tolerance)
        {
            double delta = System.Math.Abs(length - 1.0);
            if (delta > tolerance)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.NonOrthonormalBasis,
                        path,
                        "Basis vector length must be approximately 1.",
                        length,
                        1.0,
                        tolerance));
            }
        }

        private static void ValidateOrthogonality(
            double dot,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            double tolerance)
        {
            double absoluteDot = System.Math.Abs(dot);
            if (absoluteDot > tolerance)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.NonOrthonormalBasis,
                        path,
                        "Basis vectors must be approximately orthogonal.",
                        dot,
                        0.0,
                        tolerance));
            }
        }

        private static void ValidateMatrix(
            Matrix4x4V1Dto? matrix,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (matrix == null)
            {
                return;
            }

            ValidateFinite(matrix.M11, path + ".m11", diagnostics);
            ValidateFinite(matrix.M12, path + ".m12", diagnostics);
            ValidateFinite(matrix.M13, path + ".m13", diagnostics);
            ValidateFinite(matrix.M14, path + ".m14", diagnostics);
            ValidateFinite(matrix.M21, path + ".m21", diagnostics);
            ValidateFinite(matrix.M22, path + ".m22", diagnostics);
            ValidateFinite(matrix.M23, path + ".m23", diagnostics);
            ValidateFinite(matrix.M24, path + ".m24", diagnostics);
            ValidateFinite(matrix.M31, path + ".m31", diagnostics);
            ValidateFinite(matrix.M32, path + ".m32", diagnostics);
            ValidateFinite(matrix.M33, path + ".m33", diagnostics);
            ValidateFinite(matrix.M34, path + ".m34", diagnostics);
            ValidateFinite(matrix.M41, path + ".m41", diagnostics);
            ValidateFinite(matrix.M42, path + ".m42", diagnostics);
            ValidateFinite(matrix.M43, path + ".m43", diagnostics);
            ValidateFinite(matrix.M44, path + ".m44", diagnostics);

            if (options.ValidateMatrixBottomRow)
            {
                ValidateMatrixBottomRowValue(matrix.M41, 0.0, path + ".m41", diagnostics, options.MatrixBottomRowTolerance);
                ValidateMatrixBottomRowValue(matrix.M42, 0.0, path + ".m42", diagnostics, options.MatrixBottomRowTolerance);
                ValidateMatrixBottomRowValue(matrix.M43, 0.0, path + ".m43", diagnostics, options.MatrixBottomRowTolerance);
                ValidateMatrixBottomRowValue(matrix.M44, 1.0, path + ".m44", diagnostics, options.MatrixBottomRowTolerance);
            }
        }

        private static void ValidateMatrixBottomRowValue(
            double value,
            double expected,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            double tolerance)
        {
            if (System.Math.Abs(value - expected) > tolerance)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.InvalidMatrixBottomRow,
                        path,
                        "Matrix bottom-row component is outside tolerance.",
                        value,
                        expected,
                        tolerance));
            }
        }

        private static void ValidateMatrixFrameConsistency(
            TrackFrameV1Dto? frame,
            Matrix4x4V1Dto? matrix,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            TrainPoseExportV1ValidationOptions options)
        {
            if (!options.ValidateMatrixFrameConsistency || frame == null || matrix == null)
            {
                return;
            }

            if (!IsFiniteFrame(frame) || !IsFiniteMatrix(matrix))
            {
                return;
            }

            double tolerance = options.MatrixFrameTolerance;

            ValidateMatrixFrameValue(matrix.M11, frame.Tangent.X, path + ".m11", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M12, frame.Normal.X, path + ".m12", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M13, frame.Binormal.X, path + ".m13", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M14, frame.Position.X, path + ".m14", diagnostics, tolerance);

            ValidateMatrixFrameValue(matrix.M21, frame.Tangent.Y, path + ".m21", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M22, frame.Normal.Y, path + ".m22", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M23, frame.Binormal.Y, path + ".m23", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M24, frame.Position.Y, path + ".m24", diagnostics, tolerance);

            ValidateMatrixFrameValue(matrix.M31, frame.Tangent.Z, path + ".m31", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M32, frame.Normal.Z, path + ".m32", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M33, frame.Binormal.Z, path + ".m33", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M34, frame.Position.Z, path + ".m34", diagnostics, tolerance);

            ValidateMatrixFrameValue(matrix.M41, 0.0, path + ".m41", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M42, 0.0, path + ".m42", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M43, 0.0, path + ".m43", diagnostics, tolerance);
            ValidateMatrixFrameValue(matrix.M44, 1.0, path + ".m44", diagnostics, tolerance);
        }

        private static void ValidateMatrixFrameValue(
            double value,
            double expected,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics,
            double tolerance)
        {
            if (System.Math.Abs(value - expected) > tolerance)
            {
                diagnostics.Add(
                    new TrainPoseExportV1ValidationDiagnostic(
                        TrainPoseExportV1ValidationCode.MatrixFrameMismatch,
                        path,
                        "Matrix component must match the associated track frame.",
                        value,
                        expected,
                        tolerance));
            }
        }

        private static bool ValidateFiniteVector(
            Vector3dV1Dto? vector,
            string path,
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics)
        {
            if (vector == null)
            {
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
            List<TrainPoseExportV1ValidationDiagnostic> diagnostics)
        {
            if (IsFinite(value))
            {
                return true;
            }

            diagnostics.Add(
                new TrainPoseExportV1ValidationDiagnostic(
                    TrainPoseExportV1ValidationCode.NonFiniteNumber,
                    path,
                    "Numeric value must be finite.",
                    value));
            return false;
        }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }

        private static bool IsFiniteFrame(TrackFrameV1Dto frame)
        {
            return IsFinite(frame.Distance) &&
                   IsFiniteVector(frame.Position) &&
                   IsFiniteVector(frame.Tangent) &&
                   IsFiniteVector(frame.Normal) &&
                   IsFiniteVector(frame.Binormal);
        }

        private static bool IsFiniteVector(Vector3dV1Dto? vector)
        {
            return vector != null &&
                   IsFinite(vector.X) &&
                   IsFinite(vector.Y) &&
                   IsFinite(vector.Z);
        }

        private static bool IsFiniteMatrix(Matrix4x4V1Dto matrix)
        {
            return IsFinite(matrix.M11) &&
                   IsFinite(matrix.M12) &&
                   IsFinite(matrix.M13) &&
                   IsFinite(matrix.M14) &&
                   IsFinite(matrix.M21) &&
                   IsFinite(matrix.M22) &&
                   IsFinite(matrix.M23) &&
                   IsFinite(matrix.M24) &&
                   IsFinite(matrix.M31) &&
                   IsFinite(matrix.M32) &&
                   IsFinite(matrix.M33) &&
                   IsFinite(matrix.M34) &&
                   IsFinite(matrix.M41) &&
                   IsFinite(matrix.M42) &&
                   IsFinite(matrix.M43) &&
                   IsFinite(matrix.M44);
        }

        private static double Length(Vector3dV1Dto vector)
        {
            return System.Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        }

        private static double Dot(Vector3dV1Dto a, Vector3dV1Dto b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        private static Vector3dV1Dto Cross(Vector3dV1Dto a, Vector3dV1Dto b)
        {
            return new Vector3dV1Dto
            {
                X = (a.Y * b.Z) - (a.Z * b.Y),
                Y = (a.Z * b.X) - (a.X * b.Z),
                Z = (a.X * b.Y) - (a.Y * b.X)
            };
        }
    }
}
