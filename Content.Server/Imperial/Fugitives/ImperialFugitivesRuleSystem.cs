using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fugitives;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Physics;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Fugitives;

/// <summary>Предыстории беглецов (FUGITIVE_BACKSTORY_*).</summary>
public enum ImperialFugitiveBackstory : byte
{
    Prisoner,
    Cultist,
    Waldo,
    Synth,
    Invisible,
}

/// <summary>Отряды охотников (HUNTER_PACK_*).</summary>
public enum ImperialHunterPack : byte
{
    Cops,
    Russian,
    Bounty,
    Psyker,
    Mi13,
}

/// <summary>/datum/dynamic_ruleset/midround/from_ghosts/fugitives.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitivesRuleComponent : Component
{
    /// <summary>Через сколько прилетают охотники (если эвакуацию не вызвали раньше).</summary>
    [DataField]
    public TimeSpan HunterDelay = TimeSpan.FromMinutes(10);

    /// <summary>Принудительная предыстория (админ), иначе случайная.</summary>
    [DataField]
    public ImperialFugitiveBackstory? Backstory;

    [DataField]
    public ImperialHunterPack? Pack;

    [ViewVariables]
    public EntityCoordinates? Spawn;

    [ViewVariables]
    public TimeSpan HuntersAt;

    [ViewVariables]
    public bool HuntersSpawned;

    [ViewVariables]
    public readonly List<EntityUid> Fugitives = new();

    [ViewVariables]
    public readonly List<EntityUid> Hunters = new();
}

/// <summary>Гост-роль беглеца или охотника: что выдать при вселении.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveSpawnerComponent : Component
{
    [DataField]
    public bool Hunter;

    [DataField]
    public ImperialFugitiveBackstory Backstory;

    [DataField]
    public ImperialHunterPack Pack;

    [DataField]
    public ProtoId<StartingGearPrototype>? Gear;

    [DataField]
    public bool Leader;
}

/// <summary>Тело из гост-роли, ещё не получившее разум.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitivePendingComponent : Component
{
    [ViewVariables]
    public bool Hunter;

    [ViewVariables]
    public ImperialFugitiveBackstory Backstory;

    [ViewVariables]
    public ImperialHunterPack Pack;

    [ViewVariables]
    public ProtoId<StartingGearPrototype>? Gear;

    [ViewVariables]
    public bool Leader;
}

/// <summary>/datum/antagonist/fugitive на разуме.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveMindComponent : Component
{
    [ViewVariables]
    public ImperialFugitiveBackstory Backstory;

    /// <summary>is_captured.</summary>
    [ViewVariables]
    public bool Captured;
}

/// <summary>/datum/antagonist/fugitive_hunter на разуме.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveHunterMindComponent : Component
{
    [ViewVariables]
    public ImperialHunterPack Pack;
}

/// <summary>Роль беглеца на разуме.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveRoleComponent : Component;

/// <summary>Роль охотника за беглецами на разуме.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveHunterRoleComponent : Component;

/// <summary>Цель беглеца: «Избегайте поимки охотниками».</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveEvadeConditionComponent : Component;

/// <summary>Цель охотника: поймать беглецов и поместить их в устройство захвата.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveCaptureConditionComponent : Component;

