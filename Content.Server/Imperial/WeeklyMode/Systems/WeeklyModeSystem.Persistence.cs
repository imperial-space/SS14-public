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
    private bool TryLoadCompatibleSnapshot(
        WeeklyModeSet set,
        string snapshotId,
        [NotNullWhen(true)] out WeeklySnapshotMetadata? metadata,
        out string message)
    {
        metadata = null;

        if (!_store.TryLoadSnapshot(set.SetId, snapshotId, out metadata))
        {
            message = $"Snapshot '{snapshotId}' was not found in set '{set.SetId}'.";
            return false;
        }

        if (!string.Equals(metadata.SetId, set.SetId, StringComparison.Ordinal))
        {
            message = $"Snapshot '{snapshotId}' belongs to set '{metadata.SetId}', not '{set.SetId}'.";
            return false;
        }

        if (!string.Equals(metadata.BaseMapPrototype, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            message =
                "Snapshot map mismatch:\n" +
                $"set base map: {set.BaseMapPrototype}\n" +
                $"snapshot map: {metadata.BaseMapPrototype}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.BaseMapPath) &&
            !string.IsNullOrWhiteSpace(set.BaseMapPath) &&
            !string.Equals(metadata.BaseMapPath, set.BaseMapPath, StringComparison.Ordinal))
        {
            message =
                "Snapshot map path mismatch:\n" +
                $"set base map path: {set.BaseMapPath}\n" +
                $"snapshot map path: {metadata.BaseMapPath}";
            return false;
        }

        if (!TryReadSnapshotStationMapName(set.SetId, snapshotId, out var snapshotMapName, out message))
        {
            message = $"Snapshot '{snapshotId}' cannot be validated: {message}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.SavedMapName) &&
            !string.Equals(metadata.SavedMapName, snapshotMapName, StringComparison.Ordinal))
        {
            message =
                "Snapshot map mismatch:\n" +
                $"metadata map: {metadata.SavedMapName}\n" +
                $"station map: {snapshotMapName}";
            return false;
        }

        if (IsDevMapMismatch(set.BaseMapPrototype, snapshotMapName))
        {
            message =
                "Snapshot map mismatch:\n" +
                $"set base map: {set.BaseMapPrototype}\n" +
                $"snapshot map: {snapshotMapName}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryReadSnapshotStationMapName(
        string setId,
        string snapshotId,
        [NotNullWhen(true)] out string? mapName,
        out string message)
    {
        mapName = null;
        var stationPath = _store.SnapshotStationPath(setId, snapshotId);

        if (!_mapLoader.TryReadFile(stationPath, out var data))
        {
            message = $"could not read {stationPath}.";
            return false;
        }

        return TryReadSerializedMapName(data, out mapName, out message);
    }

    internal static bool TryReadSerializedMapName(
        MappingDataNode data,
        [NotNullWhen(true)] out string? mapName,
        out string message)
    {
        mapName = null;
        var mapYamlIds = new HashSet<int>();

        if (data.TryGet<SequenceDataNode>("maps", out var maps))
        {
            foreach (var node in maps)
            {
                if (node is ValueDataNode value && int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    mapYamlIds.Add(id);
            }
        }

        foreach (var entity in EnumerateSerializedEntities(data))
        {
            if (!TryGetSerializedEntityUid(entity, out var uid))
                continue;

            if (mapYamlIds.Count > 0 && !mapYamlIds.Contains(uid))
                continue;

            if (!TryGetSerializedComponent(entity, "Map", out _))
                continue;

            if (TryGetSerializedMetaName(entity, out mapName))
            {
                message = string.Empty;
                return true;
            }

            message = $"serialized map entity {uid} has no MetaData.name.";
            return false;
        }

        if (mapYamlIds.Count > 0)
        {
            message = $"serialized map entity {string.Join(",", mapYamlIds.OrderBy(id => id))} was not found.";
            return false;
        }

        message = "serialized map root was not found.";
        return false;
    }

    private static IEnumerable<MappingDataNode> EnumerateSerializedEntities(MappingDataNode data)
    {
        if (!data.TryGet<SequenceDataNode>("entities", out var groups))
            yield break;

        foreach (var groupNode in groups)
        {
            if (groupNode is not MappingDataNode group ||
                !group.TryGet<SequenceDataNode>("entities", out var entities))
            {
                continue;
            }

            foreach (var entityNode in entities)
            {
                if (entityNode is MappingDataNode entity)
                    yield return entity;
            }
        }
    }

    private static bool TryGetSerializedEntityUid(MappingDataNode entity, out int uid)
    {
        uid = 0;
        return entity.TryGet<ValueDataNode>("uid", out var uidNode) &&
               int.TryParse(uidNode.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uid);
    }

    private static bool TryGetSerializedComponent(
        MappingDataNode entity,
        string componentType,
        [NotNullWhen(true)] out MappingDataNode? component)
    {
        component = null;
        if (!entity.TryGet<SequenceDataNode>("components", out var components))
            return false;

        foreach (var componentNode in components)
        {
            if (componentNode is not MappingDataNode candidate ||
                !candidate.TryGet<ValueDataNode>("type", out var typeNode) ||
                !string.Equals(typeNode.Value, componentType, StringComparison.Ordinal))
            {
                continue;
            }

            component = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGetSerializedMetaName(MappingDataNode entity, [NotNullWhen(true)] out string? mapName)
    {
        mapName = null;
        if (!TryGetSerializedComponent(entity, "MetaData", out var meta) ||
            !meta.TryGet<ValueDataNode>("name", out var nameNode) ||
            string.IsNullOrWhiteSpace(nameNode.Value))
        {
            return false;
        }

        mapName = nameNode.Value;
        return true;
    }

    private List<EntityUid> CollectGridUids(MapId mapId)
    {
        var result = new List<EntityUid>();
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out var meta))
        {
            if (xform.MapID == mapId && meta.EntityLifeStage < EntityLifeStage.Terminating)
                result.Add(uid);
        }

        return result;
    }

    private int CountEntitiesOnMap(MapId mapId, EntityUid mapEntity, IReadOnlyCollection<EntityUid> gridUids)
    {
        var count = 0;
        foreach (var uid in CollectWeeklyMapEntities(mapEntity, gridUids))
        {
            if (!_metaQuery.TryGetComponent(uid, out var meta) ||
                meta.EntityLifeStage >= EntityLifeStage.Terminating)
                continue;

            count++;
        }

        return count;
    }

    private string GetCurrentOrConfiguredMapName()
    {
        var defaultMap = _gameTicker.DefaultMap;
        if (defaultMap != MapId.Nullspace && _map.MapExists(defaultMap))
        {
            var mapEntity = _map.GetMapOrInvalid(defaultMap);
            if (_metaQuery.TryGetComponent(mapEntity, out var meta) &&
                !string.IsNullOrWhiteSpace(meta.EntityName))
            {
                return meta.EntityName;
            }
        }

        return _gameMapManager.GetSelectedMap()?.MapName ?? "<none>";
    }

    private void ClearActiveWeeklyMapTracking()
    {
        _activeWeeklyMapId = null;
        _activeWeeklyMapEntity = null;
        _activeWeeklyGridIds.Clear();
        _activeWeeklyMapPrototype = null;
        _activeWeeklyMapName = null;
        _state.ActiveWeeklyMapId = null;
        _state.ActiveWeeklyMapEntity = null;
        _state.ActiveWeeklyGridIds = new List<int>();
        _state.ActiveWeeklyMapPrototype = null;
        _state.ActiveWeeklyMapName = null;
    }

    private static bool IsDevMapMismatch(string baseMapPrototype, string? mapName)
    {
        return !string.Equals(baseMapPrototype, "Dev", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(mapName, "Dev", StringComparison.OrdinalIgnoreCase);
    }

    private void SuppressSnapshotMapInitOnlyComponents(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids)
    {
        var weeklyEntities = CollectWeeklyMapEntities(mapEntity, gridUids);
        var removed = 0;
        removed += RemoveMapInitOnlyComponents<ContainerFillComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<EntityTableContainerFillComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<StorageFillComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<RandomFillSolutionComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<ConditionalSpawnerComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<RandomSpawnerComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<EntityTableSpawnerComponent>(weeklyEntities);
        removed += RemoveMapInitOnlyComponents<RandomDecalSpawnerComponent>(weeklyEntities);

        if (removed > 0)
            _sawmill.Info($"Suppressed {removed} map-init-only fill components before initializing weekly snapshot map {mapId}.");
    }

    private int RemoveMapInitOnlyComponents<T>(HashSet<EntityUid> weeklyEntities) where T : IComponent
    {
        var removed = 0;
        foreach (var uid in weeklyEntities)
        {
            if (!HasComp<T>(uid))
                continue;

            RemComp<T>(uid);
            removed++;
        }

        return removed;
    }

    private void SuppressSnapshotStartingItems(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids)
    {
        var weeklyEntities = CollectWeeklyMapEntities(mapEntity, gridUids);
        var poweredLightStartupLamps = 0;
        var itemSlotStartingItems = 0;
        var actionGrantStartingActions = 0;
        var binInitialContents = 0;
        var cartridgePreinstalledPrograms = 0;
        var lightReplacerStartingContents = 0;
        var vendingInitialStocks = 0;

        foreach (var uid in weeklyEntities)
        {
            if (TryComp<PoweredLightComponent>(uid, out var poweredLight) &&
                _poweredLight.ClearSpawnedPrototype((uid, poweredLight)))
            {
                poweredLightStartupLamps++;
            }

            if (TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            {
                var itemSlotsChanged = false;
                foreach (var slot in itemSlots.Slots.Values)
                {
                    if (string.IsNullOrEmpty(slot.StartingItem))
                        continue;

                    slot.StartingItem = null;
                    itemSlotsChanged = true;
                    itemSlotStartingItems++;
                }

                if (itemSlotsChanged)
                    Dirty(uid, itemSlots);
            }

            if (TryComp<ActionGrantComponent>(uid, out var actionGrant))
                actionGrantStartingActions += _actionGrant.SuppressMapInitGrants((uid, actionGrant));

            if (TryComp<BinComponent>(uid, out var bin))
                binInitialContents += _bin.SuppressInitialContents((uid, bin));

            if (TryComp<CartridgeLoaderComponent>(uid, out var cartridgeLoader))
            {
                var suppressed = cartridgeLoader.PreinstalledPrograms.Count;
                if (suppressed > 0)
                {
                    cartridgeLoader.PreinstalledPrograms.Clear();
                    Dirty(uid, cartridgeLoader);
                    cartridgePreinstalledPrograms += suppressed;
                }
            }

            if (TryComp<LightReplacerComponent>(uid, out var lightReplacer))
                lightReplacerStartingContents += _lightReplacer.SuppressStartingContents((uid, lightReplacer));

            if (TryComp<VendingMachineComponent>(uid, out var vending) &&
                !vending.SuppressInitialRestock)
            {
                vending.SuppressInitialRestock = true;
                Dirty(uid, vending);
                vendingInitialStocks++;
            }
        }

        if (poweredLightStartupLamps > 0 ||
            itemSlotStartingItems > 0 ||
            actionGrantStartingActions > 0 ||
            binInitialContents > 0 ||
            cartridgePreinstalledPrograms > 0 ||
            lightReplacerStartingContents > 0 ||
            vendingInitialStocks > 0)
        {
            _sawmill.Info(
                "Suppressed snapshot starting items before initializing weekly snapshot map:\n" +
                $"map={mapId}\n" +
                $"poweredLightStartupLamps={poweredLightStartupLamps}\n" +
                $"itemSlotStartingItems={itemSlotStartingItems}\n" +
                $"actionGrantStartingActions={actionGrantStartingActions}\n" +
                $"binInitialContents={binInitialContents}\n" +
                $"cartridgePreinstalledPrograms={cartridgePreinstalledPrograms}\n" +
                $"lightReplacerStartingContents={lightReplacerStartingContents}\n" +
                $"vendingInitialStocks={vendingInitialStocks}");
        }
    }

    private void ApplyContainerPatch(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids,
        WeeklyContainerPatch patch,
        string setId,
        string snapshotId)
    {
        var yamlToEntity = BuildYamlEntityMap(mapId, mapEntity, gridUids);
        var restored = 0;
        var skipped = 0;

        foreach (var entry in patch.Entries
                     .OrderBy(x => x.OwnerDepth)
                     .ThenBy(x => x.OwnerYamlUid)
                     .ThenBy(x => x.ContainerId, StringComparer.Ordinal)
                     .ThenBy(x => x.Index))
        {
            if (!yamlToEntity.TryGetValue(entry.OwnerYamlUid, out var owner))
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped: owner yaml uid {entry.OwnerYamlUid} missing in snapshot '{snapshotId}'.");
                continue;
            }

            if (!yamlToEntity.TryGetValue(entry.ChildYamlUid, out var child))
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped: child yaml uid {entry.ChildYamlUid} missing in snapshot '{snapshotId}'.");
                continue;
            }

            if (!_containers.TryGetContainer(owner, entry.ContainerId, out var container))
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped: container '{entry.ContainerId}' missing on {ToPrettyString(owner)} in snapshot '{snapshotId}'.");
                continue;
            }

            if (container.Contains(child))
            {
                restored++;
                continue;
            }

            try
            {
                if (_containers.TryGetContainingContainer((child, null, null), out var currentContainer) &&
                    currentContainer != container)
                {
                    _containers.Remove((child, null, null), currentContainer, reparent: false, force: true);
                }

                if (_containers.Insert((child, null, null), container, force: true))
                {
                    restored++;
                    continue;
                }

                skipped++;
                _sawmill.Warning($"Container patch skipped: failed to insert {ToPrettyString(child)} into '{entry.ContainerId}' on {ToPrettyString(owner)}.");
            }
            catch (Exception e)
            {
                skipped++;
                _sawmill.Warning($"Container patch skipped corrupt entry owner={entry.OwnerYamlUid} child={entry.ChildYamlUid} container={entry.ContainerId}: {e.Message}");
            }
        }

        _sawmill.Info($"Applied weekly container patch for set '{setId}' snapshot '{snapshotId}': restored={restored}, skipped={skipped}, entries={patch.Entries.Count}.");
    }

    private void DeactivateLoadedSnapshotBorgs(EntityUid mapEntity, IReadOnlyCollection<EntityUid> gridUids)
    {
        var weeklyEntities = CollectWeeklyMapEntities(mapEntity, gridUids);
        var deactivated = 0;
        foreach (var uid in weeklyEntities)
        {
            if (!TryComp<BorgChassisComponent>(uid, out var borg))
                continue;

            if (borg.Active)
                _borg.SetActive((uid, borg), false);

            if (TryComp<MindContainerComponent>(uid, out var mindContainer) &&
                mindContainer.Mind != null)
            {
                _mind.TransferTo(mindContainer.Mind.Value, null, createGhost: false);
            }

            deactivated++;
        }

        if (deactivated > 0)
            _sawmill.Info($"Deactivated {deactivated} borg chassis after Weekly snapshot load.");
    }

    private Dictionary<int, EntityUid> BuildYamlEntityMap(
        MapId mapId,
        EntityUid mapEntity,
        IReadOnlyCollection<EntityUid> gridUids)
    {
        var result = new Dictionary<int, EntityUid>();
        foreach (var uid in CollectWeeklyMapEntities(mapEntity, gridUids))
        {
            if (!_yamlUidQuery.TryGetComponent(uid, out var yaml))
                continue;

            result.TryAdd(yaml.Uid, uid);
        }

        return result;
    }

    private HashSet<EntityUid> CollectWeeklyMapEntities(EntityUid mapEntity, IReadOnlyCollection<EntityUid> gridUids)
    {
        var entities = new HashSet<EntityUid>();
        var toVisit = new List<EntityUid>();
        if (mapEntity.IsValid())
            toVisit.Add(mapEntity);

        foreach (var grid in gridUids)
        {
            if (grid.IsValid())
                toVisit.Add(grid);
        }

        for (var i = 0; i < toVisit.Count; i++)
        {
            var uid = toVisit[i];
            if (!entities.Add(uid) ||
                !_xformQuery.TryGetComponent(uid, out var xform))
            {
                continue;
            }

            using var children = xform.ChildEnumerator;
            while (children.MoveNext(out var child))
                toVisit.Add(child);
        }

        return entities;
    }

    private void ApplyRoleOverridesToStations(WeeklyModeSet set)
    {
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out _))
        {
            ApplyRoleOverridesToStation(station, set);
        }
    }

    private void ApplyRoleOverridesToStation(EntityUid station, WeeklyModeSet set)
    {
        var disabledJobs = set.DefaultDisabledJobs.ToHashSet(StringComparer.Ordinal);
        foreach (var jobId in set.DefaultDisabledJobs)
        {
            if (!_stationJobs.TryGetJobSlot(station, jobId, out var current))
                continue;

            var stationSlots = EnsureComp<WeeklyStationRoleStateComponent>(station).OriginalSlots;
            stationSlots.TryAdd(jobId, current);
            _stationJobs.TrySetJobSlot(station, jobId, 0);
        }

        foreach (var (jobId, limit) in set.DefaultRoleLimits.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (disabledJobs.Contains(jobId) || limit < 0)
                continue;

            if (!_stationJobs.TryGetJobSlot(station, jobId, out var current))
                continue;

            var stationSlots = EnsureComp<WeeklyStationRoleStateComponent>(station).OriginalSlots;
            stationSlots.TryAdd(jobId, current);
            _stationJobs.TrySetJobSlot(station, jobId, limit);
        }
    }

    private void RestoreRolesOnStations(IReadOnlyList<string> jobIds)
    {
        if (jobIds.Count == 0)
            return;

        var query = EntityQueryEnumerator<WeeklyStationRoleStateComponent>();
        while (query.MoveNext(out var station, out var roleState))
        {
            var slots = roleState.OriginalSlots;
            foreach (var jobId in jobIds)
            {
                if (!slots.TryGetValue(jobId, out var original))
                    continue;

                if (original == null)
                    _stationJobs.MakeJobUnlimited(station, jobId);
                else
                    _stationJobs.TrySetJobSlot(station, jobId, original.Value, true);

                slots.Remove(jobId);
            }

            if (slots.Count == 0)
                RemComp<WeeklyStationRoleStateComponent>(station);
        }
    }

    private void RestoreAllRolesOnStations()
    {
        var query = EntityQueryEnumerator<WeeklyStationRoleStateComponent>();
        while (query.MoveNext(out var station, out var roleState))
        {
            var slots = roleState.OriginalSlots;
            foreach (var (jobId, original) in slots.ToArray())
            {
                if (original == null)
                    _stationJobs.MakeJobUnlimited(station, jobId);
                else
                    _stationJobs.TrySetJobSlot(station, jobId, original.Value, true);
            }
        }

        ClearOriginalRoleSlots();
    }

    private void ClearOriginalRoleSlots()
    {
        var query = EntityQueryEnumerator<WeeklyStationRoleStateComponent>();
        while (query.MoveNext(out var uid, out _))
            RemComp<WeeklyStationRoleStateComponent>(uid);
    }

    private void ApplyWeeklyResearchOverlay(WeeklyModeSet set)
    {
        var technologies = BuildWeeklyResearchData(set);

        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var database))
            _research.SetWeeklyModeOverlay(uid, true, technologies, true, database);

        RaiseLocalEvent(new WeeklyRecipesChangedEvent());
    }

    private void RestoreWeeklyResearchOverlay()
    {
        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var database))
        {
            if (!database.WeeklyModeOnly &&
                database.WeeklyAllowedTechnologies.Count == 0 &&
                database.WeeklyTechnologies.Count == 0 &&
                database.WeeklyUnlockedTechnologies.Count == 0)
            {
                continue;
            }

            _research.SetWeeklyModeOverlay(uid, false, Array.Empty<WeeklyTechnologyData>(), false, database);
        }
    }

    private List<WeeklyTechnologyData> BuildWeeklyResearchData(WeeklyModeSet set)
    {
        var technologies = new List<WeeklyTechnologyData>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in set.WeeklyTechnologies)
        {
            if (!seen.Add(entry.TechnologyId))
                continue;

            if (!TryNormalizeTechnologyBranch(entry.Branch, out var branch, out _) ||
                !WeeklyTechnologyDisciplineIds.TryGetValue(branch, out var disciplineId) ||
                !_prototype.TryIndex<TechDisciplinePrototype>(disciplineId, out var discipline))
            {
                continue;
            }

            var name = entry.TechnologyId;
            var icon = discipline.Icon;
            if (_prototype.TryIndex<TechnologyPrototype>(entry.TechnologyId, out var existingTechnology))
            {
                name = Loc.GetString(existingTechnology.Name);
                icon = existingTechnology.Icon;
            }

            technologies.Add(new WeeklyTechnologyData
            {
                TechnologyId = entry.TechnologyId,
                Name = name,
                Branch = disciplineId,
                Cost = entry.Cost,
                Tier = entry.Tier,
                RecipeIds = entry.RecipeIds.Distinct(StringComparer.Ordinal).ToList(),
                Icon = icon,
            });
        }

        technologies.Sort((a, b) => string.Compare(a.TechnologyId, b.TechnologyId, StringComparison.Ordinal));
        return technologies;
    }

    internal SnapshotExclusionResult CollectSnapshotExclusionsForTest(WeeklyModeSet set)
    {
        return CollectSnapshotExclusions(set);
    }

    private SnapshotExclusionResult CollectSnapshotExclusions(WeeklyModeSet set)
    {
        var excluded = new HashSet<EntityUid>();
        var forceMapSavablePrototypes = new HashSet<string>(StringComparer.Ordinal);
        var excludedPrototypes = set.ExcludedMobPrototypes.ToHashSet(StringComparer.Ordinal);
        var autonomousMobsIncluded = 0;
        var autonomousMobsExcluded = 0;
        var playerBodiesExcluded = 0;
        var borgChassisIncluded = 0;
        var borgChassisExcluded = 0;

        void AddExcludedPlayerControlled(EntityUid uid)
        {
            if (!excluded.Add(uid))
                return;

            if (HasComp<BorgChassisComponent>(uid))
                borgChassisExcluded++;
            else if (HasComp<MobStateComponent>(uid))
                playerBodiesExcluded++;
        }

        void AddExcludedAutonomousMob(EntityUid uid)
        {
            if (excluded.Add(uid))
                autonomousMobsExcluded++;
        }

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } attached)
                continue;

            if (!ShouldPreservePlayerControlledEntity(attached, set))
                AddExcludedPlayerControlled(attached);
        }

        var actors = EntityQueryEnumerator<ActorComponent>();
        while (actors.MoveNext(out var uid, out _))
        {
            if (!ShouldPreservePlayerControlledEntity(uid, set))
                AddExcludedPlayerControlled(uid);
        }

        var ghosts = EntityQueryEnumerator<GhostComponent>();
        while (ghosts.MoveNext(out var uid, out _))
            excluded.Add(uid);

        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var uid, out _))
            excluded.Add(uid);

        var mindContainers = EntityQueryEnumerator<MindContainerComponent>();
        while (mindContainers.MoveNext(out var uid, out var mindContainer))
        {
            if (mindContainer.HasMind || mindContainer.Mind != null)
            {
                if (!ShouldPreservePlayerControlledEntity(uid, set))
                    AddExcludedPlayerControlled(uid);
            }
        }

        var mobs = EntityQueryEnumerator<MobStateComponent>();
        while (mobs.MoveNext(out var uid, out _))
        {
            if (excluded.Contains(uid))
                continue;

            var prototypeId = GetPrototypeId(uid);
            var hasPrototype = !string.IsNullOrWhiteSpace(prototypeId);
            if (hasPrototype &&
                IsPrototypeOrParentExcluded(prototypeId, excludedPrototypes))
            {
                if (HasComp<BorgChassisComponent>(uid))
                {
                    if (excluded.Add(uid))
                        borgChassisExcluded++;
                }
                else
                {
                    AddExcludedAutonomousMob(uid);
                }

                continue;
            }

            if (ShouldPreservePlayerControlledEntity(uid, set))
            {
                if (hasPrototype)
                    forceMapSavablePrototypes.Add(prototypeId);

                borgChassisIncluded++;
                continue;
            }

            if (IsPlayerControlledEntity(uid))
            {
                AddExcludedPlayerControlled(uid);
                continue;
            }

            if (!set.PersistAutonomousMobs)
            {
                AddExcludedAutonomousMob(uid);
                continue;
            }

            if (hasPrototype)
                forceMapSavablePrototypes.Add(prototypeId);

            autonomousMobsIncluded++;
        }

        return new SnapshotExclusionResult(
            excluded,
            forceMapSavablePrototypes,
            autonomousMobsIncluded,
            autonomousMobsExcluded,
            playerBodiesExcluded,
            borgChassisIncluded,
            borgChassisExcluded);
    }

    private Dictionary<EntityPrototype, bool> ForceMapSavablePrototypes(IReadOnlySet<string> prototypeIds)
    {
        var changed = new Dictionary<EntityPrototype, bool>();
        foreach (var prototypeId in prototypeIds)
        {
            if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype))
                continue;

            if (prototype.MapSavable)
                continue;

            changed[prototype] = prototype.MapSavable;
            prototype.MapSavable = true;
        }

        return changed;
    }

    private static void RestoreMapSavablePrototypes(Dictionary<EntityPrototype, bool> changed)
    {
        foreach (var (prototype, previous) in changed)
        {
            prototype.MapSavable = previous;
        }
    }

    private bool ShouldPreservePlayerControlledEntity(EntityUid uid, WeeklyModeSet set)
    {
        return set.PersistPlayerControlledBorgs && HasComp<BorgChassisComponent>(uid);
    }

    private bool IsPlayerControlledEntity(EntityUid uid)
    {
        if (_actorQuery.HasComponent(uid))
            return true;

        if (_mindContainerQuery.TryGetComponent(uid, out var mindContainer) &&
            (mindContainer.HasMind || mindContainer.Mind != null))
        {
            return true;
        }

        return _playerManager.Sessions.Any(session => session.AttachedEntity == uid);
    }

    private bool IsPrototypeOrParentExcluded(string prototypeId, IReadOnlySet<string> excludedPrototypes)
    {
        if (excludedPrototypes.Contains(prototypeId))
            return true;

        foreach (var (parentId, _) in _prototype.EnumerateAllParents<EntityPrototype>(prototypeId, true))
        {
            if (excludedPrototypes.Contains(parentId))
                return true;
        }

        return false;
    }

    private WeeklyContainerPatch BuildContainerPatch(
        IReadOnlyDictionary<EntityUid, int> yamlUidMap,
        HashSet<EntityUid> excludedRoots)
    {
        var patch = new WeeklyContainerPatch();
        var query = EntityQueryEnumerator<ContainerManagerComponent>();

        while (query.MoveNext(out var owner, out var manager))
        {
            if (IsEntityOrParentExcluded(owner, excludedRoots))
            {
                patch.SkippedExcluded++;
                continue;
            }

            if (!yamlUidMap.TryGetValue(owner, out var ownerYaml))
            {
                patch.SkippedUnserialized++;
                continue;
            }

            var ownerDepth = GetContainerOwnerDepth(owner);
            foreach (var (containerId, container) in manager.Containers.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var contained = container.ContainedEntities;
                for (var i = 0; i < contained.Count; i++)
                {
                    var child = contained[i];
                    if (IsEntityOrParentExcluded(child, excludedRoots))
                    {
                        patch.SkippedExcluded++;
                        continue;
                    }

                    if (!yamlUidMap.TryGetValue(child, out var childYaml))
                    {
                        patch.SkippedUnserialized++;
                        continue;
                    }

                    patch.Entries.Add(new WeeklyContainerEntry
                    {
                        OwnerYamlUid = ownerYaml,
                        OwnerPrototype = GetPrototypeId(owner),
                        ContainerId = containerId,
                        ChildYamlUid = childYaml,
                        ChildPrototype = GetPrototypeId(child),
                        Index = i,
                        OwnerDepth = ownerDepth,
                    });
                }
            }
        }

        patch.Entries.Sort((a, b) =>
        {
            var cmp = a.OwnerDepth.CompareTo(b.OwnerDepth);
            if (cmp != 0)
                return cmp;

            cmp = a.OwnerYamlUid.CompareTo(b.OwnerYamlUid);
            if (cmp != 0)
                return cmp;

            cmp = string.Compare(a.ContainerId, b.ContainerId, StringComparison.Ordinal);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        return patch;
    }

    private int GetContainerOwnerDepth(EntityUid owner)
    {
        var depth = 0;
        var current = owner;
        var guard = 0;

        while (current.IsValid() && guard++ < 256)
        {
            if (_containers.TryGetContainingContainer((current, null, null), out _))
                depth++;

            if (!_xformQuery.TryGetComponent(current, out var xform) || !xform.ParentUid.IsValid())
                break;

            current = xform.ParentUid;
        }

        return depth;
    }

    private string GetPrototypeId(EntityUid uid)
    {
        return _metaQuery.TryGetComponent(uid, out var meta)
            ? meta.EntityPrototype?.ID ?? string.Empty
            : string.Empty;
    }

    private bool IsCompleteSnapshotDirectory(ResPath snapshotDirectory, out string message)
    {
        var required = new[]
        {
            WeeklyModeStore.StationFileName,
            WeeklyModeStore.SnapshotMetadataFileName,
            WeeklyModeStore.RoleOverridesFileName,
            WeeklyModeStore.ContainerPatchFileName,
            WeeklyModeStore.IntegrityFileName,
        };

        foreach (var fileName in required)
        {
            var path = _store.SnapshotFilePath(snapshotDirectory, fileName);
            if (_resource.UserData.Exists(path))
                continue;

            message = $"Snapshot bundle is incomplete: missing {fileName}.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private WeeklySnapshotIntegrity BuildIntegrity(string setId, string snapshotId, DateTime createdAtUtc, ResPath snapshotDirectory)
    {
        var integrity = new WeeklySnapshotIntegrity
        {
            SetId = setId,
            SnapshotId = snapshotId,
            CreatedAtUtc = createdAtUtc,
        };

        foreach (var fileName in new[]
                 {
                     WeeklyModeStore.StationFileName,
                     WeeklyModeStore.SnapshotMetadataFileName,
                     WeeklyModeStore.RoleOverridesFileName,
                     WeeklyModeStore.ContainerPatchFileName,
                 })
        {
            var path = _store.SnapshotFilePath(snapshotDirectory, fileName);
            integrity.Files[fileName] = BuildFileIntegrity(path);
        }

        return integrity;
    }

    private WeeklySnapshotFileIntegrity BuildFileIntegrity(ResPath path)
    {
        var bytes = _resource.UserData.ReadAllBytes(path);
        return new WeeklySnapshotFileIntegrity
        {
            SizeBytes = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
        };
    }
}
