using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quantum.Tests;

public sealed class SnapshotCatalogTests
{
    [Fact]
    public void FindSnapshots_ReturnsValidDebugViewportSnapshots()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string repositoryRoot = Path.Combine(tempDirectory, "repo");
        string snapshotRoot = Path.Combine(repositoryRoot, "artifacts", "debug-viewport");
        string snapshotPath = Path.Combine(snapshotRoot, "sample.snapshot.json");
        string otherPath = Path.Combine(snapshotRoot, "not-a-snapshot.json");

        try
        {
            Directory.CreateDirectory(snapshotRoot);
            File.WriteAllText(Path.Combine(repositoryRoot, "QuantumCoasterWorks.sln"), string.Empty);
            File.WriteAllText(snapshotPath, CreateSnapshotJson());
            File.WriteAllText(otherPath, "{\"contract\":\"other\"}");

            IReadOnlyList<SnapshotSummary> snapshots =
                SnapshotCatalog.FindSnapshots(repositoryRoot, snapshotRoot);

            SnapshotSummary snapshot = Assert.Single(snapshots);
            Assert.Equal("artifacts/debug-viewport/sample.snapshot.json", snapshot.RepositoryRelativePath);
            Assert.Equal("sample.snapshot.json", snapshot.FileName);
            Assert.Equal(1, snapshot.Version);
            Assert.Equal("Preview catalog test", snapshot.SourceFixtureName);
            Assert.Equal(2, snapshot.CenterlinePointCount);
            Assert.Equal(2, snapshot.FrameCount);
            Assert.Equal(1, snapshot.LineCount);
            Assert.Equal(1, snapshot.BoxCount);
            Assert.Equal(1, snapshot.TrainPoseCarCount);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void TryResolveRepositoryFile_RejectsPathsOutsideRepository()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string repositoryRoot = Path.Combine(tempDirectory, "repo");
        string outsideDirectory = Path.Combine(tempDirectory, "outside");
        string outsidePath = Path.Combine(outsideDirectory, "sample.snapshot.json");

        try
        {
            Directory.CreateDirectory(repositoryRoot);
            Directory.CreateDirectory(outsideDirectory);
            File.WriteAllText(outsidePath, CreateSnapshotJson());

            bool resolved = PreviewViewerPaths.TryResolveRepositoryFile(
                repositoryRoot,
                outsidePath,
                out string _,
                out string error);

            Assert.False(resolved);
            Assert.Contains("inside the repository", error);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static string CreateSnapshotJson()
    {
        return """
            {
              "contract": "quantum.debug_viewport_snapshot",
              "version": 1,
              "metadata": {
                "units": "meters",
                "sourceFixtureName": "Preview catalog test",
                "sampleCount": 2
              },
              "centerlinePoints": [
                { "distance": 0.0, "position": { "x": 0.0, "y": 0.0, "z": 0.0 } },
                { "distance": 1.0, "position": { "x": 1.0, "y": 0.0, "z": 0.0 } }
              ],
              "frames": [
                {
                  "distance": 0.0,
                  "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
                  "tangent": { "x": 1.0, "y": 0.0, "z": 0.0 },
                  "normal": { "x": 0.0, "y": 1.0, "z": 0.0 },
                  "binormal": { "x": 0.0, "y": 0.0, "z": 1.0 }
                },
                {
                  "distance": 1.0,
                  "position": { "x": 1.0, "y": 0.0, "z": 0.0 },
                  "tangent": { "x": 1.0, "y": 0.0, "z": 0.0 },
                  "normal": { "x": 0.0, "y": 1.0, "z": 0.0 },
                  "binormal": { "x": 0.0, "y": 0.0, "z": 1.0 }
                }
              ],
              "lines": [
                {
                  "kind": "diagnostic.line",
                  "start": { "x": 0.0, "y": 0.0, "z": 0.0 },
                  "end": { "x": 0.0, "y": 1.0, "z": 0.0 }
                }
              ],
              "boxes": [
                {
                  "role": "train.body",
                  "label": "car-0",
                  "frame": {
                    "distance": 1.0,
                    "position": { "x": 1.0, "y": 0.0, "z": 0.0 },
                    "tangent": { "x": 1.0, "y": 0.0, "z": 0.0 },
                    "normal": { "x": 0.0, "y": 1.0, "z": 0.0 },
                    "binormal": { "x": 0.0, "y": 0.0, "z": 1.0 }
                  },
                  "size": { "length": 1.0, "width": 1.0, "height": 1.0 }
                }
              ],
              "trainPose": {
                "contract": "quantum.train_pose",
                "version": 1,
                "cars": [ {} ]
              }
            }
            """;
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.PreviewViewer.SnapshotCatalogTests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
