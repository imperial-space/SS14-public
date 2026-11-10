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
    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
        {
            _sawmill.Error($"Weekly map selection failed: set '{_state.ActiveSetId}' is missing.");
            throw new InvalidOperationException($"Weekly map selection failed: set '{_state.ActiveSetId}' is missing.");
        }

        ClearActiveWeeklyMapTracking();
        var previousMap = GetCurrentOrConfiguredMapName();
        var snapshotId = _state.PendingSnapshotId ?? set.CurrentSnapshot;
        if (snapshotId != null)
        {
            if (!TryLoadCompatibleSnapshot(set, snapshotId, out _, out var validationMessage))
                throw new InvalidOperationException($"Weekly snapshot selection failed: {validationMessage}");

            var stationPath = _store.SnapshotStationPath(set.SetId, snapshotId);
            _gameMapManager.SelectPersistentMap(set.BaseMapPrototype, stationPath);
            _state.ActiveSnapshotId = snapshotId;
            _state.PendingSnapshotId = null;
            _store.SaveState(_state);
            _sawmill.Info(
                "Weekly snapshot start:\n" +
                $"set={set.SetId}\n" +
                $"snapshot={snapshotId}\n" +
                $"stationPath={stationPath}\n" +
                $"previousMap={previousMap}");
        }
        else
        {
            if (TryGetConfiguredBaseMapPath(set, out var baseMapPath))
                _gameMapManager.SelectMapPath(set.BaseMapPrototype, baseMapPath);
            else
                _gameMapManager.SelectMap(set.BaseMapPrototype);

            _state.ActiveSnapshotId = null;
            _state.PendingSnapshotId = null;
            _store.SaveState(_state);
            _sawmill.Info(
                "Weekly first start:\n" +
                $"set={set.SetId}\n" +
                $"baseMapPrototype={set.BaseMapPrototype}\n" +
                $"baseMapPath={set.BaseMapPath}\n" +
                "source=BaseMap\n" +
                $"previousMap={previousMap}");
        }

        var selected = _gameMapManager.GetSelectedMap();
        if (selected == null)
            throw new InvalidOperationException($"Weekly map selection failed: base map '{set.BaseMapPrototype}' could not be selected.");

        if (!string.Equals(selected.ID, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Weekly map selection failed: selected map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {selected.ID}");
        }

        ev.Maps.Clear();
        ev.Maps.Add(selected);
    }

    private void OnPreGameMapLoad(PreGameMapLoad ev)
    {
        if (!_enabled ||
            !_state.IsActive ||
            _state.ActiveSetId == null ||
            (_state.PendingSnapshotId ?? _state.ActiveSnapshotId) == null)
        {
            return;
        }

        ev.Options = ev.Options with { StoreYamlUids = true };
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        var (mapEntity, gridUids) = TrackActiveWeeklyMap(set, ev);
        ApplyWeeklyResearchOverlay(set);

        if (_state.ActiveSnapshotId == null)
            return;

        SuppressSnapshotMapInitOnlyComponents(ev.Map, mapEntity, gridUids);
        SuppressSnapshotStartingItems(ev.Map, mapEntity, gridUids);

        if (!_store.TryLoadContainerPatch(_state.ActiveSetId, _state.ActiveSnapshotId, out var patch))
        {
            _sawmill.Warning($"Weekly snapshot '{_state.ActiveSnapshotId}' has no readable container patch.");
            return;
        }

        ApplyContainerPatch(ev.Map, mapEntity, gridUids, patch, _state.ActiveSetId, _state.ActiveSnapshotId);
        DeactivateLoadedSnapshotBorgs(mapEntity, gridUids);
    }

    private void OnStationInitialized(StationInitializedEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        ApplyRoleOverridesToStation(ev.Station, set);
        ApplyWeeklyResearchOverlay(set);
    }

    private void OnTechnologyDatabaseStartup(EntityUid uid, TechnologyDatabaseComponent component, ComponentStartup args)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
            return;

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
            return;

        _research.SetWeeklyModeOverlay(uid, true, BuildWeeklyResearchData(set), true, component);
        RaiseLocalEvent(new WeeklyRecipesChangedEvent());
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_enabled || !_state.IsActive || _state.ActiveSetId == null)
        {
            _nextAutosaveAt = null;
            _autosaveWarningIssued = false;
            return;
        }

        if (!_store.TryLoadSet(_state.ActiveSetId, out var set))
        {
            _nextAutosaveAt = null;
            _autosaveWarningIssued = false;
            return;
        }

        _nextAutosaveAt = _timing.CurTime + TimeSpan.FromMinutes(Math.Max(1, set.AutosaveMinutes));
        _autosaveWarningIssued = false;
    }

    private (EntityUid MapEntity, List<EntityUid> GridUids) TrackActiveWeeklyMap(WeeklyModeSet set, PostGameMapLoad ev)
    {
        if (!string.Equals(ev.GameMap.ID, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Weekly map load failed: loaded map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {ev.GameMap.ID}");
        }

        var mapEntity = _map.GetMapOrInvalid(ev.Map);
        if (!mapEntity.IsValid() ||
            !_metaQuery.TryGetComponent(mapEntity, out var meta) ||
            meta.EntityLifeStage >= EntityLifeStage.Terminating)
        {
            throw new InvalidOperationException($"Weekly map load failed: loaded map {ev.Map} has no live map entity.");
        }

        var mapName = string.IsNullOrWhiteSpace(meta.EntityName)
            ? ev.GameMap.MapName
            : meta.EntityName;

        if (IsDevMapMismatch(set.BaseMapPrototype, mapName))
        {
            throw new InvalidOperationException(
                "Weekly map load failed: active map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {mapName}");
        }

        var gridUids = CollectGridUids(ev.Map);
        foreach (var uid in ev.Grids)
        {
            if (uid.IsValid() && !gridUids.Contains(uid))
                gridUids.Add(uid);
        }

        _activeWeeklyMapId = ev.Map;
        _activeWeeklyMapEntity = mapEntity;
        _activeWeeklyGridIds.Clear();
        _activeWeeklyGridIds.AddRange(gridUids);
        _activeWeeklyMapPrototype = set.BaseMapPrototype;
        _activeWeeklyMapName = mapName;

        _state.ActiveWeeklyMapId = (int) ev.Map;
        _state.ActiveWeeklyMapEntity = mapEntity.Id;
        _state.ActiveWeeklyGridIds = gridUids.Select(uid => uid.Id).OrderBy(id => id).ToList();
        _state.ActiveWeeklyMapPrototype = set.BaseMapPrototype;
        _state.ActiveWeeklyMapName = mapName;
        _store.SaveState(_state);

        var entityCount = CountEntitiesOnMap(ev.Map, mapEntity, gridUids);
        var prefix = _state.ActiveSnapshotId == null
            ? "Weekly first start loaded:"
            : "Weekly snapshot loaded:";

        _sawmill.Info(
            $"{prefix}\n" +
            $"newMapId={(int) ev.Map}\n" +
            $"mapEntity={mapEntity.Id}\n" +
                $"mapName={mapName}\n" +
                $"grids={string.Join(",", gridUids.Select(uid => uid.Id))}\n" +
                $"entities={entityCount}");

        return (mapEntity, gridUids);
    }

    private bool TryResolveActiveWeeklyMap(
        WeeklyModeSet set,
        out MapId mapId,
        out EntityUid mapEntity,
        out string mapName,
        out List<EntityUid> gridUids,
        out int entityCount,
        out string message)
    {
        mapId = MapId.Nullspace;
        mapEntity = EntityUid.Invalid;
        mapName = string.Empty;
        gridUids = new List<EntityUid>();
        entityCount = 0;

        if (_activeWeeklyMapId == null && _state.ActiveWeeklyMapId is { } persistedMapId)
            _activeWeeklyMapId = new MapId(persistedMapId);

        if (_activeWeeklyMapId == null || _activeWeeklyMapId.Value == MapId.Nullspace)
        {
            message = "Weekly save aborted: no active Weekly map has been loaded for this round.";
            return false;
        }

        mapId = _activeWeeklyMapId.Value;
        if (!_map.MapExists(mapId))
        {
            message = $"Weekly save aborted: active Weekly map {mapId} no longer exists.";
            return false;
        }

        mapEntity = _activeWeeklyMapEntity ?? _map.GetMapOrInvalid(mapId);
        if (!mapEntity.IsValid() ||
            !_metaQuery.TryGetComponent(mapEntity, out var meta) ||
            meta.EntityLifeStage >= EntityLifeStage.Terminating)
        {
            message = $"Weekly save aborted: active Weekly map {mapId} is terminating or has no live map entity.";
            return false;
        }

        var activePrototype = _activeWeeklyMapPrototype ?? _state.ActiveWeeklyMapPrototype;
        if (!string.Equals(activePrototype, set.BaseMapPrototype, StringComparison.Ordinal))
        {
            message =
                "Weekly save aborted:\n" +
                "active map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {activePrototype ?? "<unknown>"}";
            return false;
        }

        mapName = string.IsNullOrWhiteSpace(meta.EntityName)
            ? _activeWeeklyMapName ?? _state.ActiveWeeklyMapName ?? mapId.ToString()
            : meta.EntityName;

        if (IsDevMapMismatch(set.BaseMapPrototype, mapName))
        {
            message =
                "Weekly save aborted:\n" +
                "active map does not match the map assigned to this Weekly set.\n" +
                $"Expected: {set.BaseMapPrototype}\n" +
                $"Actual: {mapName}";
            return false;
        }

        gridUids = CollectGridUids(mapId);
        if (gridUids.Count == 0 && _activeWeeklyGridIds.Count > 0)
            gridUids.AddRange(_activeWeeklyGridIds.Where(uid => uid.IsValid()));

        entityCount = CountEntitiesOnMap(mapId, mapEntity, gridUids);
        message = string.Empty;
        return true;
    }
}
