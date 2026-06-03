using System;

namespace Quantum.IO.MeshExport.V1
{
    /// <summary>
    /// Versioned renderer-neutral DTO for mesh topology handoff.
    /// </summary>
    /// <remarks>
    /// MeshExportV1 describes backend-owned mesh data only. It intentionally
    /// does not carry Blender, Unity, GLTF, shader, texture, camera, light, or
    /// renderer material objects.
    /// </remarks>
    public sealed class MeshExportV1Dto
    {
        public const string ContractName = "quantum.mesh_export";

        public const int ContractVersion = 1;

        public string Contract { get; set; } = ContractName;

        public int Version { get; set; } = ContractVersion;

        public MeshExportMeshV1Dto[] Meshes { get; set; } = Array.Empty<MeshExportMeshV1Dto>();
    }

    public sealed class MeshExportMeshV1Dto
    {
        public string Name { get; set; } = string.Empty;

        public MeshExportVector3dV1Dto[] Vertices { get; set; } = Array.Empty<MeshExportVector3dV1Dto>();

        public int[] TriangleIndices { get; set; } = Array.Empty<int>();

        public MeshExportVector3dV1Dto[]? Normals { get; set; }

        public string[] MaterialSlotLabels { get; set; } = Array.Empty<string>();
    }

    public sealed class MeshExportVector3dV1Dto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }
}
