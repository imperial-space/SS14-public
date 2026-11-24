using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Content.Shared.WeeklyMode;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.WeeklyMode.Storage;

public sealed class WeeklyModeStore
{
    private static readonly string[] DefaultExcludedMobPrototypes =
    {
        "MobMouse",
        "MobMouseDead",
        "MobMouseAdmeme",
        "MobMouse1",
        "MobMouse2",
        "MobMouseCancer",
    };

    public const string StationFileName = "station.yml";
    public const string SnapshotMetadataFileName = "snapshot.json";
    public const string RoleOverridesFileName = "role-overrides.json";
    public const string ContainerPatchFileName = "container-patch.json";
    public const string IntegrityFileName = "integrity.json";
    public const string RecipesFileName = "recipes.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IResourceManager _resource;
    private readonly ResPath _root;

    public WeeklyModeStore(IResourceManager resource, string root)
    {
        _resource = resource;
        _root = NormalizeRoot(root);
    }

    public ResPath Root => _root;

    public ResPath SetsRoot => _root / "sets";

    public ResPath StatePath => _root / "state.json";

    public static bool IsSafeId([NotNullWhen(true)] string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        foreach (var c in id)
        {
            if (c is >= 'a' and <= 'z' ||
                c is >= 'A' and <= 'Z' ||
                c is >= '0' and <= '9' ||
                c is '-' or '_' or '.')
            {
                continue;
            }

            return false;
        }

        return id is not "." and not "..";
    }

    public WeeklyModeSet CreateSet(string setId, string baseMapPrototype, int autosaveMinutes, int retainAutosaves, string? displayName = null, string? baseMapPath = null)
    {
        ValidateId(setId, nameof(setId));

        var set = new WeeklyModeSet
        {
            SetId = setId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? setId : displayName,
            BaseMapPrototype = baseMapPrototype,
            BaseMapPath = baseMapPath ?? string.Empty,
            AutosaveMinutes = autosaveMinutes,
            AutosaveWarningMinutes = 2,
            RetainAutosaves = retainAutosaves,
        };

        SaveSet(set);
        return set;
    }

    public bool TryLoadSet(string setId, [NotNullWhen(true)] out WeeklyModeSet? set)
    {
        set = null;
        if (!IsSafeId(setId))
            return false;

        if (!TryReadJson(SetPath(setId), out set))
            return false;

        NormalizeSet(set);
        return true;
    }

    public WeeklyModeSet LoadSet(string setId)
    {
        if (!TryLoadSet(setId, out var set))
            throw new FileNotFoundException($"Weekly mode set '{setId}' was not found.");

        return set;
    }

    public void SaveSet(WeeklyModeSet set)
    {
        ValidateId(set.SetId, nameof(set.SetId));
        NormalizeSet(set);
        WriteJson(SetPath(set.SetId), set);
    }

    public string ExportSetJson(WeeklyModeSet set)
    {
        NormalizeSet(set);
        return JsonSerializer.Serialize(set, JsonOptions);
    }

    private static void NormalizeSet(WeeklyModeSet set)
    {
        set.DisplayName ??= set.SetId;
        set.BaseMapPrototype ??= string.Empty;
        set.BaseMapPath ??= string.Empty;
        set.CurrentSnapshot = string.IsNullOrWhiteSpace(set.CurrentSnapshot) ? null : set.CurrentSnapshot;
        set.Snapshots ??= new List<string>();
        set.DefaultDisabledJobs ??= new List<string>();
        set.DefaultRoleAliases ??= new Dictionary<string, string>();
        set.DefaultRoleLimits ??= new Dictionary<string, int>();
        set.ExcludedMobPrototypes ??= DefaultExcludedMobPrototypes.ToList();
        set.DiscordChannel ??= string.Empty;
        set.WeeklyTechnologies ??= new List<WeeklyTechnologyEntry>();
        set.WeeklyCargoProducts ??= new List<WeeklyCargoProductEntry>();
        set.ForcedRoleAssignments ??= new List<WeeklyForcedRoleAssignment>();
        set.CampaignState ??= new Dictionary<string, string>();

        foreach (var technology in set.WeeklyTechnologies)
        {
            technology.TechnologyId ??= string.Empty;
            technology.Branch ??= string.Empty;
            technology.RecipeIds ??= new List<string>();
        }

        foreach (var product in set.WeeklyCargoProducts)
        {
            product.ProductId ??= string.Empty;
            product.Category ??= string.Empty;
            product.ItemPrototype ??= string.Empty;
        }

        foreach (var assignment in set.ForcedRoleAssignments)
        {
            assignment.PlayerNetUserId ??= string.Empty;
            assignment.LastKnownCKey ??= string.Empty;
            assignment.JobId ??= string.Empty;
            assignment.CreatedBy ??= string.Empty;
            if (assignment.CreatedAt.Kind == DateTimeKind.Unspecified)
                assignment.CreatedAt = DateTime.SpecifyKind(assignment.CreatedAt, DateTimeKind.Utc);
        }
    }

