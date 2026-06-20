using System.Text.Json;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string repositoryRoot = PreviewViewerPaths.FindRepositoryRoot(app.Environment.ContentRootPath);
string configuredSnapshotRoot = app.Configuration["SnapshotRoot"] ?? Path.Combine("artifacts", "debug-viewport");
string snapshotRoot = PreviewViewerPaths.ResolveRepoPath(repositoryRoot, configuredSnapshotRoot);
string configuredStyleManifest = app.Configuration["PreviewStyleManifest"] ??
    Path.Combine("Quantum.PreviewViewer", "preview-styles.json");
string styleManifestPath = PreviewViewerPaths.ResolveRepoPath(repositoryRoot, configuredStyleManifest);
string configuredStyleAssetRoot = app.Configuration["PreviewStyleAssetRoot"] ??
    Path.GetDirectoryName(styleManifestPath) ??
    repositoryRoot;
string styleAssetRoot = PreviewViewerPaths.ResolveRepoPath(repositoryRoot, configuredStyleAssetRoot);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/snapshots", () =>
{
    IReadOnlyList<SnapshotSummary> snapshots = SnapshotCatalog.FindSnapshots(repositoryRoot, snapshotRoot);
    return Results.Json(
        new SnapshotCatalogResponse(
            PreviewViewerPaths.ToRepositoryRelativePath(repositoryRoot, snapshotRoot),
            snapshots));
});

app.MapGet("/api/snapshot", (string path) =>
{
    if (!PreviewViewerPaths.TryResolveRepositoryFile(repositoryRoot, path, out string resolvedPath, out string error))
    {
        return Results.BadRequest(new { error });
    }

    SnapshotSummary? summary = SnapshotCatalog.TryReadSummary(repositoryRoot, resolvedPath);
    if (summary == null)
    {
        return Results.BadRequest(new { error = "File is not a valid DebugViewportSnapshotV1 JSON payload." });
    }

    string json = File.ReadAllText(resolvedPath);
    return Results.Text(json, "application/json");
});

app.MapGet("/api/styles", () =>
{
    JsonObject manifest = PreviewStyleManifestCatalog.LoadManifest(
        repositoryRoot,
        styleManifestPath,
        styleAssetRoot,
        "/style-assets");

    return Results.Json(manifest);
});

app.MapGet("/style-assets/{**assetPath}", (string assetPath) =>
{
    if (!PreviewStyleAssetFiles.TryResolveStyleAssetFile(
        styleAssetRoot,
        assetPath,
        out string resolvedPath,
        out string contentType,
        out string error))
    {
        int statusCode = error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Results.Problem(error, statusCode: statusCode);
    }

    return Results.File(resolvedPath, contentType, enableRangeProcessing: true);
});

app.MapGet("/api/health", () => Results.Json(new { status = "ok" }));

app.MapFallbackToFile("index.html");

app.Run();

public static class PreviewViewerPaths
{
    public static string FindRepositoryRoot(string contentRootPath)
    {
        DirectoryInfo? directory = new DirectoryInfo(contentRootPath);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QuantumCoasterWorks.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, ".."));
    }

    public static string ResolveRepoPath(string repositoryRoot, string path)
    {
        string candidate = Path.IsPathRooted(path)
            ? path
            : Path.Combine(repositoryRoot, path);

        return Path.GetFullPath(candidate);
    }

    public static bool TryResolveRepositoryFile(
        string repositoryRoot,
        string requestedPath,
        out string resolvedPath,
        out string error)
    {
        resolvedPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            error = "Snapshot path is required.";
            return false;
        }

        string candidate = ResolveRepoPath(repositoryRoot, requestedPath);
        if (!IsPathInsideDirectory(repositoryRoot, candidate))
        {
            error = "Snapshot path must resolve inside the repository. Use Open JSON for external files.";
            return false;
        }

        if (!File.Exists(candidate))
        {
            error = "Snapshot file was not found.";
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    public static string ToRepositoryRelativePath(string repositoryRoot, string path)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, path);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static bool IsPathInsideDirectory(string rootDirectory, string candidatePath)
    {
        string normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootDirectory));
        string normalizedCandidate = Path.GetFullPath(candidatePath);
        string normalizedRootWithoutTrailingSeparator = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(normalizedCandidate, normalizedRootWithoutTrailingSeparator, StringComparison.OrdinalIgnoreCase) ||
            normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}

