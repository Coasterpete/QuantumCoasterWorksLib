using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Quantum.Tests;

public sealed class StyleManifestCatalogTests
{
    [Fact]
    public void LoadManifest_AddsAssetUrlsForConfiguredTrainAssets()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string repositoryRoot = Path.Combine(tempDirectory, "repo");
        string viewerRoot = Path.Combine(repositoryRoot, "Quantum.PreviewViewer");
        string assetRoot = viewerRoot;
        string manifestPath = Path.Combine(viewerRoot, "preview-styles.json");
        string assetPath = Path.Combine(viewerRoot, "assets", "trains", "custom-car.glb");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
            File.WriteAllText(Path.Combine(repositoryRoot, "QuantumCoasterWorks.sln"), string.Empty);
            File.WriteAllText(assetPath, "self-authored-placeholder");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "version": 1,
                  "defaultTrainStyle": "custom",
                  "trainStyles": [
                    {
                      "id": "custom",
                      "name": "Custom",
                      "roles": {
                        "train.body": {
                          "asset": "assets/trains/custom-car.glb",
                          "fitToBox": true
                        }
                      }
                    }
                  ],
                  "trackStyles": []
                }
                """);

            JsonObject manifest = PreviewStyleManifestCatalog.LoadManifest(
                repositoryRoot,
                manifestPath,
                assetRoot,
                "/style-assets");

            JsonObject role = GetTrainBodyRole(manifest);
            Assert.Equal("/style-assets/assets/trains/custom-car.glb", role["assetUrl"]!.GetValue<string>());
            Assert.Equal("Quantum.PreviewViewer/preview-styles.json", manifest["manifestPath"]!.GetValue<string>());
            Assert.Equal("Quantum.PreviewViewer", manifest["assetRoot"]!.GetValue<string>());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void LoadManifest_MissingManifestReturnsDebugBoxesStyle()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string repositoryRoot = Path.Combine(tempDirectory, "repo");
        string viewerRoot = Path.Combine(repositoryRoot, "Quantum.PreviewViewer");

        try
        {
            Directory.CreateDirectory(viewerRoot);
            File.WriteAllText(Path.Combine(repositoryRoot, "QuantumCoasterWorks.sln"), string.Empty);

            JsonObject manifest = PreviewStyleManifestCatalog.LoadManifest(
                repositoryRoot,
                Path.Combine(viewerRoot, "preview-styles.json"),
                viewerRoot,
                "/style-assets");

            Assert.Equal("debug-boxes", manifest["defaultTrainStyle"]!.GetValue<string>());
            JsonArray trainStyles = Assert.IsType<JsonArray>(manifest["trainStyles"]);
            JsonObject debugStyle = Assert.IsType<JsonObject>(trainStyles[0]);
            Assert.Equal("debug-boxes", debugStyle["id"]!.GetValue<string>());
            JsonArray diagnostics = Assert.IsType<JsonArray>(manifest["diagnostics"]);
            Assert.NotEmpty(diagnostics);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void LoadManifest_AddsAssetUrlsForTrainRoleVariantAssets()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string repositoryRoot = Path.Combine(tempDirectory, "repo");
        string viewerRoot = Path.Combine(repositoryRoot, "Quantum.PreviewViewer");
        string manifestPath = Path.Combine(viewerRoot, "preview-styles.json");

        try
        {
            Directory.CreateDirectory(Path.Combine(viewerRoot, "assets", "trains"));
            File.WriteAllText(Path.Combine(repositoryRoot, "QuantumCoasterWorks.sln"), string.Empty);
            File.WriteAllText(Path.Combine(viewerRoot, "assets", "trains", "lead.glb"), "lead");
            File.WriteAllText(Path.Combine(viewerRoot, "assets", "trains", "middle.glb"), "middle");
            File.WriteAllText(Path.Combine(viewerRoot, "assets", "trains", "rear.glb"), "rear");
            File.WriteAllText(Path.Combine(viewerRoot, "assets", "trains", "body.glb"), "body");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "version": 1,
                  "defaultTrainStyle": "variants",
                  "trainStyles": [
                    {
                      "id": "variants",
                      "name": "Variants",
                      "roles": {
                        "train.lead": { "asset": "assets/trains/lead.glb" },
                        "train.middle": { "asset": "assets/trains/middle.glb" },
                        "train.rear": { "asset": "assets/trains/rear.glb" },
                        "train.body": { "asset": "assets/trains/body.glb" }
                      }
                    }
                  ],
                  "trackStyles": []
                }
                """);

            JsonObject manifest = PreviewStyleManifestCatalog.LoadManifest(
                repositoryRoot,
                manifestPath,
                viewerRoot,
                "/style-assets");

            Assert.Equal("/style-assets/assets/trains/lead.glb", GetRole(manifest, "train.lead")["assetUrl"]!.GetValue<string>());
            Assert.Equal("/style-assets/assets/trains/middle.glb", GetRole(manifest, "train.middle")["assetUrl"]!.GetValue<string>());
            Assert.Equal("/style-assets/assets/trains/rear.glb", GetRole(manifest, "train.rear")["assetUrl"]!.GetValue<string>());
            Assert.Equal("/style-assets/assets/trains/body.glb", GetRole(manifest, "train.body")["assetUrl"]!.GetValue<string>());
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void ResolveConfiguredStyleRole_UsesTrainPositionVariantsThenBodyFallback()
    {
        string[] variantRoles =
        {
            PreviewTrainStyleRoles.TrainLeadRole,
            PreviewTrainStyleRoles.TrainMiddleRole,
            PreviewTrainStyleRoles.TrainRearRole,
            PreviewTrainStyleRoles.TrainBodyRole
        };

        Assert.Equal(
            PreviewTrainStyleRoles.TrainLeadRole,
            PreviewTrainStyleRoles.ResolveConfiguredStyleRole(
                PreviewTrainStyleRoles.TrainBodyRole,
                carIndex: 0,
                carCount: 4,
                variantRoles));
        Assert.Equal(
            PreviewTrainStyleRoles.TrainMiddleRole,
            PreviewTrainStyleRoles.ResolveConfiguredStyleRole(
                PreviewTrainStyleRoles.TrainBodyRole,
                carIndex: 1,
                carCount: 4,
                variantRoles));
        Assert.Equal(
            PreviewTrainStyleRoles.TrainRearRole,
            PreviewTrainStyleRoles.ResolveConfiguredStyleRole(
                PreviewTrainStyleRoles.TrainBodyRole,
                carIndex: 3,
                carCount: 4,
                variantRoles));
        Assert.Equal(
            PreviewTrainStyleRoles.TrainLeadRole,
            PreviewTrainStyleRoles.ResolveConfiguredStyleRole(
                PreviewTrainStyleRoles.TrainBodyRole,
                carIndex: 0,
                carCount: 1,
                variantRoles));

        string[] fallbackRoles = { PreviewTrainStyleRoles.TrainBodyRole };
        Assert.Equal(
            PreviewTrainStyleRoles.TrainBodyRole,
            PreviewTrainStyleRoles.ResolveConfiguredStyleRole(
                PreviewTrainStyleRoles.TrainBodyBankingProfileRole,
                carIndex: 1,
                carCount: 3,
                fallbackRoles));
    }

    [Fact]
    public void TryResolveStyleAssetFile_AllowsGltfSidecarBinFiles()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string assetRoot = Path.Combine(tempDirectory, "repo", "Quantum.PreviewViewer");
        string sidecarPath = Path.Combine(assetRoot, "assets", "train.bin");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
            File.WriteAllBytes(sidecarPath, Array.Empty<byte>());

            bool resolved = PreviewStyleAssetFiles.TryResolveStyleAssetFile(
                assetRoot,
                "assets/train.bin",
                out string resolvedPath,
                out string contentType,
                out string error);

            Assert.True(resolved, error);
            Assert.Equal(sidecarPath, resolvedPath);
            Assert.Equal("application/octet-stream", contentType);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void LoadManifest_DoesNotAddAssetUrlForAssetsOutsideAssetRoot()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string repositoryRoot = Path.Combine(tempDirectory, "repo");
        string viewerRoot = Path.Combine(repositoryRoot, "Quantum.PreviewViewer");
        string manifestPath = Path.Combine(viewerRoot, "preview-styles.json");

        try
        {
            Directory.CreateDirectory(viewerRoot);
            File.WriteAllText(Path.Combine(repositoryRoot, "QuantumCoasterWorks.sln"), string.Empty);
            File.WriteAllText(
                manifestPath,
                """
                {
                  "version": 1,
                  "defaultTrainStyle": "unsafe",
                  "trainStyles": [
                    {
                      "id": "unsafe",
                      "name": "Unsafe",
                      "roles": {
                        "train.body": {
                          "asset": "../outside.glb"
                        }
                      }
                    }
                  ],
                  "trackStyles": []
                }
                """);

            JsonObject manifest = PreviewStyleManifestCatalog.LoadManifest(
                repositoryRoot,
                manifestPath,
                viewerRoot,
                "/style-assets");

            JsonObject role = GetTrainBodyRole(manifest);
            Assert.Null(role["assetUrl"]);
            JsonArray diagnostics = Assert.IsType<JsonArray>(manifest["diagnostics"]);
            Assert.Contains(
                diagnostics,
                diagnostic => ((JsonObject)diagnostic!)["message"]!.GetValue<string>()
                    .Contains("inside the configured preview asset root", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    [Fact]
    public void TryResolveStyleAssetFile_RejectsPathsOutsideAssetRoot()
    {
        string tempDirectory = CreateTempDirectoryPath();
        string assetRoot = Path.Combine(tempDirectory, "repo", "Quantum.PreviewViewer");

        try
        {
            Directory.CreateDirectory(assetRoot);

            bool resolved = PreviewStyleAssetFiles.TryResolveStyleAssetFile(
                assetRoot,
                "../outside.glb",
                out string _,
                out string _,
                out string error);

            Assert.False(resolved);
            Assert.Contains("inside the configured preview asset root", error);
        }
        finally
        {
            DeleteDirectoryIfPresent(tempDirectory);
        }
    }

    private static JsonObject GetTrainBodyRole(JsonObject manifest)
    {
        return GetRole(manifest, "train.body");
    }

    private static JsonObject GetRole(JsonObject manifest, string roleName)
    {
        JsonArray trainStyles = Assert.IsType<JsonArray>(manifest["trainStyles"]);
        JsonObject style = Assert.IsType<JsonObject>(trainStyles[0]);
        JsonObject roles = Assert.IsType<JsonObject>(style["roles"]);
        return Assert.IsType<JsonObject>(roles[roleName]);
    }

    private static string CreateTempDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.PreviewViewer.StyleManifestCatalogTests",
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
