using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>/obj/item/teleportation_scroll: свиток с несколькими телепортами по зонам станции.</summary>
[RegisterComponent]
public sealed partial class ImperialTeleportScrollComponent : Component
{
    [DataField]
    public int Uses = 4;
}

/// <summary>
/// Телепорт по зонам (area_teleport). Зоны станции — именованные маяки навигационной карты:
/// телепорт переносит на случайную свободную клетку рядом с маяком.
/// </summary>
public sealed class ImperialWizardTeleportSystem : EntitySystem
{
    [Dependency] private readonly ImperialListInputSystem _input = default!;
    [Dependency] private readonly ImperialSpellSystem _spells = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly SoundSpecifier ArriveSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/teleport_app.ogg");
    private static readonly EntProtoId SmokeProto = "WizardSmoke";

    /// <summary>Радиус зоны вокруг маяка.</summary>
    private const float AreaRadius = 5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialSpellComponent, ImperialAreaTeleportActionEvent>(OnTeleportSpell);
        SubscribeLocalEvent<ImperialTeleportScrollComponent, UseInHandEvent>(OnScrollUse);
        SubscribeLocalEvent<ImperialTeleportScrollComponent, ExaminedEvent>(OnScrollExamine);
    }

    /// <summary>GLOB.teleportlocs: имена зон и их маяки.</summary>
    public Dictionary<string, EntityUid> GetAreas()
    {
        var result = new Dictionary<string, EntityUid>();
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (!beacon.Enabled || xform.GridUid == null || _station.GetOwningStation(uid, xform) == null)
                continue;

            var name = beacon.Text ?? (beacon.DefaultText is { } loc ? Loc.GetString(loc) : null);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result.TryAdd(name, uid);
        }

        return result;
    }

    /// <summary>get_destinations(): свободные клетки зоны.</summary>
    public bool TryGetDestination(EntityUid beacon, out EntityCoordinates coords)
    {
        coords = default;
        var xform = Transform(beacon);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var center = _maps.LocalToTile(gridUid, grid, xform.Coordinates);
        var candidates = new List<Vector2i>();
        var radius = (int) AreaRadius;
        for (var x = -radius; x <= radius; x++)
        for (var y = -radius; y <= radius; y++)
        {
            var tile = center + new Vector2i(x, y);
            if (!_maps.TryGetTileRef(gridUid, grid, tile, out var tileRef) || _turf.IsSpace(tileRef)
                || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
                continue;

            candidates.Add(tile);
        }

        if (candidates.Count == 0)
            return false;

        coords = _maps.GridTileToLocal(gridUid, grid, _random.Pick(candidates));
        return true;
    }

    /// <summary>do_teleport(): перенос с дымом и звуком прибытия.</summary>
    public void Teleport(EntityUid user, EntityCoordinates coords, bool smoke)
    {
        _buckle.TryUnbuckle(user, user, popup: false);
        if (smoke)
            Spawn(SmokeProto, Transform(user).Coordinates);

        _transform.SetCoordinates(user, coords);
        _transform.AttachToGridOrMap(user);
        _audio.PlayPvs(ArriveSound, user);
        if (smoke)
            Spawn(SmokeProto, coords);
    }

    private void OnTeleportSpell(Entity<ImperialSpellComponent> ent, ref ImperialAreaTeleportActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.Performer, out var actor))
            return;

        // Откат начнётся после выбора зоны.
        var user = args.Performer;
        var areas = GetAreas();
        _input.Open(actor.PlayerSession, Loc.GetString("imperial-wizard-teleport-title"),
            Loc.GetString("imperial-wizard-teleport-prompt"), areas.Keys.OrderBy(k => k).ToList(), choice =>
            {
                if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(user) || !areas.TryGetValue(choice, out var beacon)
                    || TerminatingOrDeleted(beacon) || !_spells.CanCast(ent, user, true))
                    return;

                if (!TryGetDestination(beacon, out var coords))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-wizard-teleport-no-room"), user, user);
                    return;
                }

                // invocation_says_area: «SCYAR NILA, МОСТИК!»
                if (ent.Comp.Invocation is { } loc)
                {
                    var phrase = Loc.GetString(loc);
                    var invocation = ent.Comp.InvocationType == ImperialInvocationType.Whisper
                        ? $"{phrase}, {choice.ToUpperInvariant()}."
                        : $"{phrase}, {choice.ToUpperInvariant()}!";
                    _spells.InvokeText(ent, user, invocation);
                }

                if (ent.Comp.CastSound != null)
                    _audio.PlayPvs(ent.Comp.CastSound, user);

                Teleport(user, coords, ent.Comp.Smoke > 0);
                _actions.SetCooldown(ent.Owner, _spells.GetCooldown(ent.Comp));
                var cast = new ImperialSpellCastEvent(user, ent);
                RaiseLocalEvent(user, ref cast);
            });
    }

    private void OnScrollUse(Entity<ImperialTeleportScrollComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Uses <= 0 || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        args.Handled = true;
        var user = args.User;
        var areas = GetAreas();
        _input.Open(actor.PlayerSession, Loc.GetString("imperial-wizard-teleport-title"),
            Loc.GetString("imperial-wizard-teleport-prompt"), areas.Keys.OrderBy(k => k).ToList(), choice =>
            {
                if (TerminatingOrDeleted(ent) || ent.Comp.Uses <= 0 || !_hands.IsHolding(user, ent)
                    || !areas.TryGetValue(choice, out var beacon) || TerminatingOrDeleted(beacon))
                    return;

                if (!TryGetDestination(beacon, out var coords))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-wizard-teleport-no-room"), user, user);
                    return;
                }

                Teleport(user, coords, true);
                ent.Comp.Uses--;
                if (ent.Comp.Uses > 0)
                    return;

                _popup.PopupEntity(Loc.GetString("imperial-wizard-scroll-dust", ("scroll", ent.Owner)), user, user, PopupType.MediumCaution);
                QueueDel(ent);
            });
    }

    private void OnScrollExamine(Entity<ImperialTeleportScrollComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Uses > 0)
            args.PushMarkup(Loc.GetString("imperial-wizard-scroll-uses", ("uses", ent.Comp.Uses)));
    }
}