    private static void NormalizeRecipes(WeeklyRecipesConfig config)
    {
        config.Recipes ??= new List<WeeklyRecipeDefinition>();

        foreach (var recipe in config.Recipes)
        {
            recipe.Id ??= string.Empty;
            recipe.ResultPrototype ??= string.Empty;
            recipe.LatheTargets ??= new List<string>();
            recipe.Materials ??= new Dictionary<string, int>();
            recipe.TechnologyIds ??= new List<string>();
        }
    }

    public IEnumerable<string> ListSetIds()
    {
        if (!_resource.UserData.Exists(SetsRoot))
            yield break;

        foreach (var entry in _resource.UserData.DirectoryEntries(SetsRoot))
        {
            if (!IsSafeId(entry))
                continue;

            var path = SetDirectory(entry);
            if (_resource.UserData.IsDir(path) && _resource.UserData.Exists(SetPath(entry)))
                yield return entry;
        }
    }

    public bool TryLoadState([NotNullWhen(true)] out WeeklyModeRuntimeState? state)
    {
        return TryReadJson(StatePath, out state);
    }

    public void SaveState(WeeklyModeRuntimeState state)
    {
        WriteJson(StatePath, state);
    }

    public ResPath SetDirectory(string setId)
    {
        ValidateId(setId, nameof(setId));
        return SetsRoot / setId;
    }

    public ResPath SetPath(string setId)
    {
        return SetDirectory(setId) / "set.json";
    }

    public ResPath RecipesPath(string setId)
    {
        return SetDirectory(setId) / RecipesFileName;
    }

    public bool TryLoadRecipes(string setId, [NotNullWhen(true)] out WeeklyRecipesConfig? config)
    {
        config = null;
        if (!IsSafeId(setId))
            return false;

        if (!TryReadJson(RecipesPath(setId), out config))
            return false;

        NormalizeRecipes(config);
        return true;
    }

    public WeeklyRecipesConfig LoadRecipesOrDefault(string setId)
    {
        ValidateId(setId, nameof(setId));

        if (!TryLoadRecipes(setId, out var config))
            return new WeeklyRecipesConfig();

        return config;
    }

    public void SaveRecipes(string setId, WeeklyRecipesConfig config)
    {
        ValidateId(setId, nameof(setId));
        NormalizeRecipes(config);
        WriteJson(RecipesPath(setId), config);
    }

