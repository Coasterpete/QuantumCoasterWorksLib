using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Quantum.IO.MeshExport.V1;

namespace Quantum.Debug
{
    public static class MeshExportV1SampleCommand
    {
        public const string CommandName = "mesh-export-v1-sample";
        public const int VertexCount = 4;
        public const int TriangleIndexCount = 6;

        internal const string DefaultRelativeOutputPath = "artifacts/mesh-export/MeshExportV1.sample.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run(string? outputPath = null)
        {
            MeshExportV1Dto dto = BuildSample();
            bool valid = MeshExportV1Validator.TryValidate(
                dto,
                out IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics);

            if (!valid)
            {
                Console.WriteLine("MeshExportV1 validation failed:");
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    MeshExportV1ValidationDiagnostic diagnostic = diagnostics[i];
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

            string json = MeshExportV1Json.Serialize(dto, indented: true);
            File.WriteAllText(resolvedOutputPath, json, Utf8NoBom);
            Console.WriteLine($"Wrote MeshExportV1 sample to '{resolvedOutputPath}'.");
            return 0;
        }

        public static MeshExportV1Dto BuildSample()
        {
            return new MeshExportV1Dto
            {
                Meshes = new[]
                {
                    new MeshExportMeshV1Dto
                    {
                        Name = "debug.quad",
                        Vertices = new[]
                        {
                            Vector(-0.5, 0.0, -0.5),
                            Vector(0.5, 0.0, -0.5),
                            Vector(0.5, 0.0, 0.5),
                            Vector(-0.5, 0.0, 0.5)
                        },
                        TriangleIndices = new[] { 0, 2, 1, 0, 3, 2 },
                        Normals = new[]
                        {
                            UnitY(),
                            UnitY(),
                            UnitY(),
                            UnitY()
                        },
                        MaterialSlotLabels = new[] { "debug.surface" }
                    }
                }
            };
        }

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultRelativeOutputPath));
            }

            return Path.GetFullPath(outputPath);
        }

        private static MeshExportVector3dV1Dto Vector(double x, double y, double z)
        {
            return new MeshExportVector3dV1Dto { X = x, Y = y, Z = z };
        }

        private static MeshExportVector3dV1Dto UnitY()
        {
            return Vector(0.0, 1.0, 0.0);
        }
    }
}
