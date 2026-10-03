using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fission;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Content.Shared.Weather;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fission;

/// <summary>
/// Ядерный реактор газового охлаждения (NGCR) из SS220 Paradise: code/modules/power/engines/fission.
/// Логика переписана на C#: ядро, камеры со стержнями, газовые узлы, силовой терминал, консоль,
/// центрифуга обогащения, фабрикатор стержней, расплав, ремонт и центкомовская перегрузка.
/// </summary>
public sealed partial class FissionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AmbientSoundSystem _ambient = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly Content.Server.Radiation.Systems.RadiationSystem _radiation = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string ScrewingQuality = "Screwing";
    private const string AnchoringQuality = "Anchoring";
    private const string PryingQuality = "Prying";
    private const string WeldingQuality = "Welding";
    private const string PulsingQuality = "Pulsing";

    private const string EngineeringChannel = "Engineering";
    private const string CommonChannel = "Common";

    private static readonly Vector2i[] CardinalOffsets =
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0),
    };

    public override void Initialize()
    {
        base.Initialize();

        InitializeReactor();
        InitializeChambers();
        InitializeMachines();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var reactors = EntityQueryEnumerator<FissionReactorComponent>();
        while (reactors.MoveNext(out var uid, out var reactor))
        {
            var ent = (uid, reactor);

            if (reactor.FinalCountdown && now >= reactor.NextCountdownStep)
                CountdownStep(ent);

            if (reactor.OverloadPrepAt is { } prep && now >= prep)
            {
                reactor.OverloadPrepAt = null;
                PrepOverload(ent);
            }

            if (reactor.OverloadDetonateAt is { } detonate && now >= detonate)
            {
                reactor.OverloadDetonateAt = null;
                DetonateOverload(ent);
            }

            if (reactor.Venting && !reactor.AdminIntervention && !reactor.Broken && now >= reactor.NextVent)
            {
                reactor.NextVent = now + FissionReactorComponent.ProcessInterval;
                ProcessVenting(ent);
            }

            if (now < reactor.NextProcess)
                continue;

            reactor.NextProcess = now + FissionReactorComponent.ProcessInterval;
            RebuildNetwork(ent);
            foreach (var chamber in reactor.ConnectedChambers)
            {
                if (TryComp<FissionChamberComponent>(chamber, out var chamberComp))
                    ProcessChamber((chamber, chamberComp), reactor);
            }

            ProcessReactor(ent);
            UpdateReactorVisuals(ent);
        }

        UpdateChambers(now);
        UpdateMachines(frameTime, now);
    }

    #region Клетки и поиск

    private bool TryGetTile(EntityUid uid, [NotNullWhen(true)] out Entity<MapGridComponent>? grid, out Vector2i tile)
    {
        grid = null;
        tile = default;
        var xform = Transform(uid);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = (gridUid, gridComp);
        tile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        return true;
    }

    private bool TryGetChamberAt(Entity<MapGridComponent> grid, Vector2i tile, out Entity<FissionChamberComponent> chamber)
    {
        foreach (var anchored in _map.GetAnchoredEntities(grid, tile))
        {
            if (!TryComp<FissionChamberComponent>(anchored, out var comp))
                continue;

            chamber = (anchored, comp);
            return true;
        }

        chamber = default;
        return false;
    }

    /// <summary>Реактор, чей корпус 3×3 занимает клетку.</summary>
    private bool TryGetReactorAt(Entity<MapGridComponent> grid, Vector2i tile, out Entity<FissionReactorComponent> reactor)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                foreach (var anchored in _map.GetAnchoredEntities(grid, tile + new Vector2i(dx, dy)))
                {
                    if (!TryComp<FissionReactorComponent>(anchored, out var comp))
                        continue;

                    reactor = (anchored, comp);
                    return true;
                }
            }
        }

        reactor = default;
        return false;
    }

    /// <summary>
    /// build_reactor_network + find_link + get_neighbors: камеры, примыкающие к корпусу реактора,
    /// и все камеры, связанные с ними по сторонам.
    /// </summary>
    private void RebuildNetwork(Entity<FissionReactorComponent> reactor)
    {
        foreach (var old in reactor.Comp.ConnectedChambers)
        {
            if (!TryComp<FissionChamberComponent>(old, out var oldComp) || oldComp.LinkedReactor != reactor.Owner)
                continue;

            oldComp.LinkedReactor = null;
            oldComp.Neighbors.Clear();
        }

        reactor.Comp.ConnectedChambers.Clear();
        if (reactor.Comp.Broken || !TryGetTile(reactor, out var grid, out var center))
            return;

        var queue = new Queue<Vector2i>();
        var tiles = new Dictionary<Vector2i, EntityUid>();
        for (var i = -1; i <= 1; i++)
        {
            queue.Enqueue(center + new Vector2i(i, 2));
            queue.Enqueue(center + new Vector2i(i, -2));
            queue.Enqueue(center + new Vector2i(2, i));
            queue.Enqueue(center + new Vector2i(-2, i));
        }

        while (queue.TryDequeue(out var tile))
        {
            if (tiles.ContainsKey(tile) || !TryGetChamberAt(grid.Value, tile, out var chamber))
                continue;

            // Камера уже принадлежит другому реактору.
            if (chamber.Comp.LinkedReactor is { } other && other != reactor.Owner && Exists(other))
                continue;

            tiles[tile] = chamber;
            chamber.Comp.LinkedReactor = reactor;
            chamber.Comp.Neighbors.Clear();
            reactor.Comp.ConnectedChambers.Add(chamber);

            foreach (var offset in CardinalOffsets)
                queue.Enqueue(tile + offset);
        }

        foreach (var (tile, chamber) in tiles)
        {
            var comp = Comp<FissionChamberComponent>(chamber);
            foreach (var offset in CardinalOffsets)
            {
                if (tiles.TryGetValue(tile + offset, out var neighbor))
                    comp.Neighbors.Add(neighbor);
            }
        }
    }

    #endregion

    #region Объявления

    /// <summary>radio_announce: инженерный канал, либо общий при null-частоте.</summary>
    private void Announce(EntityUid source, string message, bool common = false)
    {
        _radio.SendRadioMessage(source, message, common ? CommonChannel : EngineeringChannel, source, escapeMarkup: false);
    }

    private void PlayGlobalOnMap(EntityUid source, Robust.Shared.Audio.SoundSpecifier sound, string? message = null)
    {
        var map = Transform(source).MapID;
        var filter = Filter.Empty().AddWhere(session =>
            session.AttachedEntity is { } ent && Transform(ent).MapID == map);
        _audio.PlayGlobal(sound, filter, true);

        if (message == null)
            return;

        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } ent)
                _popup.PopupEntity(message, ent, ent, PopupType.LargeCaution);
        }
    }

    #endregion

    private void SpawnRadiationPulse(EntityUid source, float intensity)
    {
        if (TerminatingOrDeleted(source))
            return;

        var pulse = Spawn(FissionReactorProtoDefaults.RadiationPulse, _xform.GetMapCoordinates(source));
        _radiation.SetIntensity(pulse, intensity);
    }

    private void SpawnSmoke(EntityUid source)
    {
        var coords = _xform.GetMapCoordinates(source);
        Spawn(FissionReactorProtoDefaults.Smoke, coords);
        foreach (var offset in CardinalOffsets)
            Spawn(FissionReactorProtoDefaults.Smoke, coords.Offset(offset));
    }
}

internal static class FissionReactorProtoDefaults
{
    public static readonly EntProtoId RadiationPulse = "ImperialFissionRadiationPulse";
    public static readonly EntProtoId Smoke = "ImperialFissionSmoke";
}