public static class PreviewStyleManifestCatalog
{
    public const int CurrentVersion = 1;
    public const string DebugBoxesTrainStyleId = "debug-boxes";

    public static JsonObject LoadManifest(
        string repositoryRoot,
        string styleManifestPath,
        string styleAssetRoot,
        string assetRequestPathPrefix)
    {
        JsonObject manifest;
        if (!File.Exists(styleManifestPath))
        {
            manifest = CreateDefaultManifest("Style manifest was not found. Using debug train boxes.");
            AddRepositoryMetadata(manifest, repositoryRoot, styleManifestPath, styleAssetRoot);
            return manifest;
        }

        try
        {
            using FileStream stream = File.OpenRead(styleManifestPath);
            manifest = JsonNode.Parse(stream) as JsonObject ??
                CreateDefaultManifest("Style manifest root must be a JSON object. Using debug train boxes.");
        }
        catch (IOException)
        {
            manifest = CreateDefaultManifest("Style manifest could not be read. Using debug train boxes.");
        }
        catch (JsonException)
        {
            manifest = CreateDefaultManifest("Style manifest is invalid JSON. Using debug train boxes.");
        }
        catch (UnauthorizedAccessException)
        {
            manifest = CreateDefaultManifest("Style manifest was not accessible. Using debug train boxes.");
        }

        AddRepositoryMetadata(manifest, repositoryRoot, styleManifestPath, styleAssetRoot);
        EnsureDefaults(manifest);
        AddAssetUrls(manifest, styleAssetRoot, assetRequestPathPrefix.TrimEnd('/'), GetDiagnostics(manifest));
        return manifest;
    }

    private static void AddRepositoryMetadata(
        JsonObject manifest,
        string repositoryRoot,
        string styleManifestPath,
        string styleAssetRoot)
    {
        manifest["manifestPath"] = PreviewViewerPaths.IsPathInsideDirectory(repositoryRoot, styleManifestPath)
            ? PreviewViewerPaths.ToRepositoryRelativePath(repositoryRoot, styleManifestPath)
            : styleManifestPath;
        manifest["assetRoot"] = PreviewViewerPaths.IsPathInsideDirectory(repositoryRoot, styleAssetRoot)
            ? PreviewViewerPaths.ToRepositoryRelativePath(repositoryRoot, styleAssetRoot)
            : styleAssetRoot;
    }

    private static void EnsureDefaults(JsonObject manifest)
    {
        manifest["version"] ??= CurrentVersion;
        JsonArray trainStyles = GetOrCreateArray(manifest, "trainStyles");
        if (trainStyles.Count == 0)
        {
            trainStyles.Add(CreateDebugBoxesTrainStyle());
        }

        JsonObject? firstTrainStyle = trainStyles[0] as JsonObject;
        string defaultTrainStyle = TryGetString(manifest, "defaultTrainStyle") ??
            TryGetString(firstTrainStyle, "id") ??
            DebugBoxesTrainStyleId;
        manifest["defaultTrainStyle"] = defaultTrainStyle;
        manifest["trackStyles"] ??= new JsonArray();

        foreach (JsonNode? trainStyle in trainStyles)
        {
            if (trainStyle is JsonObject trainStyleObject)
            {
                trainStyleObject["roles"] ??= new JsonObject();
            }
        }
    }

    private static void AddAssetUrls(
        JsonNode? node,
        string styleAssetRoot,
        string assetRequestPathPrefix,
        JsonArray diagnostics)
    {
        if (node is JsonObject jsonObject)
        {
            if (TryGetString(jsonObject, "asset") is { Length: > 0 } assetPath &&
                TryBuildStyleAssetUrl(styleAssetRoot, assetPath, assetRequestPathPrefix, diagnostics, out string assetUrl))
            {
                jsonObject["assetUrl"] = assetUrl;
            }

            foreach (KeyValuePair<string, JsonNode?> property in jsonObject.ToList())
            {
                AddAssetUrls(property.Value, styleAssetRoot, assetRequestPathPrefix, diagnostics);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? item in jsonArray)
            {
                AddAssetUrls(item, styleAssetRoot, assetRequestPathPrefix, diagnostics);
            }
        }
    }

