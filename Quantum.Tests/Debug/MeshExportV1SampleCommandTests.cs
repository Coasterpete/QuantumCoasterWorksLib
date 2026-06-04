using System.IO;
using Quantum.Debug;
using Quantum.IO.MeshExport.V1;

namespace Quantum.Tests;

public sealed class MeshExportV1SampleCommandTests
{
    [Fact]
    public void Run_WithExplicitOutputPath_WritesValidMeshExportV1Json()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string outputPath = Path.Combine(tempDirectory, "MeshExportV1.sample.json");

        try
        {
            int exitCode = MeshExportV1SampleCommand.Run(outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            string json = File.ReadAllText(outputPath);
            MeshExportV1Dto dto = MeshExportV1Json.Deserialize(json);
            bool isValid = MeshExportV1Validator.TryValidate(
                dto,
                out IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics);

            Assert.True(isValid);
            Assert.Empty(diagnostics);
            Assert.Equal(MeshExportV1Dto.ContractName, dto.Contract);
            Assert.Equal(MeshExportV1Dto.ContractVersion, dto.Version);

            MeshExportMeshV1Dto mesh = Assert.Single(dto.Meshes);
            Assert.Equal("debug.quad", mesh.Name);
            Assert.Equal(MeshExportV1SampleCommand.VertexCount, mesh.Vertices.Length);
            Assert.Equal(MeshExportV1SampleCommand.TriangleIndexCount, mesh.TriangleIndices.Length);
            Assert.NotNull(mesh.Normals);
            Assert.Equal(MeshExportV1SampleCommand.VertexCount, mesh.Normals!.Length);
            Assert.Equal(new[] { "debug.surface" }, mesh.MaterialSlotLabels);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Run_WithExplicitOutputPath_IsDeterministicAcrossTwoRuns()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string firstOutputPath = Path.Combine(tempDirectory, "MeshExportV1.first.json");
        string secondOutputPath = Path.Combine(tempDirectory, "MeshExportV1.second.json");

        try
        {
            int firstExitCode = MeshExportV1SampleCommand.Run(firstOutputPath);
            int secondExitCode = MeshExportV1SampleCommand.Run(secondOutputPath);

            Assert.Equal(0, firstExitCode);
            Assert.Equal(0, secondExitCode);

            string firstJson = File.ReadAllText(firstOutputPath);
            string secondJson = File.ReadAllText(secondOutputPath);

            Assert.Equal(firstJson, secondJson);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void Validation_CatchesMalformedSampleTopology()
    {
        MeshExportV1Dto dto = MeshExportV1SampleCommand.BuildSample();
        dto.Meshes[0].TriangleIndices = new[] { 0, 1, 99 };

        IReadOnlyList<MeshExportV1ValidationDiagnostic> diagnostics = MeshExportV1Validator.Validate(dto);

        Assert.Contains(
            diagnostics,
            d => d.Code == MeshExportV1ValidationCode.InvalidTriangleIndex &&
                 d.Path == "meshes[0].triangleIndices[2]");
    }

    [Fact]
    public void BuildSample_UsesExpectedMinimalQuadShape()
    {
        MeshExportV1Dto dto = MeshExportV1SampleCommand.BuildSample();

        MeshExportMeshV1Dto mesh = Assert.Single(dto.Meshes);
        Assert.Equal(new[] { 0, 2, 1, 0, 3, 2 }, mesh.TriangleIndices);
        Assert.Equal(-0.5, mesh.Vertices[0].X);
        Assert.Equal(0.0, mesh.Vertices[0].Y);
        Assert.Equal(-0.5, mesh.Vertices[0].Z);
        Assert.NotNull(mesh.Normals);
        Assert.All(mesh.Normals!, normal =>
        {
            Assert.Equal(0.0, normal.X);
            Assert.Equal(1.0, normal.Y);
            Assert.Equal(0.0, normal.Z);
        });
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "QuantumCoasterWorks.MeshExportV1SampleCommandTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
