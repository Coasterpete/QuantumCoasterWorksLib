using Quantum.Editor.Avalonia.Services.Documents;

namespace Quantum.Tests;

public sealed class TrackDocumentFileServiceTests
{
    [Fact]
    public void SaveAndOpen_PreservesPackageAndCleanState()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "showcase.qcwtrack.json");
        var fileService = new TrackDocumentFileService();

        try
        {
            TrackEditorDocument source = TrackEditorDocument.Create(
                TrackPackageFactory.CreateShowcasePackage(),
                "Untitled");
            source.MarkDirty();

            fileService.Save(source, path);
            TrackEditorDocument reopened = fileService.Open(path);

            Assert.True(File.Exists(path));
            Assert.False(source.IsDirty);
            Assert.False(reopened.IsDirty);
            Assert.Equal(Path.GetFullPath(path), reopened.FilePath);
            Assert.Equal("showcase.qcwtrack", reopened.DisplayName);
            Assert.Equal(source.CapturePackageJson(), reopened.CapturePackageJson());
            Assert.Equal(195.0, reopened.Compilation!.TotalLength, 9);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Open_InvalidPackage_ReportsValidationDiagnostics()
    {
        string tempDirectory = CreateTempDirectory();
        string path = Path.Combine(tempDirectory, "invalid.json");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                path,
                "{\"contract\":\"quantum.track_layout_package\",\"version\":2,\"metadata\":{},\"startPose\":{},\"sections\":[]}");

            TrackEditorDocumentException exception = Assert.Throws<TrackEditorDocumentException>(
                () => new TrackDocumentFileService().Open(path));

            Assert.NotEmpty(exception.Diagnostics);
            Assert.Contains("section", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static string CreateTempDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.EditorTests",
            Guid.NewGuid().ToString("N"));
    }
}