    private static bool TryBuildStyleAssetUrl(
        string styleAssetRoot,
        string assetPath,
        string assetRequestPathPrefix,
        JsonArray diagnostics,
        out string assetUrl)
    {
        assetUrl = string.Empty;
        string extension = Path.GetExtension(assetPath);
        if (!PreviewStyleAssetFiles.IsPrimaryModelExtension(extension))
        {
            diagnostics.Add(CreateDiagnostic(
                "warning",
                $"Style asset '{assetPath}' is not a supported .glb or .gltf model."));
            return false;
        }

        string resolvedPath = PreviewViewerPaths.ResolveRepoPath(styleAssetRoot, assetPath);
        if (!PreviewViewerPaths.IsPathInsideDirectory(styleAssetRoot, resolvedPath))
        {
            diagnostics.Add(CreateDiagnostic(
                "warning",
                $"Style asset '{assetPath}' must resolve inside the configured preview asset root."));
            return false;
        }

        if (!File.Exists(resolvedPath))
        {
            diagnostics.Add(CreateDiagnostic(
                "warning",
                $"Style asset '{assetPath}' was not found. Matching train boxes will use debug rendering."));
        }

        string relativePath = Path.GetRelativePath(styleAssetRoot, resolvedPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        assetUrl = $"{assetRequestPathPrefix}/{EscapePathSegments(relativePath)}";
        return true;
    }

    private static JsonObject CreateDefaultManifest(string diagnosticMessage)
    {
        return new JsonObject
        {
            ["version"] = CurrentVersion,
            ["defaultTrainStyle"] = DebugBoxesTrainStyleId,
            ["trainStyles"] = new JsonArray(CreateDebugBoxesTrainStyle()),
            ["trackStyles"] = new JsonArray(),
            ["diagnostics"] = new JsonArray(CreateDiagnostic("warning", diagnosticMessage))
        };
    }

    private static JsonObject CreateDebugBoxesTrainStyle()
    {
        return new JsonObject
        {
            ["id"] = DebugBoxesTrainStyleId,
            ["name"] = "Debug boxes",
            ["roles"] = new JsonObject()
        };
    }

    private static JsonArray GetDiagnostics(JsonObject manifest)
    {
        return GetOrCreateArray(manifest, "diagnostics");
    }

    private static JsonArray GetOrCreateArray(JsonObject manifest, string propertyName)
    {
        if (manifest[propertyName] is JsonArray array)
        {
            return array;
        }

        array = new JsonArray();
        manifest[propertyName] = array;
        return array;
    }

    private static JsonObject CreateDiagnostic(string severity, string message)
    {
        return new JsonObject
        {
            ["severity"] = severity,
            ["message"] = message
        };
    }

    private static string? TryGetString(JsonObject? jsonObject, string propertyName)
    {
        if (jsonObject == null ||
            jsonObject[propertyName] is not JsonValue value ||
            !value.TryGetValue(out string? result) ||
            string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        return result;
    }

    private static string EscapePathSegments(string path)
    {
        return string.Join(
            '/',
            path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }
}

public static class PreviewStyleAssetFiles
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".glb"] = "model/gltf-binary",
            [".gltf"] = "model/gltf+json",
            [".bin"] = "application/octet-stream",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".ktx2"] = "image/ktx2"
        };

    public static bool TryResolveStyleAssetFile(
        string styleAssetRoot,
        string requestedAssetPath,
        out string resolvedPath,
        out string contentType,
        out string error)
    {
        resolvedPath = string.Empty;
        contentType = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(requestedAssetPath))
        {
            error = "Style asset path is required.";
            return false;
        }

        string extension = Path.GetExtension(requestedAssetPath);
        if (!ContentTypes.TryGetValue(extension, out string? resolvedContentType))
        {
            error = "Style assets must be .glb, .gltf, .bin, or common image texture files.";
            return false;
        }

        string candidate = PreviewViewerPaths.ResolveRepoPath(styleAssetRoot, requestedAssetPath);
        if (!PreviewViewerPaths.IsPathInsideDirectory(styleAssetRoot, candidate))
        {
            error = "Style asset path must resolve inside the configured preview asset root.";
            return false;
        }

        if (!File.Exists(candidate))
        {
            error = "Style asset file was not found.";
            return false;
        }

        resolvedPath = candidate;
        contentType = resolvedContentType;
        return true;
    }

    public static bool IsPrimaryModelExtension(string extension)
    {
        return string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase);
    }
}

