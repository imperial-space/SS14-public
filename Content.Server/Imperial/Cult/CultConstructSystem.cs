using Content.Server.Actions;
using System.Linq;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Инициализирует конструктов культа, выдаёт им действия и выполняет их превращение в жнецов после призыва Нар'Си.
/// </summary>
public sealed class CultConstructSystem : EntitySystem
{
    private static readonly EntProtoId CommuneAction = "ActionCultCommune";
    private static readonly EntProtoId CreateWallAction = "ActionCultConstructCreateWall";

    private static readonly string[] ForbiddenCultistActions =
    {
        "ActionCultBloodMagic",
        "ActionCultStun",
        "ActionCultShackles",
        "ActionCultTeleport",
        "ActionCultEmp",
        "ActionCultTwistedConstruction",
        "ActionCultSummonDagger",
        "ActionCultSummonEquipment",
        "ActionCultConcealPresence",
        "ActionCultBloodRites",
        "ActionCultRecallBloodSpear",
        "ActionCultDarkSpiritReturn",
        "ActionCultDarkSpiritCommune",
    };

    private static readonly ProtoId<EntityPrototype>[] ArtificerActions =
    {
        "ActionCultConstructCreateJuggernautShell",
        "ActionCultConstructCreateWraithShell",
        "ActionCultConstructCreateArtificerShell",
        "ActionCultConstructCreateSoulStone",
        "ActionCultConstructCreatePylon",
        "ActionCultConstructCreateFloor",
        "ActionCultConstructHealAlly",
    };

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultConstructComponent, ComponentStartup>(OnConstructStartup);
        SubscribeLocalEvent<CultConstructComponent, ComponentShutdown>(OnConstructShutdown);
        SubscribeLocalEvent<CultConstructComponent, CultCommuneActionEvent>(OnCommune);
        SubscribeLocalEvent<CultConstructComponent, CultConstructSpawnItemActionEvent>(OnSpawnItemAction);
        SubscribeLocalEvent<CultConstructComponent, CultConstructSpawnStructureActionEvent>(OnSpawnStructureAction);
        SubscribeLocalEvent<CultConstructComponent, CultConstructHealTargetActionEvent>(OnHealTargetAction);
        SubscribeLocalEvent<CultConstructComponent, CultConstructCreateFloorActionEvent>(OnCreateFloorAction);
        SubscribeLocalEvent<CultNarSieSummonedEvent>(OnNarSieSummoned);
    }

    private void OnConstructStartup(EntityUid uid, CultConstructComponent comp, ComponentStartup args)
    {
        // Конструкт не должен вести себя как полноценный культист: без Blood Magic и без antag-маркера.
        if (TryComp<CultistComponent>(uid, out var cultist) && cultist.BuiHolder.HasValue && Exists(cultist.BuiHolder.Value))
            QueueDel(cultist.BuiHolder.Value);

        RemCompDeferred<CultistComponent>(uid);
        RemComp<Content.Shared.Antag.ShowAntagIconsComponent>(uid);

        foreach (var action in _actions.GetActions(uid))
        {
            var protoId = MetaData(action.Owner).EntityPrototype?.ID;
            if (protoId != null && ForbiddenCultistActions.Contains(protoId))
                _actions.RemoveAction(uid, action.Owner);
        }

        _actions.AddAction(uid, CommuneAction);

        switch (comp.Kind)
        {
            case CultConstructKind.Artificer:
                foreach (var action in ArtificerActions)
                {
                    _actions.AddAction(uid, action);
                }
                break;
            case CultConstructKind.Juggernaut:
                _actions.AddAction(uid, CreateWallAction);
                break;
        }

        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        var activeRadio = EnsureComp<ActiveRadioComponent>(uid);
        if (!activeRadio.Channels.Contains("Cult"))
            activeRadio.Channels.Add("Cult");

        var holder = Spawn("CultBuiHolder", Transform(uid).Coordinates);
        _xform.SetParent(holder, uid);
        comp.BuiHolder = holder;

        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetDrawLight((uid, eye), false);
    }

    private void OnConstructShutdown(EntityUid uid, CultConstructComponent comp, ComponentShutdown args)
    {
        if (comp.BuiHolder.HasValue && Exists(comp.BuiHolder.Value))
            QueueDel(comp.BuiHolder.Value);
    }

    private void OnCommune(EntityUid uid, CultConstructComponent comp, CultCommuneActionEvent args)
    {
        args.Handled = true;

        if (comp.BuiHolder.HasValue)
            _ui.TryOpenUi(comp.BuiHolder.Value, CultCommuneBuiKey.Key, uid);
    }

    private void OnSpawnItemAction(EntityUid uid, CultConstructComponent comp, CultConstructSpawnItemActionEvent args)
    {
        args.Handled = true;
        var spawned = Spawn(args.Prototype, Transform(uid).Coordinates);
        _popup.PopupEntity(Loc.GetString("cult-construct-created", ("name", MetaData(spawned).EntityName)), uid, uid, PopupType.Small);
    }

    private void OnSpawnStructureAction(EntityUid uid, CultConstructComponent comp, CultConstructSpawnStructureActionEvent args)
    {
        var userCoords = _xform.GetMapCoordinates(uid);
        var targetCoords = _xform.ToMapCoordinates(args.Target);
        if (userCoords.MapId != targetCoords.MapId || (userCoords.Position - targetCoords.Position).Length() > 2.5f)
        {
            _popup.PopupEntity(Loc.GetString("cult-construct-too-far"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var spawned = Spawn(args.Prototype, args.Target.SnapToGrid(EntityManager));
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cult-construct-created", ("name", MetaData(spawned).EntityName)), uid, uid, PopupType.Small);
    }

    private void OnHealTargetAction(EntityUid uid, CultConstructComponent comp, CultConstructHealTargetActionEvent args)
    {
        if (!HasComp<Content.Shared.Imperial.Cult.Components.CultistComponent>(args.Target)
            && !HasComp<CultConstructComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("cult-construct-heal-invalid"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!TryComp<DamageableComponent>(args.Target, out _))
            return;

        args.Handled = true;
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -15;
        heal.DamageDict["Slash"] = -15;
        heal.DamageDict["Piercing"] = -15;
        heal.DamageDict["Heat"] = -15;
        heal.DamageDict["Shock"] = -15;
        heal.DamageDict["Cold"] = -15;
        heal.DamageDict["Caustic"] = -15;
        _damage.TryChangeDamage(args.Target, heal, ignoreResistances: true, interruptsDoAfters: false);

        _popup.PopupEntity(Loc.GetString("cult-construct-heal-success"), args.Target, args.Target, PopupType.Small);
    }

    private void OnCreateFloorAction(EntityUid uid, CultConstructComponent comp, CultConstructCreateFloorActionEvent args)
    {
        var userCoords = _xform.GetMapCoordinates(uid);
        var targetCoords = _xform.ToMapCoordinates(args.Target);
        if (userCoords.MapId != targetCoords.MapId || (userCoords.Position - targetCoords.Position).Length() > 2.5f)
        {
            _popup.PopupEntity(Loc.GetString("cult-construct-too-far"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (args.Target.GetGridUid(EntityManager) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        if (!_tiles.TryGetDefinition("FloorCult", out var cultFloor))
            return;

        var indices = _map.CoordinatesToTile(gridUid, grid, args.Target);
        _map.SetTile(gridUid, grid, indices, new Tile(cultFloor.TileId));
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cult-construct-floor-success"), uid, uid, PopupType.Small);
    }

    private void OnNarSieSummoned(CultNarSieSummonedEvent ev)
    {
        var transforms = new List<(EntityUid OldUid, EntityCoordinates Coords, EntityUid? MindId)>();

        var cultistQuery = EntityQueryEnumerator<Content.Shared.Imperial.Cult.Components.CultistComponent>();
        while (cultistQuery.MoveNext(out var cultistUid, out _))
        {
            if (_mobState.IsDead(cultistUid))
                continue;

            _mind.TryGetMind(cultistUid, out var mindId, out _);
            transforms.Add((cultistUid, Transform(cultistUid).Coordinates, mindId));
        }

        var constructQuery = EntityQueryEnumerator<CultConstructComponent>();
        while (constructQuery.MoveNext(out var constructUid, out var construct))
        {
            if (construct.Kind == CultConstructKind.Harvester || _mobState.IsDead(constructUid))
                continue;

            _mind.TryGetMind(constructUid, out var mindId, out _);
            transforms.Add((constructUid, Transform(constructUid).Coordinates, mindId));
        }

        foreach (var transform in transforms)
        {
            if (!Exists(transform.OldUid) || TerminatingOrDeleted(transform.OldUid))
                continue;

            var harvester = Spawn("MobCultHarvester", transform.Coords);
            if (transform.MindId != null)
                _mind.TransferTo(transform.MindId.Value, harvester);

            QueueDel(transform.OldUid);
        }
    }
}