using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Quantum.IO.TrainPose.V1;
using Quantum.Math;
using Quantum.Track;

namespace Quantum.Debug
{
    public static class TrainPoseExportV1Command
    {
        internal const string DefaultRelativeOutputPath = "artifacts/train-pose/TrainPoseExportV1.sample.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            TrainPoseResult source = BuildDeterministicPose();
            TrainPoseExportV1Dto dto = TrainPoseExportV1Mapper.Export(source);

            bool valid = TrainPoseExportV1Validator.TryValidate(
                dto,
                out IReadOnlyList<TrainPoseExportV1ValidationDiagnostic> diagnostics);

            if (!valid)
            {
                Console.WriteLine("TrainPoseExportV1 validation failed:");
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    TrainPoseExportV1ValidationDiagnostic diagnostic = diagnostics[i];
                    Console.WriteLine($"  [{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}");
                }

                return 1;
            }

            string resolvedOutputPath = ResolveOutputPath(outputPath);
            string? parentDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = TrainPoseExportV1Json.Serialize(dto, indented: true);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);
            Console.WriteLine($"Wrote TrainPoseExportV1 sample to '{resolvedOutputPath}'.");
            return 0;
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static TrainPoseResult BuildDeterministicPose()
        {
            var definition = new TrainConsistDefinition(
                carCount: 1,
                carSpacing: 3.25,
                carGeometry: new TrainCarGeometry(length: 4.5, width: 1.8, height: 2.1),
                bogieLayout: new TrainBogieLayout(bogieSpacing: 2.75),
                wheelLayout: new TrainWheelLayout(
                    wheelCountPerBogie: 2,
                    wheelRadius: 0.45,
                    wheelWidth: 0.25,
                    axleSpacing: 1.1));

            var cars = new[]
            {
                CreateCar(carIndex: 0, distance: 20.0)
            };

            return new TrainPoseResult(leadDistance: 21.5, definition: definition, cars: cars);
        }

        private static ArticulatedTrainCarWithWheelsTransform CreateCar(int carIndex, double distance)
        {
            TrainBogieWithWheelsTransform frontBogie = CreateBogie(
                carIndex: carIndex,
                bogieIndex: 0,
                distance: distance + 1.25,
                seed: 100.0 + carIndex);
            TrainBogieWithWheelsTransform rearBogie = CreateBogie(
                carIndex: carIndex,
                bogieIndex: 1,
                distance: distance - 1.25,
                seed: 200.0 + carIndex);

            var originalBody = new TrainCarTransform(
                carIndex: carIndex,
                distance: distance,
                frame: CreateFrame(distance, 1.0 + carIndex, 2.0 + carIndex, 3.0 + carIndex),
                matrix: CreateFloatMatrix(10f + carIndex));

            var articulatedBody = new ArticulatedTrainCarTransform(
                originalBody: originalBody,
                frontBogie: frontBogie.Bogie,
                rearBogie: rearBogie.Bogie,
                articulatedFrame: CreateFrame(distance + 0.5, 4.0 + carIndex, 5.0 + carIndex, 6.0 + carIndex),
                articulatedMatrix: CreateDoubleMatrix(500.0 + carIndex),
                centerDistance: distance + 0.5);

            return new ArticulatedTrainCarWithWheelsTransform(
                body: articulatedBody,
                frontBogie: frontBogie,
                rearBogie: rearBogie);
        }

        private static TrainBogieWithWheelsTransform CreateBogie(int carIndex, int bogieIndex, double distance, double seed)
        {
            TrackFrame frame = CreateFrame(distance, seed + 1.0, seed + 2.0, seed + 3.0);

            var bogie = new BogieTransform(
                carIndex: carIndex,
                bogieIndex: bogieIndex,
                distance: distance,
                frame: frame,
                matrix: CreateDoubleMatrix(seed));

            var wheels = new[]
            {
                CreateWheel(carIndex, bogieIndex, wheelIndex: 0, -0.35, 0.0, 0.8, frame, CreateDoubleMatrix(seed + 0.1)),
                CreateWheel(carIndex, bogieIndex, wheelIndex: 1, 0.35, 0.0, 0.8, frame, CreateDoubleMatrix(seed + 0.2))
            };

            return new TrainBogieWithWheelsTransform(bogie, wheels);
        }

        private static WheelTransform CreateWheel(
            int carIndex,
            int bogieIndex,
            int wheelIndex,
            double localOffsetX,
            double localOffsetY,
            double localOffsetZ,
            TrackFrame frame,
            Matrix4x4d matrix)
        {
            return new WheelTransform(
                carIndex: carIndex,
                bogieIndex: bogieIndex,
                wheelIndex: wheelIndex,
                localOffsetX: localOffsetX,
                localOffsetY: localOffsetY,
                localOffsetZ: localOffsetZ,
                frame: frame,
                matrix: matrix);
        }

        private static TrackFrame CreateFrame(double distance, double x, double y, double z)
        {
            return new TrackFrame(
                distance: distance,
                position: new Vector3d(x, y, z),
                tangent: new Vector3d(1.0, 0.0, 0.0),
                normal: new Vector3d(0.0, 1.0, 0.0),
                binormal: new Vector3d(0.0, 0.0, 1.0));
        }

        private static Matrix4x4 CreateFloatMatrix(float seed)
        {
            return new Matrix4x4(
                seed + 1f, seed + 2f, seed + 3f, seed + 4f,
                seed + 5f, seed + 6f, seed + 7f, seed + 8f,
                seed + 9f, seed + 10f, seed + 11f, seed + 12f,
                seed + 13f, seed + 14f, seed + 15f, seed + 16f);
        }

        private static Matrix4x4d CreateDoubleMatrix(double seed)
        {
            return new Matrix4x4d(
                seed + 1.0, seed + 2.0, seed + 3.0, seed + 4.0,
                seed + 5.0, seed + 6.0, seed + 7.0, seed + 8.0,
                seed + 9.0, seed + 10.0, seed + 11.0, seed + 12.0,
                seed + 13.0, seed + 14.0, seed + 15.0, seed + 16.0);
        }
    }
}
