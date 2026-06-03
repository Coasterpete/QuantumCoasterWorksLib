using System;
using System.Collections.Generic;
using System.Text.Json;
using Quantum.IO.MeshExport.V1;

namespace Quantum.Tests;

public sealed class MeshExportV1Tests
{
    [Fact]
    public void Dto_DefaultsSetContractVersionAndEmptyMeshes()
    {
        var dto = new MeshExportV1Dto();

        Assert.Equal(MeshExportV1Dto.ContractName, dto.Contract);
        Assert.Equal(MeshExportV1Dto.ContractVersion, dto.Version);
        Assert.Empty(dto.Meshes);
    }

    [Fact]
    public void SerializeDeserialize_RoundtripPreservesRepresentativeValues()
    {
        MeshExportV1Dto expected = CreateValidExport();

        string json = MeshExportV1Json.Serialize(expected);
        MeshExportV1Dto actual = MeshExportV1Json.Deserialize(json);

        Assert.Contains("\"contract\":", json);
        Assert.Contains("\"meshes\":", json);
        Assert.Contains("\"triangleIndices\":", json);
        Assert.Contains("\"materialSlotLabels\":", json);
        Assert.DoesNotContain("\"TriangleIndices\":", json);

        Assert.Equal(expected.Contract, actual.Contract);
        Assert.Equal(expected.Version, actual.Version);

        MeshExportMeshV1Dto mesh = Assert.Single(actual.Meshes);
        Assert.Equal("track.preview.triangle", mesh.Name);
        Assert.Equal(3, mesh.Vertices.Length);
        Assert.Equal(2.0, mesh.Vertices[2].Y);
        Assert.Equal(new[] { 0, 1, 2 }, mesh.TriangleIndices);
        Assert.NotNull(mesh.Normals);
        Assert.Equal(1.0, mesh.Normals![0].Z);
        Assert.Equal(new[] { "track.surface" }, mesh.MaterialSlotLabels);
    }

    [Fact]
    public void Deserialize_RejectsWrongContract()
    {
        string json = CreateMinimalJson(contract: "wrong.contract", version: MeshExportV1Dto.ContractVersion);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => MeshExportV1Json.Deserialize(json));

        Assert.Contains("contract", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsWrongVersion()
    {
        string json = CreateMinimalJson(contract: MeshExportV1Dto.ContractName, version: 999);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => MeshExportV1Json.Deserialize(json));

        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        const string malformedJson = @"{""contract"":""quantum.mesh_export"",""version"":1,";

        JsonException ex = Assert.Throws<JsonException>(() => MeshExportV1Json.Deserialize(malformedJson));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_ValidExport_Passes()
    {
        MeshExportV1Dto dto = CreateValidExport();

        bool isValid = MeshExportV1Validator.TryValidate(
            dto,
            out IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics);

        Assert.True(isValid);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validation_RejectsInvalidContractAndVersion()
    {
        MeshExportV1Dto dto = CreateValidExport();
        dto.Contract = "wrong.contract";
        dto.Version = 999;

        IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics = MeshExportV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.InvalidContract &&
                 d.Path == "contract");
        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.InvalidVersion &&
                 d.Path == "version");
    }

    [Fact]
    public void Validation_RejectsEmptyMeshCollection()
    {
        var dto = new MeshExportV1Dto();

        IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics = MeshExportV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.EmptyMeshCollection &&
                 d.Path == "meshes");
    }

    [Fact]
    public void Validation_RejectsEmptyMeshTopology()
    {
        var dto = new MeshExportV1Dto
        {
            Meshes = new[]
            {
                new MeshExportMeshV1Dto
                {
                    Name = "empty.mesh"
                }
            }
        };

        IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics = MeshExportV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.EmptyVertexCollection &&
                 d.Path == "meshes[0].vertices");
        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.EmptyTriangleIndexCollection &&
                 d.Path == "meshes[0].triangleIndices");
    }

    [Fact]
    public void Validation_RejectsInvalidTriangleIndex()
    {
        MeshExportV1Dto dto = CreateValidExport();
        dto.Meshes[0].TriangleIndices = new[] { 0, 1, 3 };

        IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics = MeshExportV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.InvalidTriangleIndex &&
                 d.Path == "meshes[0].triangleIndices[2]");
    }

    [Fact]
    public void Validation_RejectsTriangleIndexCountThatIsNotTriangles()
    {
        MeshExportV1Dto dto = CreateValidExport();
        dto.Meshes[0].TriangleIndices = new[] { 0, 1 };

        IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics = MeshExportV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.InvalidTriangleIndexCount &&
                 d.Path == "meshes[0].triangleIndices");
    }

    [Fact]
    public void Validation_AllowsMissingNormalsAndRejectsMismatchedNormals()
    {
        MeshExportV1Dto dto = CreateValidExport();
        dto.Meshes[0].Normals = null;

        Assert.True(MeshExportV1Validator.TryValidate(dto, out _));

        dto.Meshes[0].Normals = new[] { UnitZ() };

        IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics = MeshExportV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.NormalCountMismatch &&
                 d.Path == "meshes[0].normals");
    }

    [Fact]
    public void Serialization_IndentedOutputIsDeterministic()
    {
        MeshExportV1Dto dto = CreateValidExport();

        string first = MeshExportV1Json.Serialize(dto, indented: true);
        string second = MeshExportV1Json.Serialize(dto, indented: true);
        string roundtrip = MeshExportV1Json.Serialize(MeshExportV1Json.Deserialize(first), indented: true);

        Assert.Equal(first, second);
        Assert.Equal(first, roundtrip);
    }

    private static MeshExportV1Dto CreateValidExport()
    {
        return new MeshExportV1Dto
        {
            Meshes = new[]
            {
                new MeshExportMeshV1Dto
                {
                    Name = "track.preview.triangle",
                    Vertices = new[]
                    {
                        new MeshExportVector3dV1Dto { X = 0.0, Y = 0.0, Z = 0.0 },
                        new MeshExportVector3dV1Dto { X = 1.0, Y = 0.0, Z = 0.0 },
                        new MeshExportVector3dV1Dto { X = 0.0, Y = 2.0, Z = 0.0 }
                    },
                    TriangleIndices = new[] { 0, 1, 2 },
                    Normals = new[]
                    {
                        UnitZ(),
                        UnitZ(),
                        UnitZ()
                    },
                    MaterialSlotLabels = new[] { "track.surface" }
                }
            }
        };
    }

    private static MeshExportVector3dV1Dto UnitZ()
    {
        return new MeshExportVector3dV1Dto { X = 0.0, Y = 0.0, Z = 1.0 };
    }

    private static string CreateMinimalJson(string contract, int version)
    {
        return $@"{{
  ""contract"": ""{contract}"",
  ""version"": {version},
  ""meshes"": []
}}";
    }
}
