using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string repositoryRoot = PreviewViewerPaths.FindRepositoryRoot(app.Environment.ContentRootPath);
string configuredSnapshotRoot = app.Configuration["SnapshotRoot"] ?? Path.Combine("artifacts", "debug-viewport");
string snapshotRoot = PreviewViewerPaths.ResolveRepoPath(repositoryRoot, configuredSnapshotRoot);

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
        string normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(repositoryRoot));
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
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

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
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
