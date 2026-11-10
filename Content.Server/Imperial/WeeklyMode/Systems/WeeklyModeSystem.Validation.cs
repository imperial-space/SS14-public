using Content.Server.WeeklyMode.Components;
using Content.Shared.Imperial.ICCVar;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Cargo.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Light.EntitySystems;
using Content.Server.Maps;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Research.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.CartridgeLoader;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Server.WeeklyMode.Storage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.VendingMachines;
using Content.Shared.WeeklyMode;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server.WeeklyMode.Systems;

public sealed partial class WeeklyModeSystem
{
    private bool TryBuildWeeklyRecipeDefinition(
        string recipeId,
        string resultPrototype,
        int resultAmount,
        double productionTimeSeconds,
        string latheTargets,
        IReadOnlyList<string> materialSpecs,
        out WeeklyRecipeDefinition recipe,
        out string message)
    {
        recipe = new WeeklyRecipeDefinition();

        if (!WeeklyModeStore.IsSafeId(recipeId))
        {
            message = "recipeId must contain only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (_prototype.HasIndex<LatheRecipePrototype>(recipeId))
        {
            message = $"Weekly recipeId '{recipeId}' conflicts with an existing global lathe recipe prototype.";
            return false;
        }

        resultPrototype = resultPrototype.Trim();
        if (!_prototype.TryIndex<EntityPrototype>(resultPrototype, out var entityPrototype))
        {
            message = $"Unknown result entity prototype: {resultPrototype}";
            return false;
        }

        if (entityPrototype.Abstract)
        {
            message = $"Result entity prototype is abstract and cannot be produced: {resultPrototype}";
            return false;
        }

        if (resultAmount < 1)
        {
            message = "resultAmount must be at least 1.";
            return false;
        }

        if (!double.IsFinite(productionTimeSeconds) || productionTimeSeconds <= 0)
        {
            message = "productionTimeSeconds must be greater than zero.";
            return false;
        }

        if (!TryNormalizeLatheTargets(latheTargets, out var targets, out message))
            return false;

        if (!TryParseRecipeMaterials(materialSpecs, out var materials, out message))
            return false;

        recipe = new WeeklyRecipeDefinition
        {
            Id = recipeId,
            ResultPrototype = resultPrototype,
            ResultAmount = resultAmount,
            ProductionTimeSeconds = productionTimeSeconds,
            LatheTargets = targets,
            Materials = materials,
        };
        message = string.Empty;
        return true;
    }

    private bool TryNormalizeLatheTargets(string rawTargets, out List<string> targets, out string message)
    {
        targets = new List<string>();
        foreach (var rawTarget in rawTargets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryNormalizeLatheTarget(rawTarget, out var target, out message))
                return false;

            if (!targets.Contains(target, StringComparer.Ordinal))
                targets.Add(target);
        }

        targets.Sort(StringComparer.Ordinal);
        if (targets.Count == 0)
        {
            message = "At least one lathe target must be specified.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryNormalizeLatheTarget(string rawTarget, out string target, out string message)
    {
        target = rawTarget.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(target))
        {
            message = "Lathe target may not be empty.";
            return false;
        }

        if (WeeklyRecipeTargetAliases.TryGetValue(target, out var alias))
        {
            foreach (var entityId in alias.Entities)
            {
                if (!IsValidLatheEntityPrototype(entityId))
                {
                    message = $"Weekly recipe target alias '{target}' references missing lathe entity prototype '{entityId}'.";
                    return false;
                }
            }

            foreach (var packId in alias.Packs)
            {
                if (!_prototype.HasIndex<LatheRecipePackPrototype>(packId))
                {
                    message = $"Weekly recipe target alias '{target}' references missing lathe recipe pack '{packId}'.";
                    return false;
                }
            }

            target = target.ToLowerInvariant();
            message = string.Empty;
            return true;
        }

        if (target.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
        {
            var entityId = target["entity:".Length..].Trim();
            if (!IsValidLatheEntityPrototype(entityId))
            {
                message = $"Unknown lathe entity target: entity:{entityId}";
                return false;
            }

            target = $"entity:{entityId}";
            message = string.Empty;
            return true;
        }

        if (target.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
        {
            var packId = target["pack:".Length..].Trim();
            if (!_prototype.HasIndex<LatheRecipePackPrototype>(packId))
            {
                message = $"Unknown lathe recipe pack target: pack:{packId}";
                return false;
            }

            target = $"pack:{packId}";
            message = string.Empty;
            return true;
        }

        message = $"Unknown lathe target '{target}'. Use an alias ({FormatList(WeeklyRecipeTargetAliases.Keys)}), entity:<LatheEntityPrototypeId>, or pack:<LatheRecipePackPrototypeId>.";
        return false;
    }

    private bool IsValidLatheEntityPrototype(string entityId)
    {
        return _prototype.TryIndex<EntityPrototype>(entityId, out var entityPrototype) &&
               !entityPrototype.Abstract &&
               entityPrototype.Components.ContainsKey("Lathe");
    }

    private bool TryParseRecipeMaterials(IReadOnlyList<string> materialSpecs, out Dictionary<string, int> materials, out string message)
    {
        materials = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var spec in materialSpecs)
        {
            var parts = spec.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                message = $"Material '{spec}' must use MaterialPrototypeId:Amount.";
                return false;
            }

            var materialId = parts[0];
            if (!_prototype.HasIndex<MaterialPrototype>(materialId))
            {
                message = $"Unknown material prototype: {materialId}";
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                message = $"Material amount for '{materialId}' must be greater than zero.";
                return false;
            }

            materials[materialId] = materials.TryGetValue(materialId, out var existing)
                ? existing + amount
                : amount;
        }

        if (materials.Count == 0)
        {
            message = "At least one material must be specified.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static void SortRecipes(WeeklyRecipesConfig recipes)
    {
        recipes.Recipes.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
        foreach (var recipe in recipes.Recipes)
        {
            recipe.LatheTargets = recipe.LatheTargets.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            recipe.Materials = recipe.Materials.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            recipe.TechnologyIds = recipe.TechnologyIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        }
    }

    private static void SyncRecipeTechnologyLinks(WeeklyModeSet set, WeeklyRecipesConfig recipes)
    {
        foreach (var recipe in recipes.Recipes)
        {
            recipe.TechnologyIds = set.WeeklyTechnologies
                .Where(technology => technology.RecipeIds.Contains(recipe.Id, StringComparer.Ordinal))
                .Select(technology => technology.TechnologyId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        SortRecipes(recipes);
    }

    private bool IsWeeklyRecipeInUse(string recipeId)
    {
        if (!_state.IsActive)
            return false;

        var query = EntityQueryEnumerator<LatheComponent>();
        while (query.MoveNext(out _, out var lathe))
        {
            if (lathe.CurrentRecipeIsWeekly &&
                string.Equals(lathe.CurrentRecipe, recipeId, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (var batch in lathe.Queue)
            {
                if (batch.IsWeekly && string.Equals(batch.Recipe, recipeId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private WeeklyRecipesConfig CloneRecipes(WeeklyRecipesConfig recipes)
    {
        return JsonSerializer.Deserialize<WeeklyRecipesConfig>(_store.ExportRecipesJson(recipes))
               ?? throw new InvalidOperationException("Failed to clone weekly recipe config.");
    }

    private bool TryLoadMutableSet(string setId, [NotNullWhen(true)] out WeeklyModeSet? set, out string message)
    {
        set = null;

        if (!WeeklyModeStore.IsSafeId(setId))
        {
            message = "Invalid setId. Use only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (!_store.TryLoadSet(setId, out set))
        {
            message = $"Weekly set '{setId}' was not found.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryLoadConfigEditableSet(string setId, [NotNullWhen(true)] out WeeklyModeSet? set, out string message)
    {
        return TryLoadMutableSet(setId, out set, out message);
    }

    private bool TryCommitConfigChange(WeeklyModeSet originalSet, WeeklyModeSet changedSet, WeeklyLiveConfigChange liveChange, out string message)
    {
        if (!TryValidateSetConfig(changedSet, out var errors))
        {
            message = $"Weekly set '{changedSet.SetId}' config is invalid:\n- {string.Join("\n- ", errors)}";
            return false;
        }

        var active = IsActiveSet(changedSet.SetId);
        var saved = false;
        try
        {
            _store.SaveSet(changedSet);
            saved = true;

            if (active)
                ApplyLiveConfigChange(changedSet, liveChange);

            message = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            if (active && saved)
            {
                try
                {
                    _store.SaveSet(originalSet);
                    ApplyLiveConfigChange(originalSet, liveChange);
                }
                catch (Exception rollbackException)
                {
                    _sawmill.Error($"Failed to roll back live weekly config change for set '{changedSet.SetId}': {rollbackException}");
                }
            }

            message = $"Failed to apply weekly config change for set '{changedSet.SetId}': {e.Message}";
            return false;
        }
    }

    private bool TryCommitRecipesChange(WeeklyModeSet set, WeeklyRecipesConfig originalRecipes, WeeklyRecipesConfig changedRecipes, WeeklyLiveConfigChange liveChange, out string message)
    {
        var errors = ValidateWeeklyRecipes(set, changedRecipes).ToList();
        if (errors.Count > 0)
        {
            message = $"Weekly recipes for '{set.SetId}' are invalid:\n- {string.Join("\n- ", errors)}";
            return false;
        }

        var active = IsActiveSet(set.SetId);
        var saved = false;
        try
        {
            _store.SaveRecipes(set.SetId, changedRecipes);
            saved = true;

            if (active)
                ApplyLiveConfigChange(set, liveChange);

            message = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            if (active && saved)
            {
                try
                {
                    _store.SaveRecipes(set.SetId, originalRecipes);
                    ApplyLiveConfigChange(set, liveChange);
                }
                catch (Exception rollbackException)
                {
                    _sawmill.Error($"Failed to roll back live weekly recipe change for set '{set.SetId}': {rollbackException}");
                }
            }

            message = $"Failed to apply weekly recipe change for set '{set.SetId}': {e.Message}";
            return false;
        }
    }

    private bool TryCommitResearchConfigChange(WeeklyModeSet originalSet, WeeklyModeSet changedSet, out string message)
    {
        var originalRecipes = _store.LoadRecipesOrDefault(changedSet.SetId);
        var changedRecipes = CloneRecipes(originalRecipes);
        SyncRecipeTechnologyLinks(changedSet, changedRecipes);
        return TryCommitConfigAndRecipesChange(originalSet, changedSet, originalRecipes, changedRecipes, WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes, out message);
    }

    private bool TryCommitConfigAndRecipesChange(
        WeeklyModeSet originalSet,
        WeeklyModeSet changedSet,
        WeeklyRecipesConfig originalRecipes,
        WeeklyRecipesConfig changedRecipes,
        WeeklyLiveConfigChange liveChange,
        out string message)
    {
        if (!TryValidateSetConfig(changedSet, out var setErrors))
        {
            message = $"Weekly set '{changedSet.SetId}' config is invalid:\n- {string.Join("\n- ", setErrors)}";
            return false;
        }

        var recipeErrors = ValidateWeeklyRecipes(changedSet, changedRecipes).ToList();
        if (recipeErrors.Count > 0)
        {
            message = $"Weekly recipes for '{changedSet.SetId}' are invalid:\n- {string.Join("\n- ", recipeErrors)}";
            return false;
        }

        var active = IsActiveSet(changedSet.SetId);
        var savedSet = false;
        var savedRecipes = false;
        try
        {
            _store.SaveSet(changedSet);
            savedSet = true;
            _store.SaveRecipes(changedSet.SetId, changedRecipes);
            savedRecipes = true;

            if (active)
                ApplyLiveConfigChange(changedSet, liveChange);

            message = string.Empty;
            return true;
        }
        catch (Exception e)
        {
            if (savedSet || savedRecipes)
            {
                try
                {
                    _store.SaveSet(originalSet);
                    _store.SaveRecipes(originalSet.SetId, originalRecipes);
                    if (active)
                        ApplyLiveConfigChange(originalSet, liveChange);
                }
                catch (Exception rollbackException)
                {
                    _sawmill.Error($"Failed to roll back weekly config and recipe change for set '{changedSet.SetId}': {rollbackException}");
                }
            }

            message = $"Failed to apply weekly config change for set '{changedSet.SetId}': {e.Message}";
            return false;
        }
    }

    private void ApplyLiveConfigChange(WeeklyModeSet set, WeeklyLiveConfigChange liveChange)
    {
        if ((liveChange & WeeklyLiveConfigChange.Research) != 0)
        {
            ApplyWeeklyResearchOverlay(set);
            _research.RefreshResearchConsoles();
        }

        if ((liveChange & WeeklyLiveConfigChange.Cargo) != 0)
        {
            RaiseLocalEvent(new WeeklyCargoCatalogChangedEvent());
        }

        if ((liveChange & WeeklyLiveConfigChange.Roles) != 0)
        {
            ApplyRoleOverridesToStations(set);
        }

        if ((liveChange & (WeeklyLiveConfigChange.Research | WeeklyLiveConfigChange.Recipes)) != 0)
        {
            RaiseLocalEvent(new WeeklyRecipesChangedEvent());
        }
    }

    private WeeklyModeSet CloneSet(WeeklyModeSet set)
    {
        return JsonSerializer.Deserialize<WeeklyModeSet>(_store.ExportSetJson(set))
               ?? throw new InvalidOperationException($"Failed to clone weekly set '{set.SetId}'.");
    }

    private bool HasPurchasedWeeklyTechnology(string setId, string technologyId)
    {
        return GetPurchasedWeeklyTechnologies(setId).Contains(technologyId);
    }

    private HashSet<string> GetPurchasedWeeklyTechnologies(string setId)
    {
        var purchased = new HashSet<string>(StringComparer.Ordinal);
        if (!IsActiveSet(setId))
            return purchased;

        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out _, out var database))
        {
            if (!database.WeeklyModeOnly)
                continue;

            foreach (var technologyId in database.WeeklyUnlockedTechnologies)
                purchased.Add(technologyId);
        }

        return purchased;
    }

    private IEnumerable<string> ValidateForcedRoleAssignments(WeeklyModeSet set)
    {
        var errors = new List<string>();
        var seenPlayers = new HashSet<Guid>();
        var seenRecords = new HashSet<string>(StringComparer.Ordinal);
        var countsByJob = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var assignment in set.ForcedRoleAssignments)
        {
            if (!Guid.TryParse(assignment.PlayerNetUserId, out var userId))
            {
                errors.Add($"forced role assignment has invalid playerNetUserId '{assignment.PlayerNetUserId}'");
                continue;
            }

            if (!seenPlayers.Add(userId))
                errors.Add($"player '{assignment.PlayerNetUserId}' has more than one forced role assignment");

            var recordKey = $"{assignment.PlayerNetUserId}:{assignment.JobId}";
            if (!seenRecords.Add(recordKey))
                errors.Add($"duplicate forced role assignment '{recordKey}'");

            if (string.IsNullOrWhiteSpace(assignment.LastKnownCKey))
                errors.Add($"forced role assignment '{assignment.PlayerNetUserId}' has empty lastKnownCKey");

            if (!_prototype.TryIndex<JobPrototype>(assignment.JobId, out _))
                errors.Add($"forced role assignment for '{assignment.PlayerNetUserId}' has unknown job prototype '{assignment.JobId}'");

            if (set.DefaultDisabledJobs.Contains(assignment.JobId))
                errors.Add($"forced role assignment for '{assignment.PlayerNetUserId}' uses disabled job '{assignment.JobId}'");

            if (set.DefaultRoleLimits.TryGetValue(assignment.JobId, out var limit) && limit == 0)
                errors.Add($"forced role assignment for '{assignment.PlayerNetUserId}' uses job '{assignment.JobId}' with campaign role limit 0");

            countsByJob[assignment.JobId] = countsByJob.GetValueOrDefault(assignment.JobId) + 1;
        }

        foreach (var (jobId, count) in countsByJob)
        {
            if (set.DefaultRoleLimits.TryGetValue(jobId, out var limit) && limit >= 0 && count > limit)
                errors.Add($"forced role assignments for '{jobId}' exceed campaign role limit {limit}: {count}");
        }

        return errors;
    }

    private bool TryValidateSetConfig(WeeklyModeSet set, out List<string> errors)
    {
        errors = new List<string>();

        if (set.SchemaVersion != WeeklyModeSet.CurrentSchemaVersion)
            errors.Add($"schemaVersion {set.SchemaVersion} is not compatible with {WeeklyModeSet.CurrentSchemaVersion}");

        if (!WeeklyModeStore.IsSafeId(set.SetId))
            errors.Add("setId is not a safe weekly mode id");

        if (string.IsNullOrWhiteSpace(set.BaseMapPrototype))
            errors.Add("baseMapPrototype is empty");
        else if (!_prototype.TryIndex<GameMapPrototype>(set.BaseMapPrototype, out _))
            errors.Add($"unknown base map prototype '{set.BaseMapPrototype}'");

        if (!string.IsNullOrWhiteSpace(set.BaseMapPath))
        {
            if (!TryResolveBaseMapPath(set.BaseMapPath, out var resolvedMap, out _, out var mapError))
            {
                errors.Add(mapError);
            }
            else if (!string.Equals(resolvedMap.ID, set.BaseMapPrototype, StringComparison.Ordinal))
            {
                errors.Add($"baseMapPath '{set.BaseMapPath}' resolves to map prototype '{resolvedMap.ID}', not configured prototype '{set.BaseMapPrototype}'");
            }
        }

        if (set.AutosaveMinutes < 1)
            errors.Add("autosave interval must be at least 1 minute");

        if (set.AutosaveWarningMinutes < 0)
            errors.Add("autosave warning may not be negative");

        if (set.AutosaveWarningMinutes >= set.AutosaveMinutes)
            errors.Add("autosave warning must be shorter than the autosave interval");

        if (set.RetainAutosaves < 1)
            errors.Add("autosave retention must be at least 1");

        foreach (var jobId in set.DefaultDisabledJobs.Distinct(StringComparer.Ordinal))
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
                errors.Add($"unknown disabled job prototype '{jobId}'");
        }

        foreach (var (jobId, alias) in set.DefaultRoleAliases)
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
                errors.Add($"unknown aliased job prototype '{jobId}'");

            if (!TryNormalizeAlias(alias, out _, out var aliasError))
                errors.Add($"invalid alias for '{jobId}': {aliasError}");
        }

        foreach (var (jobId, limit) in set.DefaultRoleLimits)
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out _))
                errors.Add($"unknown limited job prototype '{jobId}'");

            if (limit < 0)
                errors.Add($"role limit for '{jobId}' may not be negative");
        }

        foreach (var prototypeId in set.ExcludedMobPrototypes.Distinct(StringComparer.Ordinal))
        {
            if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out _))
                errors.Add($"unknown excluded mob prototype '{prototypeId}'");
        }

        if (set.MinPlaytimeHours < 0)
            errors.Add("minimum playtime may not be negative");

        if (set.DiscordChannel.Any(char.IsControl))
            errors.Add("discord channel/link may not contain control characters");

        var weeklyRecipes = _store.LoadRecipesOrDefault(set.SetId);
        errors.AddRange(ValidateWeeklyRecipes(set, weeklyRecipes));
        errors.AddRange(ValidateWeeklyTechnologies(set, weeklyRecipes));
        errors.AddRange(ValidateWeeklyCargoProducts(set));
        errors.AddRange(ValidateForcedRoleAssignments(set));

        return errors.Count == 0;
    }

    private void DeactivateWeeklyMode()
    {
        RestoreAllRolesOnStations();
        _state.IsActive = false;
        _state.ActiveSetId = null;
        _state.ActiveSnapshotId = null;
        _state.PendingSnapshotId = null;
        _state.StartedAtUtc = null;
        _state.StartedBy = null;
        ClearActiveWeeklyMapTracking();
        _nextAutosaveAt = null;
        _autosaveWarningIssued = false;
        _accessLobbyNoticeSent.Clear();
        _accessNoticeCooldowns.Clear();
        _forcedRoleNoticeCooldowns.Clear();
        RestoreWeeklyResearchOverlay();
        RaiseLocalEvent(new WeeklyRecipesChangedEvent());
        _gameMapManager.ClearSelectedMap();
        _store.SaveState(_state);
    }

    private bool TryResolveBaseMapPath(
        string rawPath,
        [NotNullWhen(true)] out GameMapPrototype? baseMap,
        out ResPath mapPath,
        out string message)
    {
        baseMap = null;
        mapPath = default;

        if (!TryNormalizeContentMapPath(rawPath, out mapPath, out message))
            return false;

        if (!_resource.ContentFileExists(mapPath))
        {
            message = $"Map file '{mapPath}' does not exist in server resources.";
            return false;
        }

        var resolvedPath = mapPath;
        baseMap = _prototype.EnumeratePrototypes<GameMapPrototype>()
            .FirstOrDefault(map => string.Equals(map.MapPath.CanonPath, resolvedPath.CanonPath, StringComparison.Ordinal));

        if (baseMap == null)
        {
            message = $"Map file '{mapPath}' exists, but no GameMapPrototype references it. Add a map prototype for this file before creating a Weekly set.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryNormalizeContentMapPath(string rawPath, out ResPath path, out string message)
    {
        path = default;
        rawPath = rawPath.Trim().Trim('"').Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            message = "Map path may not be empty.";
            return false;
        }

        if (rawPath.Contains(':') || rawPath.StartsWith("//", StringComparison.Ordinal))
        {
            message = "Map path must be a resource path, not an operating-system absolute path.";
            return false;
        }

        if (!rawPath.StartsWith("/", StringComparison.Ordinal))
        {
            message = "Map path must start with '/', for example /Maps/saltern.yml.";
            return false;
        }

        if (!ResPath.IsValidPath(rawPath))
        {
            message = "Map path contains invalid separators.";
            return false;
        }

        path = new ResPath(rawPath).ToRootedPath();
        if (ContainsTraversal(path))
        {
            message = "Map path may not contain '..'.";
            return false;
        }

        if (!string.Equals(path.Extension, "yml", StringComparison.OrdinalIgnoreCase))
        {
            message = "Map path must point to a .yml file.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryGetConfiguredBaseMapPath(WeeklyModeSet set, out ResPath mapPath)
    {
        mapPath = default;
        if (string.IsNullOrWhiteSpace(set.BaseMapPath))
            return false;

        if (!TryNormalizeContentMapPath(set.BaseMapPath, out var resolved, out _))
            return false;

        mapPath = resolved;
        return true;
    }

    private static bool ContainsTraversal(ResPath path)
    {
        foreach (var segment in path.CanonPath.Split(ResPath.SeparatorStr, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
                return true;
        }

        return false;
    }

    private IEnumerable<string> ValidateWeeklyTechnologies(WeeklyModeSet set)
    {
        return ValidateWeeklyTechnologies(set, _store.LoadRecipesOrDefault(set.SetId));
    }

    private IEnumerable<string> ValidateWeeklyTechnologies(WeeklyModeSet set, WeeklyRecipesConfig recipes)
    {
        var weeklyRecipeIds = recipes.Recipes.Select(recipe => recipe.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in set.WeeklyTechnologies)
        {
            if (!TryNormalizeTechnologyBranch(entry.Branch, out var normalizedBranch, out var branchError))
            {
                yield return $"technology '{entry.TechnologyId}' has invalid branch: {branchError}";
            }
            else if (!WeeklyTechnologyDisciplineIds.TryGetValue(normalizedBranch, out var disciplineId) ||
                     !_prototype.TryIndex<TechDisciplinePrototype>(disciplineId, out _))
            {
                yield return $"technology '{entry.TechnologyId}' branch '{entry.Branch}' has no matching research discipline";
            }

            if (!WeeklyModeStore.IsSafeId(entry.TechnologyId))
                yield return $"technologyId '{entry.TechnologyId}' is not a safe id";

            if (!seen.Add(entry.TechnologyId))
                yield return $"duplicate weekly technology '{entry.TechnologyId}'";

            if (entry.Cost < 0)
                yield return $"technology '{entry.TechnologyId}' cost may not be negative";

            if (entry.Tier is < 1 or > 3)
                yield return $"technology '{entry.TechnologyId}' tier must be 1, 2 or 3";

            foreach (var recipeId in entry.RecipeIds.Distinct(StringComparer.Ordinal))
            {
                if (!_prototype.TryIndex<LatheRecipePrototype>(recipeId, out _) &&
                    !weeklyRecipeIds.Contains(recipeId))
                {
                    yield return $"technology '{entry.TechnologyId}' references unknown recipe '{recipeId}'";
                }
            }
        }
    }

    private IEnumerable<string> ValidateWeeklyRecipes(WeeklyModeSet set, WeeklyRecipesConfig recipes)
    {
        if (recipes.SchemaVersion != WeeklyRecipesConfig.CurrentSchemaVersion)
            yield return $"recipes schemaVersion {recipes.SchemaVersion} is not compatible with {WeeklyRecipesConfig.CurrentSchemaVersion}";

        var technologyIds = set.WeeklyTechnologies
            .Select(technology => technology.TechnologyId)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in recipes.Recipes)
        {
            if (!WeeklyModeStore.IsSafeId(recipe.Id))
                yield return $"recipeId '{recipe.Id}' is not a safe id";

            if (!seen.Add(recipe.Id))
                yield return $"duplicate weekly recipe '{recipe.Id}'";

            if (_prototype.HasIndex<LatheRecipePrototype>(recipe.Id))
                yield return $"recipe '{recipe.Id}' conflicts with a global lathe recipe prototype";

            if (!_prototype.TryIndex<EntityPrototype>(recipe.ResultPrototype, out var resultPrototype))
            {
                yield return $"recipe '{recipe.Id}' references unknown result entity prototype '{recipe.ResultPrototype}'";
            }
            else if (resultPrototype.Abstract)
            {
                yield return $"recipe '{recipe.Id}' result entity prototype '{recipe.ResultPrototype}' is abstract";
            }

            if (recipe.ResultAmount < 1)
                yield return $"recipe '{recipe.Id}' resultAmount must be at least 1";

            if (!double.IsFinite(recipe.ProductionTimeSeconds) || recipe.ProductionTimeSeconds <= 0)
                yield return $"recipe '{recipe.Id}' productionTimeSeconds must be greater than zero";

            if (recipe.LatheTargets.Count == 0)
                yield return $"recipe '{recipe.Id}' must specify at least one lathe target";

            foreach (var target in recipe.LatheTargets)
            {
                if (!TryNormalizeLatheTarget(target, out _, out var targetError))
                    yield return $"recipe '{recipe.Id}' has invalid target '{target}': {targetError}";
            }

            if (recipe.Materials.Count == 0)
                yield return $"recipe '{recipe.Id}' must specify at least one material";

            foreach (var (materialId, amount) in recipe.Materials)
            {
                if (!_prototype.HasIndex<MaterialPrototype>(materialId))
                    yield return $"recipe '{recipe.Id}' references unknown material '{materialId}'";

                if (amount <= 0)
                    yield return $"recipe '{recipe.Id}' material '{materialId}' amount must be greater than zero";
            }

            foreach (var technologyId in recipe.TechnologyIds.Distinct(StringComparer.Ordinal))
            {
                if (!technologyIds.Contains(technologyId))
                    yield return $"recipe '{recipe.Id}' references unknown weekly technology '{technologyId}'";
            }
        }
    }

    private IEnumerable<string> ValidateWeeklyCargoProducts(WeeklyModeSet set)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in set.WeeklyCargoProducts)
        {
            if (!WeeklyModeStore.IsSafeId(entry.ProductId))
                yield return $"cargo productId '{entry.ProductId}' is not a safe id";

            if (!seen.Add(entry.ProductId))
                yield return $"duplicate weekly cargo product '{entry.ProductId}'";

            if (string.IsNullOrWhiteSpace(entry.Category))
                yield return $"cargo product '{entry.ProductId}' category may not be empty";

            if (entry.Cost < 0)
                yield return $"cargo product '{entry.ProductId}' cost may not be negative";

            if (entry.Amount < 1)
                yield return $"cargo product '{entry.ProductId}' amount must be at least 1";

            if (!_prototype.TryIndex<EntityPrototype>(entry.ItemPrototype, out _))
                yield return $"cargo product '{entry.ProductId}' references unknown item prototype '{entry.ItemPrototype}'";

            if (entry.Boxed && entry.Amount > GetCargoSettings().BoxedCapacity)
                yield return $"cargo product '{entry.ProductId}' boxed amount exceeds default crate capacity ({GetCargoSettings().BoxedCapacity})";
        }
    }

    private static string DescribeBaseMap(WeeklyModeSet set)
    {
        return string.IsNullOrWhiteSpace(set.BaseMapPath)
            ? set.BaseMapPrototype
            : $"{set.BaseMapPath} ({set.BaseMapPrototype})";
    }

    private void SendAutosaveOoc(string message)
    {
        _chatManager.ChatMessageToAll(ChatChannel.OOC, message, message, EntityUid.Invalid, false, true);
    }

    private void SendAccessDeniedNotice(ICommonSession session, WeeklyModeSet? set, bool force)
    {
        if (set == null || set.MinPlaytimeHours <= 0)
            return;

        if (!force &&
            _accessNoticeCooldowns.TryGetValue(session.UserId, out var nextAllowed) &&
            _timing.CurTime < nextAllowed)
        {
            return;
        }

        _accessNoticeCooldowns[session.UserId] = _timing.CurTime + TimeSpan.FromSeconds(30);
        _chatManager.DispatchServerMessage(session, BuildAccessDeniedNotice(set));
    }

    private static string BuildAccessDeniedNotice(WeeklyModeSet set)
    {
        var discord = string.IsNullOrWhiteSpace(set.DiscordChannel)
            ? "Discord-канале сервера"
            : set.DiscordChannel;

        return
            $"Для участия в этом мероприятии необходимо иметь минимум {set.MinPlaytimeHours} часов игрового времени именно на этом сервере.\n\n" +
            "Это ограничение используется для защиты мероприятия от организованных набегов и случайных нарушителей.\n\n" +
            "Вы пока не можете выбирать роли, но можете наблюдать за игрой в качестве призрака.\n\n" +
            $"Дополнительная информация о мероприятии находится в Discord-канале сервера: {discord}";
    }

    private static string FormatList(IEnumerable<string> values)
    {
        var ordered = values.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "<none>" : string.Join(", ", ordered);
    }

    private static string FormatDictionary<TValue>(IReadOnlyDictionary<string, TValue> values)
    {
        if (values.Count == 0)
            return "<none>";

        return string.Join(", ", values
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}"));
    }

    private static bool TryNormalizeAlias(string rawAlias, out string alias, out string message)
    {
        alias = rawAlias.Trim().Trim('"');

        if (alias.Length == 0)
        {
            message = "Alias may not be empty.";
            return false;
        }

        if (alias.Length > 64)
        {
            message = "Alias may not be longer than 64 characters.";
            return false;
        }

        foreach (var c in alias)
        {
            if (char.IsControl(c))
            {
                message = "Alias may not contain control characters.";
                return false;
            }

            if (c is '[' or ']')
            {
                message = "Alias may not contain markup brackets '[' or ']'.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool TryNormalizeTechnologyBranch(string rawBranch, out string branch, out string message)
    {
        branch = rawBranch.Trim().ToLowerInvariant();
        if (!WeeklyTechnologyBranches.Contains(branch))
        {
            message = $"Branch must be one of: {string.Join(", ", WeeklyTechnologyBranches.OrderBy(x => x, StringComparer.Ordinal))}.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1)
            return $"{age.TotalDays:F1}d";

        if (age.TotalHours >= 1)
            return $"{age.TotalHours:F1}h";

        if (age.TotalMinutes >= 1)
            return $"{age.TotalMinutes:F1}m";

        return $"{Math.Max(0, age.TotalSeconds):F0}s";
    }

    private void PruneAutosaves(WeeklyModeSet set)
    {
        var retain = Math.Max(1, set.RetainAutosaves);
        var autos = new List<WeeklySnapshotMetadata>();

        foreach (var snapshotId in set.Snapshots)
        {
            if (_store.TryLoadSnapshot(set.SetId, snapshotId, out var metadata) &&
                metadata.Kind == WeeklySnapshotKind.Auto)
            {
                autos.Add(metadata);
            }
        }

        foreach (var metadata in autos.OrderByDescending(x => x.CreatedAtUtc).Skip(retain))
        {
            if (metadata.SnapshotId == set.CurrentSnapshot)
                continue;

            _store.DeleteSnapshot(set.SetId, metadata.SnapshotId);
            set.Snapshots.Remove(metadata.SnapshotId);
        }
    }

    private bool IsActiveSet(string setId)
    {
        return _state.IsActive && _state.ActiveSetId == setId;
    }

    private bool EnsureEnabled(out string message)
    {
        if (_enabled)
        {
            message = string.Empty;
            return true;
        }

        message = "Weekly mode is disabled. Set weekly_mode.enabled to true first.";
        return false;
    }

    private bool TryEnterOperation(out string message)
    {
        if (_operationInProgress)
        {
            message = "A weekly mode operation is already in progress.";
            return false;
        }

        _operationInProgress = true;
        message = string.Empty;
        return true;
    }

    private void RestartForWeeklyMap()
    {
        _nextAutosaveAt = null;
        ClearOriginalRoleSlots();
        _gameTicker.RestartRound();
    }

    private void RebuildStore(string root)
    {
        _store = new WeeklyModeStore(_resource, root);
    }

    private static void CopyState(WeeklyModeRuntimeState source, WeeklyModeRuntimeState target)
    {
        target.SchemaVersion = source.SchemaVersion;
        target.IsActive = source.IsActive;
        target.ActiveSetId = source.ActiveSetId;
        target.ActiveSnapshotId = source.ActiveSnapshotId;
        target.PendingSnapshotId = source.PendingSnapshotId;
        target.ActiveWeeklyMapId = source.ActiveWeeklyMapId;
        target.ActiveWeeklyMapEntity = source.ActiveWeeklyMapEntity;
        target.ActiveWeeklyGridIds = source.ActiveWeeklyGridIds?.ToList() ?? new List<int>();
        target.ActiveWeeklyMapPrototype = source.ActiveWeeklyMapPrototype;
        target.ActiveWeeklyMapName = source.ActiveWeeklyMapName;
        target.StartedAtUtc = source.StartedAtUtc;
        target.StartedBy = source.StartedBy;
    }

    private static string BuildSnapshotId(WeeklySnapshotKind kind)
    {
        var prefix = kind switch
        {
            WeeklySnapshotKind.Auto => "auto",
            WeeklySnapshotKind.Manual => "manual",
            WeeklySnapshotKind.Endshift => "endshift",
            WeeklySnapshotKind.RollbackPoint => "rollback-point",
            WeeklySnapshotKind.RollbackBackup => "rollback-backup",
            _ => "snapshot",
        };

        return $"{prefix}-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
    }
}