    public string ExportRecipesJson(WeeklyRecipesConfig config)
    {
        NormalizeRecipes(config);
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    public ResPath SnapshotsDirectory(string setId)
    {
        return SetDirectory(setId) / "snapshots";
    }

    public ResPath SnapshotDirectory(string setId, string snapshotId)
    {
        ValidateId(setId, nameof(setId));
        ValidateId(snapshotId, nameof(snapshotId));
        return SnapshotsDirectory(setId) / snapshotId;
    }

    public ResPath SnapshotStationPath(string setId, string snapshotId)
    {
        return SnapshotFilePath(SnapshotDirectory(setId, snapshotId), StationFileName);
    }

    public ResPath SnapshotMetadataPath(string setId, string snapshotId)
    {
        return SnapshotFilePath(SnapshotDirectory(setId, snapshotId), SnapshotMetadataFileName);
    }

    public ResPath SnapshotRoleOverridesPath(string setId, string snapshotId)
    {
        return SnapshotFilePath(SnapshotDirectory(setId, snapshotId), RoleOverridesFileName);
    }

    public ResPath SnapshotContainerPatchPath(string setId, string snapshotId)
    {
        return SnapshotFilePath(SnapshotDirectory(setId, snapshotId), ContainerPatchFileName);
    }

    public ResPath SnapshotIntegrityPath(string setId, string snapshotId)
    {
        return SnapshotFilePath(SnapshotDirectory(setId, snapshotId), IntegrityFileName);
    }

    public ResPath SnapshotFilePath(ResPath snapshotDirectory, string fileName)
    {
        return snapshotDirectory / fileName;
    }

    public ResPath TempSnapshotDirectory(string setId, string snapshotId)
    {
        ValidateId(setId, nameof(setId));
        ValidateId(snapshotId, nameof(snapshotId));
        return SnapshotsDirectory(setId) / $".tmp-{snapshotId}-{Guid.NewGuid():N}";
    }

    public bool SnapshotExists(string setId, string snapshotId)
    {
        return IsSafeId(setId) &&
               IsSafeId(snapshotId) &&
               _resource.UserData.Exists(SnapshotStationPath(setId, snapshotId)) &&
               _resource.UserData.Exists(SnapshotMetadataPath(setId, snapshotId)) &&
               _resource.UserData.Exists(SnapshotRoleOverridesPath(setId, snapshotId)) &&
               _resource.UserData.Exists(SnapshotContainerPatchPath(setId, snapshotId)) &&
               _resource.UserData.Exists(SnapshotIntegrityPath(setId, snapshotId));
    }

    public bool TryLoadSnapshot(string setId, string snapshotId, [NotNullWhen(true)] out WeeklySnapshotMetadata? metadata)
    {
        metadata = null;
        if (!SnapshotExists(setId, snapshotId))
            return false;

        return TryReadJson(SnapshotMetadataPath(setId, snapshotId), out metadata);
    }

    public void SaveSnapshotMetadata(WeeklySnapshotMetadata metadata)
    {
        WriteJson(SnapshotMetadataPath(metadata.SetId, metadata.SnapshotId), metadata);
    }

    public void SaveSnapshotMetadata(ResPath snapshotDirectory, WeeklySnapshotMetadata metadata)
    {
        WriteJson(SnapshotFilePath(snapshotDirectory, SnapshotMetadataFileName), metadata);
    }

    public void SaveRoleOverrides(string setId, string snapshotId, WeeklyRoleOverrides overrides)
    {
        WriteJson(SnapshotRoleOverridesPath(setId, snapshotId), overrides);
    }

    public void SaveRoleOverrides(ResPath snapshotDirectory, WeeklyRoleOverrides overrides)
    {
        WriteJson(SnapshotFilePath(snapshotDirectory, RoleOverridesFileName), overrides);
    }

    public bool TryLoadRoleOverrides(string setId, string snapshotId, [NotNullWhen(true)] out WeeklyRoleOverrides? overrides)
    {
        overrides = null;
        if (!IsSafeId(setId) || !IsSafeId(snapshotId))
            return false;

        return TryReadJson(SnapshotRoleOverridesPath(setId, snapshotId), out overrides);
    }

    public void SaveContainerPatch(ResPath snapshotDirectory, WeeklyContainerPatch patch)
    {
        WriteJson(SnapshotFilePath(snapshotDirectory, ContainerPatchFileName), patch);
    }

    public bool TryLoadContainerPatch(string setId, string snapshotId, [NotNullWhen(true)] out WeeklyContainerPatch? patch)
    {
        patch = null;
        if (!IsSafeId(setId) || !IsSafeId(snapshotId))
            return false;

        return TryReadJson(SnapshotContainerPatchPath(setId, snapshotId), out patch);
    }

    public void SaveSnapshotIntegrity(ResPath snapshotDirectory, WeeklySnapshotIntegrity integrity)
    {
        WriteJson(SnapshotFilePath(snapshotDirectory, IntegrityFileName), integrity);
    }

    public bool TryLoadSnapshotIntegrity(string setId, string snapshotId, [NotNullWhen(true)] out WeeklySnapshotIntegrity? integrity)
    {
        integrity = null;
        if (!IsSafeId(setId) || !IsSafeId(snapshotId))
            return false;

        return TryReadJson(SnapshotIntegrityPath(setId, snapshotId), out integrity);
    }

    public void ReplaceSnapshotDirectory(ResPath tempDirectory, string setId, string snapshotId)
    {
        ReplaceSnapshotDirectoryCore(tempDirectory, setId, snapshotId, null);
    }

    internal void ReplaceSnapshotDirectoryForTest(ResPath tempDirectory, string setId, string snapshotId, Action? afterBackupMoved)
    {
        ReplaceSnapshotDirectoryCore(tempDirectory, setId, snapshotId, afterBackupMoved);
    }

    private void ReplaceSnapshotDirectoryCore(ResPath tempDirectory, string setId, string snapshotId, Action? afterBackupMoved)
    {
        ValidateId(setId, nameof(setId));
        ValidateId(snapshotId, nameof(snapshotId));

        var finalDirectory = SnapshotDirectory(setId, snapshotId);
        var tempPath = GetUserDataPath(tempDirectory);
        var finalPath = GetUserDataPath(finalDirectory);
        var finalParentPath = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException($"Could not resolve parent directory for '{finalDirectory}'.");
        var backupPath = GetBackupDirectoryPath(finalPath);

        EnsureSameVolume(tempPath, finalPath);
        EnsureSameVolume(finalPath, backupPath);

        if (!Directory.Exists(tempPath))
            throw new DirectoryNotFoundException($"Weekly snapshot temp directory does not exist: {tempDirectory}");

        if (File.Exists(tempPath))
            throw new IOException($"Weekly snapshot temp path is a file, not a directory: {tempDirectory}");

        Directory.CreateDirectory(finalParentPath);

        var backedUpExisting = false;
        var movedTempToFinal = false;

        try
        {
            if (File.Exists(finalPath))
                throw new IOException($"Weekly snapshot destination is a file, not a directory: {finalDirectory}");

            if (Directory.Exists(finalPath))
            {
                Directory.Move(finalPath, backupPath);
                backedUpExisting = true;
                afterBackupMoved?.Invoke();
            }

            Directory.Move(tempPath, finalPath);
            movedTempToFinal = true;
        }
        catch
        {
            if (backedUpExisting && !movedTempToFinal && Directory.Exists(backupPath))
                RestoreBackupDirectory(finalPath, backupPath);

            throw;
        }

        if (backedUpExisting && Directory.Exists(backupPath))
            Directory.Delete(backupPath, true);
    }

    public long GetDirectorySize(ResPath directory)
    {
        if (!_resource.UserData.Exists(directory))
            return 0;

        long size = 0;
        foreach (var entry in _resource.UserData.DirectoryEntries(directory))
        {
            var path = directory / entry;
            if (_resource.UserData.IsDir(path))
                size += GetDirectorySize(path);
            else if (_resource.UserData.Exists(path))
            {
                using var stream = _resource.UserData.OpenRead(path);
                size += stream.Length;
            }
        }

        return size;
    }

    public void DeleteSnapshot(string setId, string snapshotId)
    {
        if (!IsSafeId(setId) || !IsSafeId(snapshotId))
            return;

        _resource.UserData.Delete(SnapshotDirectory(setId, snapshotId));
    }

    private bool TryReadJson<T>(ResPath path, [NotNullWhen(true)] out T? value)
    {
        value = default;

        if (!_resource.UserData.Exists(path) || _resource.UserData.IsDir(path))
            return false;

        if (!_resource.UserData.TryReadAllText(path, out var text))
            return false;

        try
        {
            value = JsonSerializer.Deserialize<T>(text, JsonOptions);
            return value != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void WriteJson<T>(ResPath path, T value)
    {
        _resource.UserData.CreateDir(path.Directory);
        var tmpPath = path.WithName($".tmp-{path.Filename}-{Guid.NewGuid():N}");
        var json = JsonSerializer.Serialize(value, JsonOptions);
        _resource.UserData.WriteAllText(tmpPath, json);
        ReplaceUserDataFile(tmpPath, path);
    }

    private void ReplaceUserDataFile(ResPath tempPath, ResPath finalPath)
    {
        if (string.IsNullOrWhiteSpace(_resource.UserData.RootDir))
        {
            ReplaceUserDataFileWithProvider(tempPath, finalPath);
            return;
        }

        var tempFullPath = GetUserDataPath(tempPath);
        var finalFullPath = GetUserDataPath(finalPath);
        var finalParentPath = Path.GetDirectoryName(finalFullPath)
            ?? throw new InvalidOperationException($"Could not resolve parent directory for '{finalPath}'.");
        var backupPath = GetBackupFilePath(finalFullPath);

        EnsureSameVolume(tempFullPath, finalFullPath);
        EnsureSameVolume(finalFullPath, backupPath);

        if (!File.Exists(tempFullPath))
            throw new FileNotFoundException($"Weekly config temp file does not exist: {tempPath}");

        if (Directory.Exists(tempFullPath))
            throw new IOException($"Weekly config temp path is a directory, not a file: {tempPath}");

        Directory.CreateDirectory(finalParentPath);

        var backedUpExisting = false;
        var movedTempToFinal = false;

        try
        {
            if (Directory.Exists(finalFullPath))
                throw new IOException($"Weekly config destination is a directory, not a file: {finalPath}");

            if (File.Exists(finalFullPath))
            {
                File.Move(finalFullPath, backupPath);
                backedUpExisting = true;
            }

            File.Move(tempFullPath, finalFullPath);
            movedTempToFinal = true;
        }
        catch
        {
            if (backedUpExisting && File.Exists(backupPath))
            {
                if (File.Exists(finalFullPath))
                    File.Delete(finalFullPath);

                File.Move(backupPath, finalFullPath);
            }

            if (!movedTempToFinal && File.Exists(tempFullPath))
                File.Delete(tempFullPath);

            throw;
        }

        if (backedUpExisting && File.Exists(backupPath))
            File.Delete(backupPath);
    }

    private void ReplaceUserDataFileWithProvider(ResPath tempPath, ResPath finalPath)
    {
        EnsureSafeUserDataPath(tempPath);
        EnsureSafeUserDataPath(finalPath);

        if (!_resource.UserData.Exists(tempPath))
            throw new FileNotFoundException($"Weekly config temp file does not exist: {tempPath}");

        if (_resource.UserData.IsDir(tempPath))
            throw new IOException($"Weekly config temp path is a directory, not a file: {tempPath}");

        if (_resource.UserData.Exists(finalPath) && _resource.UserData.IsDir(finalPath))
            throw new IOException($"Weekly config destination is a directory, not a file: {finalPath}");

        var backupPath = finalPath.WithName($".backup-{finalPath.Filename}-{Guid.NewGuid():N}");
        var backedUpExisting = false;
        var movedTempToFinal = false;

        try
        {
            if (_resource.UserData.Exists(finalPath))
            {
                _resource.UserData.Rename(finalPath, backupPath);
                backedUpExisting = true;
            }

            _resource.UserData.Rename(tempPath, finalPath);
            movedTempToFinal = true;
        }
        catch
        {
            if (backedUpExisting && _resource.UserData.Exists(backupPath))
            {
                if (_resource.UserData.Exists(finalPath))
                    _resource.UserData.Delete(finalPath);

                _resource.UserData.Rename(backupPath, finalPath);
            }

            if (!movedTempToFinal && _resource.UserData.Exists(tempPath))
                _resource.UserData.Delete(tempPath);

            throw;
        }

        if (backedUpExisting && _resource.UserData.Exists(backupPath))
            _resource.UserData.Delete(backupPath);
    }

    private static void EnsureSafeUserDataPath(ResPath path)
    {
        if (!path.IsRooted)
            throw new InvalidOperationException($"Weekly mode path must be rooted: {path}");

        if (!ResPath.IsValidPath(path.CanonPath) || ContainsTraversal(path))
            throw new InvalidOperationException($"Weekly mode path may not contain path traversal: {path}");

        var relativePath = path.ToRelativeSystemPath();
        if (Path.IsPathRooted(relativePath) || Path.IsPathFullyQualified(relativePath))
            throw new InvalidOperationException($"Weekly mode path may not resolve to an external absolute path: {path}");
    }

    private string GetUserDataPath(ResPath path)
    {
        var rootDir = _resource.UserData.RootDir;
        if (string.IsNullOrWhiteSpace(rootDir))
            throw new NotSupportedException("Weekly mode atomic file replacement requires a real user data directory.");

        EnsureSafeUserDataPath(path);

        var rootPath = NormalizeRootDirectory(rootDir);
        var relativePath = path.ToRelativeSystemPath();
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        EnsurePathInsideRoot(fullPath, rootPath, path);
        return fullPath;
    }

    private string GetBackupDirectoryPath(string finalPath)
    {
        var finalParentPath = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException($"Could not resolve parent directory for '{finalPath}'.");
        var finalName = Path.GetFileName(Path.TrimEndingDirectorySeparator(finalPath));
        var backupPath = Path.Combine(finalParentPath, $".backup-{finalName}-{Guid.NewGuid():N}");
        var rootPath = NormalizeRootDirectory(_resource.UserData.RootDir!);
        EnsurePathInsideRoot(backupPath, rootPath, new ResPath(backupPath.Replace('\\', '/')));
        return backupPath;
    }

    private string GetBackupFilePath(string finalPath)
    {
        var finalParentPath = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException($"Could not resolve parent directory for '{finalPath}'.");
        var finalName = Path.GetFileName(finalPath);
        var backupPath = Path.Combine(finalParentPath, $".backup-{finalName}-{Guid.NewGuid():N}");
        var rootPath = NormalizeRootDirectory(_resource.UserData.RootDir!);
        EnsurePathInsideRoot(backupPath, rootPath, new ResPath(backupPath.Replace('\\', '/')));
        return backupPath;
    }

    private static void RestoreBackupDirectory(string finalPath, string backupPath)
    {
        if (Directory.Exists(finalPath))
            Directory.Delete(finalPath, true);
        else if (File.Exists(finalPath))
            File.Delete(finalPath);

        Directory.Move(backupPath, finalPath);
    }

    private static string NormalizeRootDirectory(string rootDir)
    {
        var fullPath = Path.GetFullPath(rootDir);
        return Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static void EnsurePathInsideRoot(string fullPath, string rootPath, ResPath sourcePath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedFullPath = Path.GetFullPath(fullPath);

        if (!normalizedFullPath.StartsWith(rootPath, comparison))
            throw new InvalidOperationException($"Weekly mode path resolves outside user data root: {sourcePath}");
    }

    private static void EnsureSameVolume(string leftPath, string rightPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var leftRoot = Path.GetPathRoot(Path.GetFullPath(leftPath));
        var rightRoot = Path.GetPathRoot(Path.GetFullPath(rightPath));

        if (!string.Equals(leftRoot, rightRoot, comparison))
            throw new IOException($"Weekly snapshot directories must stay on the same volume: '{leftPath}' -> '{rightPath}'.");
    }

    private static bool ContainsTraversal(ResPath path)
    {
        foreach (var segment in path.CanonPath.Split(ResPath.Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
                return true;
        }

        return false;
    }

    private static ResPath NormalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            root = "/weekly-mode";

        root = root.Replace('\\', '/').TrimEnd('/');
        if (!root.StartsWith('/'))
            root = $"/{root}";

        if (root.Contains(".."))
            throw new ArgumentException("weekly_mode.data_root may not contain '..'.", nameof(root));

        return new ResPath(root);
    }

    private static void ValidateId(string id, string paramName)
    {
        if (!IsSafeId(id))
            throw new ArgumentException("Expected a safe weekly mode id containing only ASCII letters, digits, '-', '_' or '.'.", paramName);
    }
}
