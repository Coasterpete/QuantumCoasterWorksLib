using System;
using System.Collections.Generic;

namespace Quantum.IO.MeshExport.V1
{
    public enum MeshExportV1ValidationCode
    {
        InvalidContract = 0,
        InvalidVersion = 1,
        MissingCollection = 2,
        EmptyMeshCollection = 3,
        MissingObject = 4,
        MissingMeshName = 5,
        EmptyVertexCollection = 6,
        EmptyTriangleIndexCollection = 7,
        InvalidTriangleIndexCount = 8,
        InvalidTriangleIndex = 9,
        NonFiniteNumber = 10,
        NormalCountMismatch = 11,
        EmptyMaterialSlotLabel = 12
    }

    public sealed class MeshExportV1ValidationDiagnostic
    {
        public MeshExportV1ValidationDiagnostic(
            MeshExportV1ValidationCode code,
            string path,
            string message,
            double? value = null,
            double? expected = null)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Value = value;
            Expected = expected;
        }

        public MeshExportV1ValidationCode Code { get; }

        public string Path { get; }

        public string Message { get; }

        public double? Value { get; }

        public double? Expected { get; }
    }

    public static class MeshExportV1Validator
    {
        public static IReadOnlyList<MeshExportV1ValidationDiagnostic> Validate(MeshExportV1Dto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            var diagnostics = new List<MeshExportV1ValidationDiagnostic>();

            ValidateContract(dto, diagnostics);
            ValidateMeshes(dto.Meshes, diagnostics);

            return diagnostics.ToArray();
        }

        public static bool TryValidate(
            MeshExportV1Dto dto,
            out IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            diagnostics = Validate(dto);
            return diagnostics.Count == 0;
        }

        private static void ValidateContract(
            MeshExportV1Dto dto,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (!string.Equals(dto.Contract, MeshExportV1Dto.ContractName, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.InvalidContract,
                        "contract",
                        "Contract name must match MeshExportV1."));
            }

            if (dto.Version != MeshExportV1Dto.ContractVersion)
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.InvalidVersion,
                        "version",
                        "Version must match MeshExportV1.",
                        dto.Version,
                        MeshExportV1Dto.ContractVersion));
            }
        }

        private static void ValidateMeshes(
            MeshExportMeshV1Dto[]? meshes,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (meshes == null)
            {
                AddMissingCollection("meshes", diagnostics);
                return;
            }

            if (meshes.Length == 0)
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.EmptyMeshCollection,
                        "meshes",
                        "MeshExportV1 requires at least one mesh."));
                return;
            }

            for (int i = 0; i < meshes.Length; i++)
            {
                string meshPath = "meshes[" + i + "]";
                MeshExportMeshV1Dto? mesh = meshes[i];
                if (mesh == null)
                {
                    AddMissingObject(meshPath, diagnostics);
                    continue;
                }

                ValidateMesh(mesh, meshPath, diagnostics);
            }
        }

        private static void ValidateMesh(
            MeshExportMeshV1Dto mesh,
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(mesh.Name))
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.MissingMeshName,
                        path + ".name",
                        "Mesh name is required."));
            }

            int vertexCount = ValidateVertices(mesh.Vertices, path + ".vertices", diagnostics);
            ValidateTriangleIndices(mesh.TriangleIndices, vertexCount, path + ".triangleIndices", diagnostics);
            ValidateNormals(mesh.Normals, vertexCount, path + ".normals", diagnostics);
            ValidateMaterialSlotLabels(mesh.MaterialSlotLabels, path + ".materialSlotLabels", diagnostics);
        }

        private static int ValidateVertices(
            MeshExportVector3dV1Dto[]? vertices,
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (vertices == null)
            {
                AddMissingCollection(path, diagnostics);
                return 0;
            }

            if (vertices.Length == 0)
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.EmptyVertexCollection,
                        path,
                        "Mesh vertices must not be empty."));
                return 0;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                ValidateVector(vertices[i], path + "[" + i + "]", diagnostics);
            }

            return vertices.Length;
        }

        private static void ValidateTriangleIndices(
            int[]? triangleIndices,
            int vertexCount,
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (triangleIndices == null)
            {
                AddMissingCollection(path, diagnostics);
                return;
            }

            if (triangleIndices.Length == 0)
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.EmptyTriangleIndexCollection,
                        path,
                        "Triangle indices must not be empty."));
                return;
            }

            if (triangleIndices.Length % 3 != 0)
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.InvalidTriangleIndexCount,
                        path,
                        "Triangle index count must be a multiple of 3.",
                        triangleIndices.Length));
            }

            if (vertexCount <= 0)
            {
                return;
            }

            for (int i = 0; i < triangleIndices.Length; i++)
            {
                int index = triangleIndices[i];
                if (index < 0 || index >= vertexCount)
                {
                    diagnostics.Add(
                        new MeshExportV1ValidationDiagnostic(
                            MeshExportV1ValidationCode.InvalidTriangleIndex,
                            path + "[" + i + "]",
                            "Triangle index must reference an existing vertex.",
                            index,
                            vertexCount - 1));
                }
            }
        }

        private static void ValidateNormals(
            MeshExportVector3dV1Dto[]? normals,
            int vertexCount,
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (normals == null)
            {
                return;
            }

            if (vertexCount > 0 && normals.Length != vertexCount)
            {
                diagnostics.Add(
                    new MeshExportV1ValidationDiagnostic(
                        MeshExportV1ValidationCode.NormalCountMismatch,
                        path,
                        "Normals are optional, but when present they must match vertex count.",
                        normals.Length,
                        vertexCount));
            }

            for (int i = 0; i < normals.Length; i++)
            {
                ValidateVector(normals[i], path + "[" + i + "]", diagnostics);
            }
        }

        private static void ValidateMaterialSlotLabels(
            string[]? materialSlotLabels,
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (materialSlotLabels == null)
            {
                AddMissingCollection(path, diagnostics);
                return;
            }

            for (int i = 0; i < materialSlotLabels.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(materialSlotLabels[i]))
                {
                    diagnostics.Add(
                        new MeshExportV1ValidationDiagnostic(
                            MeshExportV1ValidationCode.EmptyMaterialSlotLabel,
                            path + "[" + i + "]",
                            "Material slot labels must be non-empty neutral labels."));
                }
            }
        }

        private static void ValidateVector(
            MeshExportVector3dV1Dto? vector,
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (vector == null)
            {
                AddMissingObject(path, diagnostics);
                return;
            }

            ValidateFinite(vector.X, path + ".x", diagnostics);
            ValidateFinite(vector.Y, path + ".y", diagnostics);
            ValidateFinite(vector.Z, path + ".z", diagnostics);
        }

        private static void ValidateFinite(
            double value,
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            if (IsFinite(value))
            {
                return;
            }

            diagnostics.Add(
                new MeshExportV1ValidationDiagnostic(
                    MeshExportV1ValidationCode.NonFiniteNumber,
                    path,
                    "Numeric value must be finite.",
                    value));
        }

        private static void AddMissingCollection(
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            diagnostics.Add(
                new MeshExportV1ValidationDiagnostic(
                    MeshExportV1ValidationCode.MissingCollection,
                    path,
                    "Required collection is missing."));
        }

        private static void AddMissingObject(
            string path,
            List<MeshExportV1ValidationDiagnostic> diagnostics)
        {
            diagnostics.Add(
                new MeshExportV1ValidationDiagnostic(
                    MeshExportV1ValidationCode.MissingObject,
                    path,
                    "Required object is missing."));
        }

        private static bool IsFinite(double value)
        {
            return !(double.IsNaN(value) || double.IsInfinity(value));
        }
    }
}