public static class SnapshotCatalog
{
    public const string ContractName = "quantum.debug_viewport_snapshot";

    public static IReadOnlyList<SnapshotSummary> FindSnapshots(string repositoryRoot, string snapshotRoot)
    {
        if (!Directory.Exists(snapshotRoot))
        {
            return Array.Empty<SnapshotSummary>();
        }

        var snapshots = new List<SnapshotSummary>();
        foreach (string path in Directory.EnumerateFiles(snapshotRoot, "*.json", SearchOption.AllDirectories))
        {
            SnapshotSummary? summary = TryReadSummary(repositoryRoot, path);
            if (summary != null)
            {
                snapshots.Add(summary);
            }
        }

        snapshots.Sort(static (left, right) =>
        {
            int modifiedComparison = right.ModifiedUtc.CompareTo(left.ModifiedUtc);
            return modifiedComparison != 0
                ? modifiedComparison
                : string.Compare(left.RepositoryRelativePath, right.RepositoryRelativePath, StringComparison.OrdinalIgnoreCase);
        });

        return snapshots;
    }

    public static SnapshotSummary? TryReadSummary(string repositoryRoot, string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (!TryGetString(root, "contract", out string? contract) ||
                !string.Equals(contract, ContractName, StringComparison.Ordinal))
            {
                return null;
            }

            int version = TryGetInt32(root, "version", out int parsedVersion) ? parsedVersion : 0;
            JsonElement metadata = root.TryGetProperty("metadata", out JsonElement metadataElement)
                ? metadataElement
                : default;

            string? sourceFixtureName = metadata.ValueKind == JsonValueKind.Object &&
                TryGetString(metadata, "sourceFixtureName", out string? parsedFixtureName)
                    ? parsedFixtureName
                    : null;

            int centerlinePointCount = GetArrayLength(root, "centerlinePoints");
            int frameCount = GetArrayLength(root, "frames");
            int lineCount = GetArrayLength(root, "lines");
            int boxCount = GetArrayLength(root, "boxes");
            int trainPoseCarCount = 0;
            if (root.TryGetProperty("trainPose", out JsonElement trainPose) &&
                trainPose.ValueKind == JsonValueKind.Object)
            {
                trainPoseCarCount = GetArrayLength(trainPose, "cars");
            }

            FileInfo file = new FileInfo(path);
            string repositoryRelativePath = PreviewViewerPaths.ToRepositoryRelativePath(repositoryRoot, file.FullName);

            return new SnapshotSummary(
                repositoryRelativePath,
                file.Name,
                file.Length,
                file.LastWriteTimeUtc,
                version,
                sourceFixtureName,
                centerlinePointCount,
                frameCount,
                lineCount,
                boxCount,
                trainPoseCarCount);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int GetArrayLength(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Array
                ? property.GetArrayLength()
                : 0;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return property.TryGetInt32(out value);
    }
}

public sealed record SnapshotCatalogResponse(
    string SnapshotRoot,
    IReadOnlyList<SnapshotSummary> Snapshots);

public sealed record SnapshotSummary(
    string RepositoryRelativePath,
    string FileName,
    long SizeBytes,
    DateTime ModifiedUtc,
    int Version,
    string? SourceFixtureName,
    int CenterlinePointCount,
    int FrameCount,
    int LineCount,
    int BoxCount,
    int TrainPoseCarCount);
