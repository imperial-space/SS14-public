using Content.Server.DoAfter;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using System.Numerics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Польза от декора: танк давит проволоку и колья и подрывается на минах, склады выдают припасы рукой,
/// станции лечения стягивают раны. Укрытия и ежи работают через физику (CadiaCoverSystem, слои столкновений).
/// </summary>
public sealed class CadiaFortificationSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly Content.Shared.Stunnable.SharedStunSystem _stun = default!;
    [Dependency] private readonly CadiaMoraleSystem _morale = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly Content.Shared.Maps.TurfSystem _turf = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    private const string MagazineSlot = "gun_magazine";
    private static readonly SoundSpecifier DepotSound = new SoundPathSpecifier("/Audio/Items/zip.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
    private const string DropProto = "CadiaSupplyDrop";
    private const string TrenchProto = "CadiaTrench";
    private static readonly SoundSpecifier DigSound = new SoundPathSpecifier("/Audio/Items/shovel_dig.ogg");
    private static readonly SoundSpecifier WarpTrapSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/fireball.ogg");
    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";
    private static readonly TimeSpan DropTime = TimeSpan.FromSeconds(3.5);
    private const int DropSpotTries = 8;
    private const float DropSpotMin = 1.5f;
    private const float DropSpotMax = 3f;
    private static readonly SoundSpecifier GrenadeInSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaPillboxComponent, MapInitEvent>(OnPillboxInit);
        SubscribeLocalEvent<CadiaPillboxComponent, ComponentShutdown>(OnPillboxShutdown);
        SubscribeLocalEvent<CadiaPillboxPartComponent, InteractUsingEvent>(OnPartInteract);
        SubscribeLocalEvent<CadiaTankCrushableComponent, StartCollideEvent>(OnCrush);
        SubscribeLocalEvent<CadiaTankMineComponent, StartCollideEvent>(OnMine);
        SubscribeLocalEvent<CadiaSupplyComponent, ActivateInWorldEvent>(OnSupply);
        SubscribeLocalEvent<CadiaSupplyComponent, ExaminedEvent>(OnSupplyExamined);
        SubscribeLocalEvent<CadiaDepotComponent, GetVerbsEvent<AlternativeVerb>>(OnDepotVerbs);
        SubscribeLocalEvent<CadiaDepotComponent, ExaminedEvent>(OnDepotExamined);
        SubscribeLocalEvent<CadiaHealStationComponent, ActivateInWorldEvent>(OnHealStation);
        SubscribeLocalEvent<CadiaEntrenchingToolComponent, AfterInteractEvent>(OnDig);
        SubscribeLocalEvent<CadiaEntrenchingToolComponent, CadiaTrenchDigDoAfterEvent>(OnDug);
        SubscribeLocalEvent<CadiaTrenchComponent, StartCollideEvent>(OnTrenchEnter);
        SubscribeLocalEvent<CadiaTrenchComponent, EndCollideEvent>(OnTrenchLeave);
        SubscribeLocalEvent<CadiaWarpTrapComponent, StartCollideEvent>(OnWarpTrap);
        SubscribeLocalEvent<CadiaHealStationComponent, CadiaHealStationDoAfterEvent>(OnHealed);
        SubscribeLocalEvent<CadiaHealStationComponent, ExaminedEvent>(OnHealExamined);
    }

    // ═══════════════════════ ДОТ ═══════════════════════

    /// <summary>Коробка ставит на свой купол орудие и считает себя центром укрытия.</summary>
    private void OnPillboxInit(Entity<CadiaPillboxComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<CadiaCoverComponent>(ent, out var cover))
            cover.Inside = ent;
        if (TryComp<CadiaPillboxPartComponent>(ent, out var part))
            part.Center = ent;
        var gun = Spawn(ent.Comp.GunProto, _xform.GetMapCoordinates(ent).Offset(ent.Comp.GunOffset));
        _xform.AttachToGridOrMap(gun);
        ent.Comp.Gun = gun;
    }

    private void OnPillboxShutdown(Entity<CadiaPillboxComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Gun is { } gun && !TerminatingOrDeleted(gun))
            QueueDel(gun);
    }

    /// <summary>Граната в руке, клик по стене ДОТа: её закидывают в амбразуру, таймер уже идёт.</summary>
    private void OnPartInteract(Entity<CadiaPillboxPartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Center is not { } center || TerminatingOrDeleted(center) || !HasComp<TimerTriggerComponent>(args.Used))
            return;
        args.Handled = true;
        var grenade = args.Used;
        if (!_hands.TryDrop(args.User, grenade))
            return;
        _xform.SetCoordinates(grenade, Transform(center).Coordinates.Offset(_random.NextVector2(1.2f)));
        _xform.AttachToGridOrMap(grenade);
        _trigger.ActivateTimerTrigger(grenade, args.User);
        _audio.PlayPvs(GrenadeInSound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-pillbox-grenade", ("name", Name(args.User))), ent, PopupType.MediumCaution);
    }

    /// <summary>Только машины на гусеницах: шагоходы перешагивают, стационарные орудия не ездят.</summary>
    private bool IsTank(EntityUid uid)
    {
        return TryComp<CadiaVehicleComponent>(uid, out var vehicle) && vehicle.TankControls && vehicle.Speed > 0f;
    }

    private void OnCrush(Entity<CadiaTankCrushableComponent> ent, ref StartCollideEvent args)
    {
        if (!IsTank(args.OtherEntity))
            return;
        _audio.PlayPvs(ent.Comp.Sound, _xform.GetMoverCoordinates(ent));
        QueueDel(ent);
    }

    private void OnMine(Entity<CadiaTankMineComponent> ent, ref StartCollideEvent args)
    {
        if (!IsTank(args.OtherEntity) || TerminatingOrDeleted(ent))
            return;
        _explosion.QueueExplosion(_xform.GetMapCoordinates(ent), "Default", ent.Comp.Intensity, 10f, 30f, ent, canCreateVacuum: false);
        QueueDel(ent);
    }

    // ═══════════════════════ Окопы ═══════════════════════

    private void OnDig(Entity<CadiaEntrenchingToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != null)
            return;
        var map = _xform.ToMapCoordinates(args.ClickLocation);
        if (!_mapManager.TryFindGridAt(map, out var gridUid, out var grid))
            return;
        args.Handled = true;

        var tile = _map.TileIndicesFor(gridUid, grid, args.ClickLocation);
        if (_turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid) || HasTrench(gridUid, grid, tile))
        {
            _popup.PopupEntity(Loc.GetString("cadia-trench-occupied"), args.User, args.User);
            return;
        }
        var spot = new EntityCoordinates(gridUid, new System.Numerics.Vector2(tile.X + 0.5f, tile.Y + 0.5f));
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.DigTime, new CadiaTrenchDigDoAfterEvent(GetNetCoordinates(spot)), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(DigSound, args.User);
    }

    private bool HasTrench(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<CadiaTrenchComponent>(uid))
                return true;
        }
        return false;
    }

    private void OnDug(Entity<CadiaEntrenchingToolComponent> ent, ref CadiaTrenchDigDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        var spot = GetCoordinates(args.Spot);
        Spawn(TrenchProto, spot);
        _audio.PlayPvs(DigSound, spot);
    }

    private void OnTrenchEnter(Entity<CadiaTrenchComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<MobStateComponent>(args.OtherEntity))
            return;
        var inside = EnsureComp<CadiaInTrenchComponent>(args.OtherEntity);
        inside.Sections++;
        Dirty(args.OtherEntity, inside);
    }

    private void OnTrenchLeave(Entity<CadiaTrenchComponent> ent, ref EndCollideEvent args)
    {
        if (!TryComp<CadiaInTrenchComponent>(args.OtherEntity, out var inside))
            return;
        inside.Sections--;
        if (inside.Sections <= 0)
            RemCompDeferred<CadiaInTrenchComponent>(args.OtherEntity);
        else
            Dirty(args.OtherEntity, inside);
    }

    /// <summary>Ловушка Хаоса: враг ранен, сбит с ног и напуган, свои проходят спокойно.</summary>
    private void OnWarpTrap(Entity<CadiaWarpTrapComponent> ent, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(ent) || !HasComp<MobStateComponent>(args.OtherEntity) ||
            !TryComp<CadiaFactionMemberComponent>(args.OtherEntity, out var member) || member.Faction == CadiaFaction.Chaos)
            return;
        var heat = new DamageSpecifier(_proto.Index(HeatDamage), 30);
        _damage.TryChangeDamage(args.OtherEntity, heat, origin: ent);
        _stun.TryUpdateParalyzeDuration(args.OtherEntity, TimeSpan.FromSeconds(2.5));
        _morale.Change(args.OtherEntity, -25f);
        Spawn("CadiaPsyWarpfireEffect", _xform.GetMapCoordinates(ent));
        _audio.PlayPvs(WarpTrapSound, _xform.GetMoverCoordinates(ent));
        QueueDel(ent);
    }

    // ═══════════════════════ Депо ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CadiaDepotComponent>();
        while (query.MoveNext(out var depot))
            depot.Points = Math.Min(depot.MaxPoints, depot.Points + depot.PerMinute * frameTime / 60f);
        UpdateDrops();

        var sources = EntityQueryEnumerator<CadiaWarpSourceComponent>();
        while (sources.MoveNext(out var source))
        {
            source.Accumulated += source.PerMinute * frameTime / 60f;
            if (source.Accumulated < 1f)
                continue;
            var whole = (int) source.Accumulated;
            source.Accumulated -= whole;
            _rule.AddWarp(whole);
        }
    }

    private void OnDepotVerbs(Entity<CadiaDepotComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        foreach (var entry in ent.Comp.Entries)
        {
            var chosen = entry;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cadia-depot-verb", ("name", Loc.GetString(entry.Name)), ("cost", entry.Cost)),
                Category = VerbCategory.SelectType,
                Disabled = ent.Comp.Points < entry.Cost,
                Act = () => DepotOrder(ent, chosen, user),
            });
        }
    }

    /// <summary>Заказ у депо: ящик появляется рядом, дальше его тащат руками или везут шаттлом.</summary>
    private void DepotOrder(Entity<CadiaDepotComponent> ent, CadiaDepotEntry entry, EntityUid user)
    {
        if (ent.Comp.Points < entry.Cost)
        {
            _popup.PopupEntity(Loc.GetString("cadia-depot-no-points"), ent, user, PopupType.SmallCaution);
            return;
        }
        ent.Comp.Points -= entry.Cost;

        // Груз не появляется из воздуха: его сбрасывают на парашюте рядом с заказавшим.
        var now = _timing.CurTime;
        var drop = Spawn(DropProto, FreeSpotNear(user));
        var comp = EnsureComp<CadiaSupplyDropComponent>(drop);
        comp.Item = entry.Proto;
        comp.Start = now;
        comp.LandAt = now + DropTime;
        Dirty(drop, comp);
        _audio.PlayPvs(DepotSound, drop);
        _popup.PopupEntity(Loc.GetString("cadia-depot-dropping"), user, user, PopupType.Medium);
    }

    /// <summary>Свободная клетка рядом с бойцом: не на него и не в стену.</summary>
    private EntityCoordinates FreeSpotNear(EntityUid user)
    {
        var here = Transform(user).Coordinates;
        if (_xform.GetGrid(here) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return here;
        for (var i = 0; i < DropSpotTries; i++)
        {
            var spot = here.Offset(_random.NextVector2(DropSpotMin, DropSpotMax));
            var tile = _map.TileIndicesFor(gridUid, grid, spot);
            if (!_turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
                return spot;
        }
        return here;
    }

    private void UpdateDrops()
    {
        var now = _timing.CurTime;
        var drops = EntityQueryEnumerator<CadiaSupplyDropComponent, TransformComponent>();
        var landed = new List<(EntityUid Uid, string Item, EntityCoordinates At)>();
        while (drops.MoveNext(out var uid, out var drop, out var xform))
        {
            if (now >= drop.LandAt)
                landed.Add((uid, drop.Item, xform.Coordinates));
        }
        foreach (var (uid, item, at) in landed)
        {
            var crate = Spawn(item, at);
            _audio.PlayPvs(LandSound, crate);
            Spawn("CadiaJumpImpactEffect", _xform.ToMapCoordinates(at));
            QueueDel(uid);
        }
    }

    private void OnDepotExamined(Entity<CadiaDepotComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cadia-depot-examine", ("points", (int) ent.Comp.Points), ("max", (int) ent.Comp.MaxPoints)));
    }

    // ═══════════════════════ Склады ═══════════════════════

    private void OnSupply(Entity<CadiaSupplyComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        var user = args.User;

        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.EmptyPopup), ent, user);
            return;
        }

        string? proto = null;
        if (ent.Comp.Items.Count > 0)
        {
            proto = ent.Comp.Items[(ent.Comp.Charges - 1) % ent.Comp.Items.Count];
        }
        else
        {
            foreach (var held in _hands.EnumerateHeld(user))
            {
                if (_slots.TryGetSlot(held, MagazineSlot, out var slot) && slot.StartingItem is { } mag)
                {
                    proto = mag;
                    break;
                }
            }
            if (proto == null)
            {
                _popup.PopupEntity(Loc.GetString("cadia-supply-need-gun"), ent, user);
                return;
            }
        }

        ent.Comp.Charges--;
        var item = Spawn(proto, Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, item);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        // Ящик с депо пустеет и исчезает, стационарный склад остаётся стоять.
        if (ent.Comp.Charges <= 0 && ent.Comp.DeleteWhenEmpty)
            QueueDel(ent);
    }

    private void OnSupplyExamined(Entity<CadiaSupplyComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cadia-supply-examine", ("charges", ent.Comp.Charges)));
    }

    // ═══════════════════════ Лечение ═══════════════════════

    private void OnHealStation(Entity<CadiaHealStationComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cadia-supply-empty"), ent, args.User);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new CadiaHealStationDoAfterEvent(), ent, args.User, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnHealed(Entity<CadiaHealStationComponent> ent, ref CadiaHealStationDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Charges <= 0)
            return;
        args.Handled = true;
        ent.Comp.Charges--;
        _damage.HealEvenly(args.User, -ent.Comp.Heal);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-heal-done"), args.User, args.User, PopupType.Medium);
    }

    private void OnHealExamined(Entity<CadiaHealStationComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cadia-supply-examine", ("charges", ent.Comp.Charges)));
    }
}