public sealed class ImperialFugitivesRuleSystem : GameRuleSystem<ImperialFugitivesRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStationSpawningSystem _spawning = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private static readonly EntProtoId FugitiveSpawner = "ImperialSpawnPointFugitive";
    private const string FugitiveRole = "MindRoleImperialFugitive";
    private const string HunterRole = "MindRoleImperialFugitiveHunter";
    private static readonly EntProtoId EvadeObjective = "ImperialFugitiveEvadeObjective";
    private static readonly EntProtoId CaptureObjective = "ImperialFugitiveCaptureObjective";

    private static readonly Dictionary<ImperialFugitiveBackstory, ProtoId<StartingGearPrototype>> FugitiveGear = new()
    {
        [ImperialFugitiveBackstory.Prisoner] = "ImperialFugitivePrisonerGear",
        [ImperialFugitiveBackstory.Cultist] = "ImperialFugitiveYalpCultistGear",
        [ImperialFugitiveBackstory.Waldo] = "ImperialFugitiveWaldoGear",
        [ImperialFugitiveBackstory.Synth] = "ImperialFugitiveSynthGear",
        [ImperialFugitiveBackstory.Invisible] = "ImperialFugitiveInvisibleGear",
    };

    private static readonly Dictionary<ImperialHunterPack, ResPath> Ships = new()
    {
        [ImperialHunterPack.Cops] = new("/Maps/Imperial/Maps/Fugitives/hunter_space_cop.yml"),
        [ImperialHunterPack.Russian] = new("/Maps/Imperial/Maps/Fugitives/hunter_russian.yml"),
        [ImperialHunterPack.Bounty] = new("/Maps/Imperial/Maps/Fugitives/hunter_bounty.yml"),
        [ImperialHunterPack.Psyker] = new("/Maps/Imperial/Maps/Fugitives/hunter_psyker.yml"),
        [ImperialHunterPack.Mi13] = new("/Maps/Imperial/Maps/Fugitives/hunter_mi13_foodtruck.yml"),
    };

    private static readonly string[] MaintenanceTiles = { "Plating", "FloorTechMaint", "FloorTechMaint2", "FloorTechMaint3" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFugitivePendingComponent, GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
        SubscribeLocalEvent<ImperialFugitivePendingComponent, MindAddedMessage>(OnPendingMind);
        SubscribeLocalEvent<ImperialFugitiveEvadeConditionComponent, ObjectiveGetProgressEvent>(OnEvadeProgress);
        SubscribeLocalEvent<ImperialFugitiveCaptureConditionComponent, ObjectiveGetProgressEvent>(OnCaptureProgress);
    }

    #region Беглецы

    /// <summary>execute(): спавн 1 или 3–4 беглецов в техтоннелях.</summary>
    protected override void Started(EntityUid uid, ImperialFugitivesRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.Spawn = FindMaintenanceSpawn();
        if (component.Spawn is not { } spawn)
        {
            Log.Warning("Беглецы: не найдено место в техтоннелях, правило завершено");
            ForceEndSelf(uid, gameRule);
            return;
        }

        // collect_candidates(): иногда беглец один.
        var count = _random.Prob(0.25f) ? 1 : _random.Next(3, 5);
        component.Backstory ??= count == 1
            ? _random.Pick(new[] { ImperialFugitiveBackstory.Invisible, ImperialFugitiveBackstory.Waldo })
            : _random.Pick(new[] { ImperialFugitiveBackstory.Cultist, ImperialFugitiveBackstory.Prisoner, ImperialFugitiveBackstory.Synth });
        if (component.Backstory is ImperialFugitiveBackstory.Invisible or ImperialFugitiveBackstory.Waldo)
            count = 1;

        component.Pack ??= _random.Pick(Enum.GetValues<ImperialHunterPack>());
        component.HuntersAt = _timing.CurTime + component.HunterDelay;

        for (var i = 0; i < count; i++)
        {
            var spawner = Spawn(FugitiveSpawner, spawn);
            var data = EnsureComp<ImperialFugitiveSpawnerComponent>(spawner);
            data.Backstory = component.Backstory.Value;
            data.Gear = FugitiveGear[data.Backstory];
            data.Leader = i == 0;
        }

        Log.Info($"Беглецы: {count} × {component.Backstory}, охотники {component.Pack} через {component.HunterDelay}");
    }

    /// <summary>find_maintenance_spawn(atmos_sensitive = TRUE): тёплая клетка техтоннелей с воздухом.</summary>
    public EntityCoordinates? FindMaintenanceSpawn()
    {
        var stations = _station.GetStations();
        if (stations.Count == 0)
            return null;

        var station = _random.Pick(stations);
        if (_station.GetLargestGrid(station) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var candidates = new List<TileRef>();
        foreach (var tile in _map.GetAllTiles(gridUid, grid))
        {
            if (MaintenanceTiles.Contains(_tileDefs[tile.Tile.TypeId].ID))
                candidates.Add(tile);
        }

        _random.Shuffle(candidates);
        foreach (var tile in candidates.Take(500))
        {
            if (_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                continue;

            var air = _atmos.GetTileMixture(gridUid, null, tile.GridIndices);
            if (air == null || air.Pressure < 80f || air.Pressure > 130f || air.Temperature < 270f || air.Temperature > 330f)
                continue;

            return _map.GridTileToLocal(gridUid, grid, tile.GridIndices);
        }

        return null;
    }

    private void OnSpawnerUsed(Entity<ImperialFugitivePendingComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<ImperialFugitiveSpawnerComponent>(args.Spawner, out var data))
            return;

        ent.Comp.Hunter = data.Hunter;
        ent.Comp.Backstory = data.Backstory;
        ent.Comp.Pack = data.Pack;
        ent.Comp.Gear = data.Gear;
        ent.Comp.Leader = data.Leader;
    }

    private void OnPendingMind(Entity<ImperialFugitivePendingComponent> ent, ref MindAddedMessage args)
    {
        var mindId = args.Mind.Owner;
        var mind = args.Mind.Comp;
        var pending = ent.Comp;
        RemCompDeferred<ImperialFugitivePendingComponent>(ent);

        if (pending.Gear is { } gear && _proto.TryIndex(gear, out var gearProto))
            _spawning.EquipStartingGear(ent, gearProto);

        if (pending.Hunter)
            MakeHunter(ent, mindId, mind, pending.Pack);
        else
            MakeFugitive(ent, mindId, mind, pending.Backstory, pending.Leader);
    }

    /// <summary>equip_fugitive() + equip_fugitive_leader() + on_gain().</summary>
    private void MakeFugitive(EntityUid body, EntityUid mindId, MindComponent mind, ImperialFugitiveBackstory backstory, bool leader)
    {
        var fugitive = EnsureComp<ImperialFugitiveMindComponent>(mindId);
        fugitive.Backstory = backstory;
        if (!_role.MindHasRole<ImperialFugitiveRoleComponent>(mindId))
            _role.MindAddRole(mindId, FugitiveRole, mind, silent: true);

        switch (backstory)
        {
            case ImperialFugitiveBackstory.Prisoner:
                // «NTP #CC-0xxx», как заключённые лаваленда, только с ЦК.
                Rename(body, mindId, $"NTP #CC-0{_random.Next(111, 1000)}");
                break;
            case ImperialFugitiveBackstory.Waldo:
                Rename(body, mindId, Loc.GetString("imperial-fugitive-waldo-name"));
                foreach (var slot in new[] { "shoes", "jumpsuit", "outerClothing", "head", "eyes" })
                {
                    if (_inventory.TryGetSlotEntity(body, slot, out var item))
                        EnsureComp<UnremoveableComponent>(item.Value);
                }

                EntityManager.System<ImperialFugitiveGearSystem>().GrantKnock(body);
                break;
            case ImperialFugitiveBackstory.Invisible:
                EntityManager.System<ImperialFugitiveGearSystem>().GrantCamouflage(body);
                break;
        }

        if (leader)
        {
            var toolbox = Spawn("ToolboxMechanicalFilled", Transform(body).Coordinates);
            _hands.TryPickupAnyHand(body, toolbox);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/emitter.ogg"), body);
        if (_objectives.TryCreateObjective(mindId, mind, EvadeObjective) is { } objective)
            _mind.AddObjective(mindId, mind, objective);

        if (GetRule() is { } rule)
            rule.Comp.Fugitives.Add(mindId);

        var key = backstory.ToString().ToLowerInvariant();
        Greet(mind, Loc.GetString($"imperial-fugitive-greeting-{key}") + "\n" + Loc.GetString("imperial-fugitive-not-antag"));
    }

    private void Rename(EntityUid body, EntityUid mindId, string name)
    {
        _meta.SetEntityName(body, name);
        if (TryComp<MindComponent>(mindId, out var mind))
            mind.CharacterName = name;
    }

    #endregion

    #region Охотники

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ImperialFugitivesRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule) || rule.HuntersSpawned || rule.Spawn == null)
                continue;

            // check_spawn_hunters(): эвакуацию вызвали — охотники прилетают сразу.
            if (_timing.CurTime < rule.HuntersAt && _roundEnd.ExpectedCountdownEnd == null)
                continue;

            rule.HuntersSpawned = true;
            SpawnHunters((uid, rule));
        }
    }

    /// <summary>spawn_hunters(): корабль отряда в космосе рядом со станцией и объявление.</summary>
    public void SpawnHunters(Entity<ImperialFugitivesRuleComponent> rule)
    {
        var pack = rule.Comp.Pack ?? ImperialHunterPack.Cops;
        var stations = _station.GetStations();
        if (stations.Count == 0 || _station.GetLargestGrid(_random.Pick(stations)) is not { } stationGrid)
            return;

        var stationXform = Transform(stationGrid);
        var center = _transform.GetWorldPosition(stationXform);
        var offset = center + _random.NextAngle().ToVec() * _random.NextFloat(180f, 240f);
        if (!_loader.TryLoadGrid(stationXform.MapID, Ships[pack], out var ship, offset: offset))
        {
            Log.Error($"Беглецы: не удалось загрузить корабль охотников {pack}");
            return;
        }

        _meta.SetEntityName(ship.Value, Loc.GetString($"imperial-hunter-ship-{pack.ToString().ToLowerInvariant()}"));
        var key = pack.ToString().ToLowerInvariant();
        _chat.DispatchGlobalAnnouncement(Loc.GetString($"imperial-hunter-announce-{key}"),
            Loc.GetString($"imperial-hunter-announce-{key}-title"), true, null, Color.Gold);
    }

    /// <summary>fugitive_hunter/on_gain() + greet().</summary>
    private void MakeHunter(EntityUid body, EntityUid mindId, MindComponent mind, ImperialHunterPack pack)
    {
        EnsureComp<ImperialFugitiveHunterMindComponent>(mindId).Pack = pack;
        if (!_role.MindHasRole<ImperialFugitiveHunterRoleComponent>(mindId))
            _role.MindAddRole(mindId, HunterRole, mind, silent: true);

        if (_objectives.TryCreateObjective(mindId, mind, CaptureObjective) is { } objective)
            _mind.AddObjective(mindId, mind, objective);

        EntityManager.System<ImperialFugitiveGearSystem>().EquipHunter(body, pack);
        if (GetRule() is { } rule)
            rule.Comp.Hunters.Add(mindId);

        var key = pack.ToString().ToLowerInvariant();
        Greet(mind, Loc.GetString($"imperial-hunter-greeting-{key}") + "\n" + Loc.GetString("imperial-hunter-not-antag"));
    }

    #endregion

    #region Цели и итоги

    private Entity<ImperialFugitivesRuleComponent>? GetRule()
    {
        var query = EntityQueryEnumerator<ImperialFugitivesRuleComponent>();
        return query.MoveNext(out var uid, out var comp) ? (uid, comp) : null;
    }

    private void OnEvadeProgress(Entity<ImperialFugitiveEvadeConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = TryComp<ImperialFugitiveMindComponent>(args.MindId, out var fugitive) && fugitive.Captured ? 0f : 1f;
    }

    private void OnCaptureProgress(Entity<ImperialFugitiveCaptureConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var (total, _, captured) = CountFugitives();
        args.Progress = total == 0 ? 0f : (float) captured / total;
    }

    private (int Total, int Dead, int Captured) CountFugitives()
    {
        int total = 0, dead = 0, captured = 0;
        var query = EntityQueryEnumerator<ImperialFugitiveMindComponent, MindComponent>();
        while (query.MoveNext(out _, out var fugitive, out var mind))
        {
            total++;
            if (fugitive.Captured)
                captured++;
            if (fugitive.Captured || mind.OwnedEntity is not { } body || _mobs.IsDead(body))
                dead++;
        }

        return (total, dead, captured);
    }

    /// <summary>fugitive_hunters/get_result().</summary>
    private string GetResult(Entity<ImperialFugitivesRuleComponent> rule)
    {
        var (total, dead, captured) = CountFugitives();
        if (total == 0)
            return "prank";

        var huntersDead = rule.Comp.Hunters.Count > 0 && rule.Comp.Hunters.All(h =>
            !TryComp<MindComponent>(h, out var m) || m.OwnedEntity is not { } b || _mobs.IsDead(b));
        var deadNotCaptured = dead - captured;

        if (captured > 0)
        {
            if (captured == total)
            {
                if (deadNotCaptured == 0 && !AnyCapturedDead())
                    return "badass-hunter";
                return huntersDead ? "postmortem-hunter" : "major-hunter";
            }

            return huntersDead ? "minor-hunter" : "hunter-victory";
        }

        if (dead == 0)
            return "major-fugitive";
        if (dead < total)
            return "fugitive-victory";
        return huntersDead ? "stalemate" : "minor-fugitive";
    }

    private bool AnyCapturedDead()
    {
        var query = EntityQueryEnumerator<ImperialFugitiveMindComponent, MindComponent>();
        while (query.MoveNext(out _, out var fugitive, out var mind))
        {
            if (fugitive.Captured && mind.OwnedEntity is { } body && _mobs.IsDead(body))
                return true;
        }

        return false;
    }

    protected override void AppendRoundEndText(EntityUid uid, ImperialFugitivesRuleComponent component, GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        if (component.Fugitives.Count == 0)
            return;

        args.AddLine(Loc.GetString("imperial-fugitive-roundend-fugitives", ("count", component.Fugitives.Count)));
        foreach (var mindId in component.Fugitives)
        {
            if (TryComp<MindComponent>(mindId, out var mind))
                args.AddLine($"  {mind.CharacterName}");
        }

        if (component.Hunters.Count == 0)
            return;

        var pack = Loc.GetString($"imperial-hunter-pack-{(component.Pack ?? ImperialHunterPack.Cops).ToString().ToLowerInvariant()}");
        args.AddLine(Loc.GetString("imperial-fugitive-roundend-hunters", ("count", component.Hunters.Count), ("pack", pack)));
        foreach (var mindId in component.Hunters)
        {
            if (TryComp<MindComponent>(mindId, out var mind))
                args.AddLine($"  {mind.CharacterName}");
        }

        var result = GetResult((uid, component));
        args.AddLine(Loc.GetString($"imperial-fugitive-result-{result}", ("pack", pack)));
    }

    private void Greet(MindComponent mind, string message)
    {
        if (mind.UserId is { } userId && _players.TryGetSessionById(userId, out var session))
            _chatManager.DispatchServerMessage(session, message, true);
    }

    #endregion
}
