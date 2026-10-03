using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Lightning;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chat;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Radiation.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>
/// Термоядерный реактор «Гиперторус» (HFR) из SS13: code/modules/atmospherics/machinery/components/fusion.
/// Здесь — сборка 3×3 из коробок, проверка раскладки, инструменты и активация интерфейсом.
/// </summary>
public sealed partial class HypertorusSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AmbientSoundSystem _ambient = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly Content.Shared.Power.EntitySystems.SharedBatterySystem _battery = default!;
    [Dependency] private readonly Content.Server.Radiation.Systems.RadiationSystem _radiation = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string ScrewingQuality = "Screwing";
    private const string AnchoringQuality = "Anchoring";
    private const string PryingQuality = "Prying";
    private const string WeldingQuality = "Welding";
    private const string PulsingQuality = "Pulsing";

    private static readonly EntProtoId GuidePaper = "ImperialHypertorusGuide";

    private static readonly (Vector2i Offset, Direction Dir)[] Cardinals =
    {
        (new Vector2i(0, 1), Direction.North),
        (new Vector2i(1, 0), Direction.East),
        (new Vector2i(0, -1), Direction.South),
        (new Vector2i(-1, 0), Direction.West),
    };

    /// <summary>Углы и направление, в которое должен смотреть угол (check_part_connectivity).</summary>
    private static readonly (Vector2i Offset, Direction Dir)[] Diagonals =
    {
        (new Vector2i(1, 1), Direction.East),
        (new Vector2i(1, -1), Direction.South),
        (new Vector2i(-1, -1), Direction.West),
        (new Vector2i(-1, 1), Direction.North),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HypertorusCoreComponent, AtmosDeviceUpdateEvent>(OnCoreAtmosUpdate);
        SubscribeLocalEvent<HypertorusCoreComponent, ComponentShutdown>(OnCoreShutdown);
        SubscribeLocalEvent<HypertorusCoreComponent, EmpPulseEvent>(OnCoreEmp);
        SubscribeLocalEvent<HypertorusCoreComponent, ExaminedEvent>(OnCoreExamined);
        SubscribeLocalEvent<HypertorusCoreComponent, InteractUsingEvent>(OnCoreInteractUsing);

        SubscribeLocalEvent<HypertorusPartComponent, InteractUsingEvent>(OnPartInteractUsing);
        SubscribeLocalEvent<HypertorusPartComponent, HypertorusRepairDoAfterEvent>(OnPartRepaired);
        SubscribeLocalEvent<HypertorusPartComponent, ExaminedEvent>(OnPartExamined);
        SubscribeLocalEvent<HypertorusPartComponent, ActivatableUIOpenAttemptEvent>(OnInterfaceOpenAttempt);

        SubscribeLocalEvent<HypertorusBoxComponent, InteractUsingEvent>(OnBoxInteractUsing);

        InitializeUi();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HypertorusCoreComponent>();
        while (query.MoveNext(out var uid, out var core))
        {
            if (core.FinalCountdown && now >= core.NextCountdownStep)
                CountdownStep((uid, core));
        }
    }

    private void PlayToolSound(EntityUid used, EntityUid user)
    {
        if (TryComp<Content.Shared.Tools.Components.ToolComponent>(used, out var tool))
            _tool.PlayToolSound(used, tool, user);
    }

    #region Сборка из коробок

    /// <summary>hfr_box/core/multitool_act: коробки 3×3 вокруг ядра разворачиваются в реактор.</summary>
    private void OnBoxInteractUsing(Entity<HypertorusBoxComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.BoxType != HypertorusBoxType.Core || !_tool.HasQuality(args.Used, PulsingQuality))
            return;

        args.Handled = true;
        if (!TryGetTile(ent, out var grid, out var tile))
        {
            _popup.PopupEntity(Loc.GetString("hypertorus-box-no-grid"), ent, args.User);
            return;
        }

        var parts = new List<(EntityUid Box, HypertorusBoxComponent Comp, Direction Dir, Vector2i Tile)>();
        foreach (var (offset, dir) in Cardinals)
        {
            if (FindBox(grid, tile + offset, HypertorusBoxType.Body) is { } box)
                parts.Add((box.Owner, box.Comp, dir, tile + offset));
        }

        foreach (var (offset, dir) in Diagonals)
        {
            if (FindBox(grid, tile + offset, HypertorusBoxType.Corner) is { } box)
                parts.Add((box.Owner, box.Comp, dir, tile + offset));
        }

        if (parts.Count != 8)
        {
            _popup.PopupEntity(Loc.GetString("hypertorus-box-incomplete"), ent, args.User);
            return;
        }

        foreach (var (box, comp, dir, partTile) in parts)
        {
            SpawnPart(comp.Part, grid, partTile, dir);
            QueueDel(box);
        }

        SpawnPart(ent.Comp.Part, grid, tile, Direction.South);
        QueueDel(ent);
        _popup.PopupEntity(Loc.GetString("hypertorus-box-built"), args.User, args.User);
    }

    private Entity<HypertorusBoxComponent>? FindBox(Entity<MapGridComponent> grid, Vector2i tile, HypertorusBoxType type)
    {
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(grid, tile, 0.1f, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (TryComp<HypertorusBoxComponent>(uid, out var box) && box.BoxType == type)
                return (uid, box);
        }

        return null;
    }

    private EntityUid SpawnPart(EntProtoId proto, Entity<MapGridComponent> grid, Vector2i tile, Direction dir)
    {
        var part = Spawn(proto, _map.GridTileToLocal(grid, grid, tile));
        _xform.SetLocalRotation(part, dir.ToAngle());
        if (!Transform(part).Anchored)
            _xform.AnchorEntity(part);
        return part;
    }

    private bool TryGetTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        grid = default;
        tile = default;
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = (gridUid, gridComp);
        tile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        return true;
    }

    #endregion

    #region Раскладка и активация

    /// <summary>
    /// check_part_connectivity: вокруг ядра — четыре стороны, смотрящие наружу (интерфейс и три порта),
    /// и четыре угла в своих направлениях; ни у кого не открыта панель.
    /// </summary>
    private bool CheckPartConnectivity(Entity<HypertorusCoreComponent> core)
    {
        if (!Transform(core).Anchored || TryComp<HypertorusCorePanelComponent>(core, out var panel) && panel.PanelOpen)
            return false;

        if (!TryGetTile(core, out var grid, out var tile))
            return false;

        EntityUid? iface = null, fuel = null, moderator = null, waste = null;
        var corners = new List<EntityUid>();

        foreach (var (offset, dir) in Cardinals)
        {
            if (FindPart(grid, tile + offset) is not { } part || part.Comp.PartType == HypertorusPartType.Corner
                || part.Comp.PanelOpen || GetDir(part) != dir)
            {
                return false;
            }

            ref var slot = ref iface;
            switch (part.Comp.PartType)
            {
                case HypertorusPartType.Interface:
                    slot = ref iface;
                    break;
                case HypertorusPartType.FuelInput:
                    slot = ref fuel;
                    break;
                case HypertorusPartType.ModeratorInput:
                    slot = ref moderator;
                    break;
                default:
                    slot = ref waste;
                    break;
            }

            if (slot != null)
                return false;

            slot = part.Owner;
        }

        foreach (var (offset, dir) in Diagonals)
        {
            if (FindPart(grid, tile + offset) is not { } part || part.Comp.PartType != HypertorusPartType.Corner
                || part.Comp.PanelOpen || GetDir(part) != dir)
            {
                return false;
            }

            corners.Add(part.Owner);
        }

        if (iface == null || fuel == null || moderator == null || waste == null || corners.Count != 4)
            return false;

        core.Comp.Interface = iface;
        core.Comp.FuelInput = fuel;
        core.Comp.ModeratorInput = moderator;
        core.Comp.WasteOutput = waste;
        core.Comp.Corners = corners;
        return true;
    }

    private Entity<HypertorusPartComponent>? FindPart(Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var uid in _map.GetAnchoredEntities(grid, grid, tile))
        {
            if (TryComp<HypertorusPartComponent>(uid, out var part))
                return (uid, part);
        }

        return null;
    }

    private Direction GetDir(EntityUid uid)
    {
        return Transform(uid).LocalRotation.GetCardinalDir();
    }

    private IEnumerable<EntityUid> LinkedParts(HypertorusCoreComponent core)
    {
        if (core.Interface is { } iface)
            yield return iface;
        if (core.FuelInput is { } fuel)
            yield return fuel;
        if (core.ModeratorInput is { } moderator)
            yield return moderator;
        if (core.WasteOutput is { } waste)
            yield return waste;
        foreach (var corner in core.Corners)
        {
            yield return corner;
        }
    }

    /// <summary>Три порта, которые могут треснуть (machine_parts).</summary>
    private IEnumerable<EntityUid> MachineParts(HypertorusCoreComponent core)
    {
        if (core.FuelInput is { } fuel)
            yield return fuel;
        if (core.WasteOutput is { } waste)
            yield return waste;
        if (core.ModeratorInput is { } moderator)
            yield return moderator;
    }

    /// <summary>interface/multitool_act + core/activate: связать все детали.</summary>
    private void TryActivate(Entity<HypertorusPartComponent> iface, EntityUid user)
    {
        var coreTile = Transform(iface).Coordinates.Offset(-GetDir(iface).ToVec());
        Entity<HypertorusCoreComponent>? core = null;
        if (TryGetTile(iface, out var grid, out _))
        {
            var tile = _map.TileIndicesFor(grid, grid, coreTile);
            foreach (var uid in _map.GetAnchoredEntities(grid, grid, tile))
            {
                if (TryComp<HypertorusCoreComponent>(uid, out var comp))
                    core = (uid, comp);
            }
        }

        if (core is not { } c || !CheckPartConnectivity(c))
        {
            _popup.PopupEntity(Loc.GetString("hypertorus-check-parts"), iface, user);
            return;
        }

        if (c.Comp.Active)
        {
            _popup.PopupEntity(Loc.GetString("hypertorus-already-active"), iface, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("hypertorus-linked"), iface, user);
        c.Comp.Active = true;
        SetActiveVisuals(c, true);
        foreach (var part in LinkedParts(c.Comp))
        {
            var comp = Comp<HypertorusPartComponent>(part);
            comp.Active = true;
            comp.Core = c;
            SetActiveVisuals(part, true);
        }

        _ambient.SetAmbience(c, true);

        if (!iface.Comp.Activated)
        {
            iface.Comp.Activated = true;
            Spawn(GuidePaper, Transform(iface).Coordinates);
        }
    }

    /// <summary>core/deactivate: развязать детали, звук и интерфейс гаснут.</summary>
    private void Deactivate(Entity<HypertorusCoreComponent> core)
    {
        if (!core.Comp.Active)
            return;

        core.Comp.Active = false;
        SetActiveVisuals(core, false);
        foreach (var part in LinkedParts(core.Comp))
        {
            if (TerminatingOrDeleted(part) || !TryComp<HypertorusPartComponent>(part, out var comp))
                continue;

            comp.Active = false;
            comp.Core = null;
            SetActiveVisuals(part, false);
            _ui.CloseUi(part, HypertorusUiKey.Key);
        }

        core.Comp.Interface = null;
        core.Comp.FuelInput = null;
        core.Comp.ModeratorInput = null;
        core.Comp.WasteOutput = null;
        core.Comp.Corners.Clear();
        _ambient.SetAmbience(core, false);
    }

    private void SetActiveVisuals(EntityUid uid, bool active)
    {
        _appearance.SetData(uid, HypertorusVisuals.Active, active);
    }

    /// <summary>core/Destroy: вместе с ядром исчезают все связанные детали.</summary>
    private void OnCoreShutdown(Entity<HypertorusCoreComponent> ent, ref ComponentShutdown args)
    {
        foreach (var part in LinkedParts(ent.Comp).ToArray())
        {
            if (!TerminatingOrDeleted(part))
                QueueDel(part);
        }
    }

    /// <summary>check_deconstructable: пока идёт синтез, детали нельзя вскрыть.</summary>
    private void UpdateFusionStarted(Entity<HypertorusCoreComponent> core)
    {
        var started = core.Comp.PowerLevel > 0;
        core.Comp.FusionStarted = started;
        foreach (var part in LinkedParts(core.Comp))
        {
            if (TryComp<HypertorusPartComponent>(part, out var comp))
                comp.FusionStarted = started;
        }
    }

    #endregion

    #region Инструменты

    private void OnPartInteractUsing(Entity<HypertorusPartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;
        var user = args.User;

        if (_tool.HasQuality(used, PulsingQuality) && ent.Comp.PartType == HypertorusPartType.Interface)
        {
            args.Handled = true;
            TryActivate(ent, user);
            return;
        }

        // welder_act: заварить трещину порта.
        if (ent.Comp.Cracked && _tool.HasQuality(used, WeldingQuality))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("hypertorus-repairing"), ent, user);
            _tool.UseTool(used, user, ent, 10f, WeldingQuality, new HypertorusRepairDoAfterEvent());
            return;
        }

        if (_tool.HasQuality(used, ScrewingQuality))
        {
            args.Handled = true;
            if (ent.Comp.FusionStarted)
            {
                _popup.PopupEntity(Loc.GetString("hypertorus-fusion-started"), ent, user);
                return;
            }

            ent.Comp.PanelOpen = !ent.Comp.PanelOpen;
            _appearance.SetData(ent, HypertorusVisuals.Open, ent.Comp.PanelOpen);
            PlayToolSound(used, user);
            return;
        }

        // default_change_direction_wrench: поворот по часовой при открытой панели.
        if (_tool.HasQuality(used, AnchoringQuality) && ent.Comp.PanelOpen)
        {
            args.Handled = true;
            _xform.SetLocalRotation(ent, Transform(ent).LocalRotation - Angle.FromDegrees(90));
            PlayToolSound(used, user);
            return;
        }

        // default_deconstruction_crowbar: деталь сворачивается обратно в коробку.
        if (_tool.HasQuality(used, PryingQuality) && ent.Comp.PanelOpen && !ent.Comp.FusionStarted)
        {
            args.Handled = true;
            Spawn(ent.Comp.Box, Transform(ent).Coordinates);
            PlayToolSound(used, user);
            QueueDel(ent);
        }
    }

    private void OnPartRepaired(Entity<HypertorusPartComponent> ent, ref HypertorusRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Cracked = false;
        _appearance.SetData(ent, HypertorusVisuals.Cracked, false);
        _popup.PopupEntity(Loc.GetString("hypertorus-repaired"), ent, args.User);
    }

    private void OnCoreInteractUsing(Entity<HypertorusCoreComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<HypertorusCorePanelComponent>(ent, out var panel))
            return;

        var used = args.Used;
        var user = args.User;

        if (_tool.HasQuality(used, ScrewingQuality))
        {
            args.Handled = true;
            if (ent.Comp.FusionStarted)
            {
                _popup.PopupEntity(Loc.GetString("hypertorus-fusion-started"), ent, user);
                return;
            }

            panel.PanelOpen = !panel.PanelOpen;
            _appearance.SetData(ent, HypertorusVisuals.Open, panel.PanelOpen);
            PlayToolSound(used, user);
            return;
        }

        // crowbar_deconstruction_act: газы ядра выходят наружу, ядро становится коробкой.
        if (_tool.HasQuality(used, PryingQuality) && panel.PanelOpen && !ent.Comp.FusionStarted)
        {
            args.Handled = true;
            if (ent.Comp.InternalFusion.Pressure > 0 || ent.Comp.ModeratorInternal.Pressure > 0)
                _chat.TrySendInGameICMessage(ent, Loc.GetString("hypertorus-deconstruct-warning"), InGameICChatType.Speak, false);

            if (_atmos.GetContainingMixture(ent.Owner, false, true) is { } air)
            {
                _atmos.Merge(air, RemoveRatioAt(ent.Comp.InternalFusion, ent.Comp.FusionTemp, 1));
                _atmos.Merge(air, RemoveRatioAt(ent.Comp.ModeratorInternal, ent.Comp.ModeratorTemp, 1));
            }

            Spawn(panel.Box, Transform(ent).Coordinates);
            PlayToolSound(used, user);
            QueueDel(ent);
        }
    }

    #endregion

    #region Осмотр

    private void OnPartExamined(Entity<HypertorusPartComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("hypertorus-examine-rotate"));
        if (ent.Comp.Cracked)
            args.PushMarkup(Loc.GetString("hypertorus-examine-cracked"));
    }

    private void OnCoreExamined(Entity<HypertorusCoreComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("hypertorus-examine-core"));
    }

    private void OnInterfaceOpenAttempt(Entity<HypertorusPartComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (ent.Comp.PartType != HypertorusPartType.Interface)
            return;

        if (ent.Comp.Active && ent.Comp.Core is { } core && !TerminatingOrDeleted(core))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("hypertorus-activate-first"), ent, args.User);
    }

    #endregion

    #region Доступ к трубам

    private GasMixture? GetPipeAir(EntityUid? uid)
    {
        if (uid is not { } part || !_nodeContainer.TryGetNode(part, HypertorusCoreComponent.PipeNode, out PipeNode? node))
            return null;

        return node.Air;
    }

    #endregion
}
