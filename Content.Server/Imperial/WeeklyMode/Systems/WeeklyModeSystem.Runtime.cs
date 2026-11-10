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
    public bool TryGetRoleAlias(string jobId, [NotNullWhen(true)] out string? alias)
    {
        alias = null;
        if (!_state.IsActive || _state.ActiveSetId == null)
            return false;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return false;

        return set.DefaultRoleAliases.TryGetValue(jobId, out alias) && !string.IsNullOrWhiteSpace(alias);
    }

    public string GetRoleDisplayName(string jobId, string fallback)
    {
        return TryGetRoleAlias(jobId, out var alias) ? alias : fallback;
    }

    public string GetJobDisplayName(ProtoId<JobPrototype> jobId)
    {
        if (_prototype.TryIndex<JobPrototype>(jobId, out var job))
            return GetRoleDisplayName(job.ID, job.LocalizedName);

        return jobId.Id;
    }

    public Dictionary<ProtoId<JobPrototype>, string> GetActiveRoleAliases()
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return new();

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return new();

        var aliases = new Dictionary<ProtoId<JobPrototype>, string>();
        foreach (var (jobId, alias) in set.DefaultRoleAliases)
        {
            if (_prototype.HasIndex<JobPrototype>(jobId))
                aliases[new ProtoId<JobPrototype>(jobId)] = alias;
        }

        return aliases;
    }

    public bool ShouldSuppressAutomaticGameRules()
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return false;

        return _store.TryLoadSet(_state.ActiveSetId, out var set) && !set.RandomGameRulesEnabled;
    }

    public bool TryGetActiveWeeklyCargoProducts(out List<WeeklyCargoProductData> products)
    {
        products = new List<WeeklyCargoProductData>();
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return false;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return false;

        foreach (var entry in set.WeeklyCargoProducts)
        {
            if (TryBuildWeeklyCargoProductData(entry, out var product))
                products.Add(product);
        }

        products.Sort((a, b) => string.Compare(a.ProductId, b.ProductId, StringComparison.Ordinal));

        return true;
    }

    public bool TryGetActiveWeeklyCargoProduct(string productId, out WeeklyCargoProductData product)
    {
        product = default;
        if (!TryGetActiveWeeklyCargoProducts(out var products))
            return false;

        foreach (var candidate in products)
        {
            if (!string.Equals(candidate.ProductId, productId, StringComparison.Ordinal))
                continue;

            product = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetActiveWeeklyCargoProductIds(out HashSet<string> productIds)
    {
        productIds = new HashSet<string>(StringComparer.Ordinal);
        if (!TryGetActiveWeeklyCargoProducts(out var products))
            return false;

        foreach (var entry in products)
            productIds.Add(entry.ProductId);

        return true;
    }

    public List<WeeklyLatheRecipeData> GetAvailableWeeklyLatheRecipes(EntityUid latheUid, LatheComponent lathe, bool getUnavailable = false)
    {
        var recipes = new List<WeeklyLatheRecipeData>();
        if (!TryGetActiveWeeklyRecipeDefinitions(out var definitions))
            return recipes;

        foreach (var definition in definitions)
        {
            if (!IsWeeklyRecipeTargetMatch(latheUid, lathe, definition))
                continue;

            if (!getUnavailable && !IsWeeklyRecipeUnlockedForLathe(latheUid, definition))
                continue;

            if (TryBuildWeeklyLatheRecipeData(definition, out var data))
                recipes.Add(data);
        }

        recipes.Sort((a, b) => string.Compare(a.RecipeId, b.RecipeId, StringComparison.Ordinal));
        return recipes;
    }

    public bool TryGetAvailableWeeklyLatheRecipe(EntityUid latheUid, LatheComponent lathe, string recipeId, out WeeklyLatheRecipeData recipe)
    {
        recipe = default;
        foreach (var candidate in GetAvailableWeeklyLatheRecipes(latheUid, lathe))
        {
            if (!string.Equals(candidate.RecipeId, recipeId, StringComparison.Ordinal))
                continue;

            recipe = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetActiveWeeklyLatheRecipe(string recipeId, out WeeklyLatheRecipeData recipe)
    {
        recipe = default;
        if (!TryGetActiveWeeklyRecipeDefinitions(out var definitions))
            return false;

        foreach (var definition in definitions)
        {
            if (!string.Equals(definition.Id, recipeId, StringComparison.Ordinal))
                continue;

            return TryBuildWeeklyLatheRecipeData(definition, out recipe);
        }

        return false;
    }

    private bool TryBuildWeeklyCargoProductData(WeeklyCargoProductEntry entry, out WeeklyCargoProductData product)
    {
        product = default;
        if (!_prototype.TryIndex<EntityPrototype>(entry.ItemPrototype, out var itemPrototype))
            return false;

        product = new WeeklyCargoProductData
        {
            ProductId = entry.ProductId,
            Name = itemPrototype.Name,
            Description = itemPrototype.Description,
            Category = entry.Category,
            Cost = entry.Cost,
            Boxed = entry.Boxed,
            Amount = entry.Amount,
            ItemPrototype = entry.ItemPrototype,
            Icon = new SpriteSpecifier.EntityPrototype(entry.ItemPrototype),
        };

        return true;
    }

    private bool TryGetActiveWeeklyRecipeDefinitions(out List<WeeklyRecipeDefinition> recipes)
    {
        recipes = new List<WeeklyRecipeDefinition>();
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return false;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return false;

        var config = _store.LoadRecipesOrDefault(set.SetId);
        SyncRecipeTechnologyLinks(set, config);
        recipes = config.Recipes;
        return true;
    }

    private bool IsWeeklyRecipeUnlockedForLathe(EntityUid latheUid, WeeklyRecipeDefinition recipe)
    {
        if (!TryComp<TechnologyDatabaseComponent>(latheUid, out var database) || !database.WeeklyModeOnly)
            return false;

        foreach (var technologyId in recipe.TechnologyIds)
        {
            foreach (var unlockedTechnologyId in database.WeeklyUnlockedTechnologies)
            {
                if (string.Equals(unlockedTechnologyId, technologyId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private bool IsWeeklyRecipeTargetMatch(EntityUid latheUid, LatheComponent lathe, WeeklyRecipeDefinition recipe)
    {
        if (recipe.LatheTargets.Count == 0)
            return false;

        var prototypeId = MetaData(latheUid).EntityPrototype?.ID;
        foreach (var target in recipe.LatheTargets)
        {
            if (IsWeeklyRecipeTargetMatch(target, prototypeId, lathe))
                return true;
        }

        return false;
    }

    private bool IsWeeklyRecipeTargetMatch(string target, string? entityPrototypeId, LatheComponent lathe)
    {
        if (WeeklyRecipeTargetAliases.TryGetValue(target, out var alias))
        {
            if (alias.All)
                return true;

            if (entityPrototypeId != null &&
                alias.Entities.Any(entity => string.Equals(entity, entityPrototypeId, StringComparison.Ordinal)))
            {
                return true;
            }

            return alias.Packs.Any(pack => LatheHasPack(lathe, pack));
        }

        if (target.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
        {
            var id = target["entity:".Length..];
            return entityPrototypeId != null && string.Equals(id, entityPrototypeId, StringComparison.Ordinal);
        }

        if (target.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
        {
            var id = target["pack:".Length..];
            return LatheHasPack(lathe, id);
        }

        return false;
    }

    private static bool LatheHasPack(LatheComponent lathe, string packId)
    {
        return lathe.StaticPacks.Any(pack => string.Equals(pack.Id, packId, StringComparison.Ordinal)) ||
               lathe.DynamicPacks.Any(pack => string.Equals(pack.Id, packId, StringComparison.Ordinal));
    }

    private bool TryBuildWeeklyLatheRecipeData(WeeklyRecipeDefinition definition, out WeeklyLatheRecipeData recipe)
    {
        recipe = default;
        if (!_prototype.TryIndex<EntityPrototype>(definition.ResultPrototype, out var resultPrototype))
            return false;

        recipe = new WeeklyLatheRecipeData
        {
            RecipeId = definition.Id,
            Name = resultPrototype.Name,
            Description = resultPrototype.Description,
            ResultPrototype = definition.ResultPrototype,
            ResultAmount = definition.ResultAmount,
            ProductionTimeSeconds = definition.ProductionTimeSeconds,
            ApplyMaterialDiscount = definition.ApplyMaterialDiscount,
            Materials = definition.Materials
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new WeeklyLatheRecipeMaterialData
                {
                    MaterialId = pair.Key,
                    Amount = pair.Value,
                })
                .ToList(),
            Icon = new SpriteSpecifier.EntityPrototype(definition.ResultPrototype),
        };

        return true;
    }

    public bool IsWeeklyAccessAllowed(ICommonSession session, bool notify)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return true;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return true;

        return IsWeeklyAccessAllowed(session, set, notify);
    }

    private bool IsWeeklyAccessAllowed(ICommonSession session, WeeklyModeSet set, bool notify)
    {
        if (set.MinPlaytimeHours <= 0)
            return true;

        var required = TimeSpan.FromHours(set.MinPlaytimeHours);
        var current = GetOverallPlaytimeOrZero(session);
        if (current >= required)
            return true;

        if (notify)
            SendAccessDeniedNotice(session, set, false);

        return false;
    }

    private TimeSpan GetOverallPlaytimeOrZero(ICommonSession session)
    {
        return _playTime.TryGetTrackerTimes(session, out var times) &&
               times.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var overall)
            ? overall
            : TimeSpan.Zero;
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        if (TryGetForcedAssignment(set, ev.PlayerSession.UserId, out var forcedAssignment))
            NotifyForcedRole(ev.PlayerSession, set, forcedAssignment, false);

        if (set.MinPlaytimeHours <= 0 || IsWeeklyAccessAllowed(ev.PlayerSession, set, false))
            return;

        if (_accessLobbyNoticeSent.Add(ev.PlayerSession.UserId))
            SendAccessDeniedNotice(ev.PlayerSession, set, true);
    }

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        if (!_playerManager.TryGetSessionById(ev.Player, out var session))
            return;

        if (IsWeeklyAccessAllowed(session, false))
            return;

        if (TryGetActiveForcedAssignment(ev.Player, out _, out var assignment) &&
            assignment.BypassPlaytime)
        {
            ev.Jobs.Clear();
            ev.Jobs.Add(new ProtoId<JobPrototype>(assignment.JobId));
            return;
        }

        if (!IsWeeklyAccessAllowed(session, true))
            ev.Jobs.Clear();
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Jobs is { Count: > 0 })
        {
            foreach (var job in ev.Jobs)
            {
                if (!CanLateJoinJob(ev.Player, EntityUid.Invalid, job, out _))
                {
                    ev.Cancelled = true;
                    return;
                }
            }
        }

        if (ev.Jobs is { Count: 1 } &&
            IsWeeklyAccessAllowedForJob(ev.Player, ev.Jobs[0].Id, true))
        {
            return;
        }

        if (IsWeeklyAccessAllowed(ev.Player, true))
            return;

        ev.Cancelled = true;
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (IsWeeklyAccessAllowed(ev.Player, false))
        {
            foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
            {
                var jobId = new ProtoId<JobPrototype>(job.ID);
                if (IsJobFullyReservedForOthers(jobId) &&
                    (!TryGetActiveForcedAssignment(ev.Player.UserId, out _, out var assignment) ||
                     !string.Equals(assignment.JobId, job.ID, StringComparison.Ordinal)))
                {
                    ev.Jobs.Add(jobId);
                }
            }

            return;
        }

        if (TryGetActiveForcedAssignment(ev.Player.UserId, out _, out var forcedAssignment) &&
            forcedAssignment.BypassPlaytime)
        {
            foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
            {
                if (!string.Equals(job.ID, forcedAssignment.JobId, StringComparison.Ordinal))
                    ev.Jobs.Add(job.ID);
            }

            return;
        }

        foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
            ev.Jobs.Add(job.ID);

        SendAccessDeniedNotice(ev.Player, LoadActiveSetOrNull(), false);
    }

    private WeeklyModeSet? LoadActiveSetOrNull()
    {
        return _state.ActiveSetId != null && _store.TryLoadSet(_state.ActiveSetId, out var set)
            ? set
            : null;
    }
}
