using System.Linq;
using System.Numerics;
using Content.Server.Electrocution;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Ghost;
using Content.Shared.Administration.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

#region Компоненты

/// <summary>/obj/item/scrying: в руках — зрение сквозь всё; в руке «использовать» — выйти духом из тела.</summary>
[RegisterComponent]
public sealed partial class ImperialScryingOrbComponent : Component
{
    [ViewVariables]
    public EntityUid? Holder;
}

/// <summary>/obj/item/necromantic_stone: поднимает мёртвых скелетами-рабами (до трёх).</summary>
[RegisterComponent]
public sealed partial class ImperialNecromanticStoneComponent : Component
{
    [DataField]
    public int MaxThralls = 3;

    [DataField]
    public bool Unlimited;

    [ViewVariables]
    public List<EntityUid> Thralls = new();
}

/// <summary>/obj/item/warp_whistle: смерч уносит в случайное безопасное место станции.</summary>
[RegisterComponent]
public sealed partial class ImperialWarpWhistleComponent : Component
{
    [ViewVariables]
    public EntityUid? Whistler;

    [ViewVariables]
    public TimeSpan SendAt;
}

/// <summary>/obj/item/runic_vendor_scepter: три заряда рунических торгоматов.</summary>
[RegisterComponent]
public sealed partial class ImperialRunicScepterComponent : Component
{
    [DataField]
    public int MaxCharges = 3;

    [DataField]
    public int Charges = 3;

    [DataField]
    public float Range = 7f;

    [DataField]
    public EntProtoId Vendor = "ImperialRunicVendor";

    [ViewVariables]
    public NetCoordinates? PendingTarget;
}

/// <summary>Рунический торгомат: отталкивает при появлении, распадается через 30 секунд, может взорваться.</summary>
[RegisterComponent]
public sealed partial class ImperialRunicVendorComponent : Component
{
    [ViewVariables]
    public TimeSpan DecayAt;
}

/// <summary>/obj/item/mjollnir: удар в двух руках или бросок — оглушение и отбрасывание.</summary>
[RegisterComponent]
public sealed partial class ImperialMjolnirComponent : Component;

/// <summary>/obj/item/singularityhammer: удар в двух руках стягивает всё вокруг к точке удара (раз в 10 секунд).</summary>
[RegisterComponent]
public sealed partial class ImperialSingularityHammerComponent : Component
{
    [ViewVariables]
    public TimeSpan NextCharge;
}

/// <summary>/obj/item/teleport_rod: телепорт в видимую точку до 8 клеток; поток причиняет урон при частых прыжках.</summary>
[RegisterComponent]
public sealed partial class ImperialTeleportRodComponent : Component
{
    [DataField]
    public float Range = 8f;
}

/// <summary>teleport_flux: повторный телепорт в течение 6 секунд ранит.</summary>
[RegisterComponent]
public sealed partial class ImperialTeleportFluxComponent : Component
{
    [ViewVariables]
    public TimeSpan End;
}

[Serializable, NetSerializable]
public sealed partial class ImperialArtefactDoAfterEvent : SimpleDoAfterEvent;

#endregion

/// <summary>Артефакты мага TG (equipment/artefact.dm, wizard_hammer.dm, teleport_rod.dm).</summary>
public sealed class ImperialWizardArtefactsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly ImperialWizardTeleportSystem _teleport = default!;
    [Dependency] private readonly ImperialWizardSpellsSystem _spells = default!;
    [Dependency] private readonly ImperialWizardRuleSystem _wizard = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;

    private static readonly SoundSpecifier WhistleSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/warpwhistle.ogg");
    private static readonly SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/summonitems_generic.ogg");

    /// <summary>/datum/outfit/roman.</summary>
    private static readonly (string Slot, string Proto)[] RomanOutfit =
    {
        ("head", "ImperialClothingHeadHelmetRoman"), ("jumpsuit", "ImperialClothingUniformRoman"), ("shoes", "ImperialClothingShoesRoman"),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialScryingOrbComponent, GotEquippedHandEvent>(OnOrbPicked);
        SubscribeLocalEvent<ImperialScryingOrbComponent, GotUnequippedHandEvent>(OnOrbDropped);
        SubscribeLocalEvent<ImperialScryingOrbComponent, UseInHandEvent>(OnOrbUse);
        SubscribeLocalEvent<ImperialNecromanticStoneComponent, AfterInteractEvent>(OnNecroStone);
        SubscribeLocalEvent<ImperialNecromanticStoneComponent, ExaminedEvent>(OnNecroExamine);
        SubscribeLocalEvent<ImperialWarpWhistleComponent, UseInHandEvent>(OnWhistle);
        SubscribeLocalEvent<ImperialRunicScepterComponent, AfterInteractEvent>(OnScepter);
        SubscribeLocalEvent<ImperialRunicScepterComponent, UseInHandEvent>(OnScepterRecharge);
        SubscribeLocalEvent<ImperialRunicScepterComponent, ImperialArtefactDoAfterEvent>(OnScepterDoAfter);
        SubscribeLocalEvent<ImperialRunicScepterComponent, ImperialMagicChargeEvent>(OnScepterCharged);
        SubscribeLocalEvent<ImperialRunicScepterComponent, ExaminedEvent>(OnScepterExamine);
        SubscribeLocalEvent<ImperialRunicVendorComponent, MapInitEvent>(OnVendorInit);
        SubscribeLocalEvent<ImperialMjolnirComponent, MeleeHitEvent>(OnMjolnirHit);
        SubscribeLocalEvent<ImperialMjolnirComponent, ThrowDoHitEvent>(OnMjolnirThrowHit);
        SubscribeLocalEvent<ImperialSingularityHammerComponent, MeleeHitEvent>(OnSingularityHit);
        SubscribeLocalEvent<ImperialTeleportRodComponent, AfterInteractEvent>(OnTeleportRod);
    }

    #region Шар предсказаний

    /// <summary>process(): держащий видит сквозь стены (TRAIT_XRAY_VISION).</summary>
    private void OnOrbPicked(Entity<ImperialScryingOrbComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.Holder = args.User;
        _eye.SetDrawFov(args.User, false);
        _popup.PopupEntity(Loc.GetString("imperial-scrying-see"), args.User, args.User);
    }

    private void OnOrbDropped(Entity<ImperialScryingOrbComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.Holder = null;
        if (!HasComp<ImperialPermanentXrayComponent>(args.User))
            _eye.SetDrawFov(args.User, true);
        _popup.PopupEntity(Loc.GetString("imperial-scrying-fade"), args.User, args.User);
    }

    /// <summary>attack_self(): user.ghostize(TRUE) — выйти духом, тело остаётся.</summary>
    private void OnOrbUse(Entity<ImperialScryingOrbComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_mind.TryGetMind(args.User, out var mindId, out var mind))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-scrying-stare", ("user", args.User)), args.User, Content.Shared.Popups.PopupType.Medium);
        _ghost.OnGhostAttempt(mindId, true, mind: mind);
    }

    #endregion

    #region Некромантский камень

    /// <summary>attack(): только мёртвый человек с душой; до трёх рабов одновременно.</summary>
    private void OnNecroStone(Entity<ImperialNecromanticStoneComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        args.Handled = true;
        var user = args.User;
        if (!_mobs.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-necrostone-only-dead"), user, user);
            return;
        }

        // Найти душу: призрак возвращается в тело.
        if (!TryGetSoul(target, out var mindId, out var mind))
        {
            _popup.PopupEntity(Loc.GetString("imperial-necrostone-no-soul"), user, user);
            return;
        }

        ent.Comp.Thralls.RemoveAll(t => TerminatingOrDeleted(t) || _mobs.IsDead(t));
        if (!ent.Comp.Unlimited && ent.Comp.Thralls.Count >= ent.Comp.MaxThralls)
        {
            _popup.PopupEntity(Loc.GetString("imperial-necrostone-max", ("max", ent.Comp.MaxThralls)), user, user);
            return;
        }

        if (mind.OwnedEntity != target)
            _mind.TransferTo(mindId, target, mind: mind);

        var skeleton = _spells.Skeletonize(target);
        _rejuvenate.PerformRejuvenate(skeleton);
        ent.Comp.Thralls.Add(skeleton);

        // equip_revived_servant(): всё на пол, римское снаряжение.
        if (_inventory.TryGetSlots(skeleton, out var slots))
        {
            foreach (var slot in slots)
            {
                _inventory.TryUnequip(skeleton, slot.Name, true, true);
            }
        }

        foreach (var (slot, proto) in RomanOutfit)
        {
            var item = Spawn(proto, Transform(skeleton).Coordinates);
            if (!_inventory.TryEquip(skeleton, item, slot, true, true))
                QueueDel(item);
        }

        _wizard.MakeMinion(skeleton, user, Loc.GetString("imperial-necrostone-objective", ("master", Name(user))));
        _popup.PopupEntity(Loc.GetString("imperial-necrostone-revived", ("master", user)), skeleton, skeleton, Content.Shared.Popups.PopupType.LargeCaution);
    }

    /// <summary>Разум умершего, который можно вернуть (у призрака есть игрок).</summary>
    private bool TryGetSoul(EntityUid body, out EntityUid mindId, out MindComponent mind)
    {
        mindId = default;
        mind = default!;
        if (_mind.TryGetMind(body, out mindId, out mind!) && mind.UserId != null)
            return true;

        var query = AllEntityQuery<MindComponent>();
        while (query.MoveNext(out var uid, out var candidate))
        {
            if (candidate.OriginalOwnedEntity != GetNetEntity(body) || candidate.UserId == null)
                continue;

            mindId = uid;
            mind = candidate;
            return true;
        }

        return false;
    }

    private void OnNecroExamine(Entity<ImperialNecromanticStoneComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Unlimited)
            args.PushMarkup(Loc.GetString("imperial-necrostone-count", ("count", ent.Comp.Thralls.Count(t => !TerminatingOrDeleted(t))), ("max", ent.Comp.MaxThralls)));
    }

    #endregion

    #region Свисток

    /// <summary>attack_self(): смерч прилетает и через пару секунд уносит в безопасное место станции.</summary>
    private void OnWhistle(Entity<ImperialWarpWhistleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Whistler != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-whistle-cooldown", ("whistle", ent.Owner)), args.User, args.User);
            return;
        }

        ent.Comp.Whistler = args.User;
        ent.Comp.SendAt = _timing.CurTime + TimeSpan.FromSeconds(4);
        _audio.PlayPvs(WhistleSound, args.User);
        var side = _random.Prob(0.5f) ? -7f : 7f;
        var tornado = Spawn("ImperialWarpTornado", _transform.GetMapCoordinates(args.User).Offset(new Vector2(side, 0)));
        EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(tornado).Lifetime = 6f;
        if (TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(tornado, out var physics))
            EntityManager.System<Robust.Shared.Physics.Systems.SharedPhysicsSystem>().SetLinearVelocity(tornado, new Vector2(-side / 3.5f, 0), body: physics);
    }

    private void UpdateWhistles()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialWarpWhistleComponent>();
        while (query.MoveNext(out _, out var whistle))
        {
            if (whistle.Whistler is not { } user || now < whistle.SendAt)
                continue;

            whistle.Whistler = null;
            if (TerminatingOrDeleted(user))
                continue;

            // send_away(): get_safe_random_station_turf_equal_weight().
            var areas = _teleport.GetAreas();
            if (areas.Count == 0 || !_teleport.TryGetDestination(_random.Pick(areas.Values.ToList()), out var destination))
                continue;

            _stun.TryUpdateStunDuration(user, TimeSpan.FromSeconds(1));
            _teleport.Teleport(user, destination, false);
        }
    }

    #endregion

    #region Скипетр вендормантии

    private void OnScepter(Entity<ImperialRunicScepterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var coords = args.ClickLocation;

        // Взорвать торгомат, если он на клетке.
        var vendor = args.Target is { } t && HasComp<ImperialRunicVendorComponent>(t)
            ? t
            : _lookup.GetEntitiesInRange<ImperialRunicVendorComponent>(coords, 0.5f).Select(v => (EntityUid?) v.Owner).FirstOrDefault();
        if (vendor is { } found)
        {
            args.Handled = true;
            RunicExplosion(found);
            return;
        }

        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-scepter-no-charges"), user, user);
            return;
        }

        if (!coords.TryDistance(EntityManager, Transform(user).Coordinates, out var distance) || distance > ent.Comp.Range)
        {
            _popup.PopupEntity(Loc.GetString("imperial-scepter-too-far"), user, user);
            return;
        }

        if (distance < 0.5f)
        {
            _popup.PopupEntity(Loc.GetString("imperial-scepter-too-close"), user, user);
            return;
        }

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(1), new ImperialArtefactDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            Hidden = false,
        });
        ent.Comp.PendingTarget = GetNetCoordinates(coords);
    }

    private void OnScepterRecharge(Entity<ImperialRunicScepterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.PendingTarget = null;
        _popup.PopupEntity(Loc.GetString("imperial-scepter-recharging"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new ImperialArtefactDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
        });
    }

    private void OnScepterDoAfter(Entity<ImperialRunicScepterComponent> ent, ref ImperialArtefactDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.PendingTarget is not { } pending)
        {
            ent.Comp.Charges = ent.Comp.MaxCharges;
            _popup.PopupEntity(Loc.GetString("imperial-scepter-charged"), args.User, args.User);
            return;
        }

        ent.Comp.PendingTarget = null;
        if (ent.Comp.Charges <= 0)
            return;

        var coords = GetCoordinates(pending);
        ent.Comp.Charges--;
        var vendor = Spawn(ent.Comp.Vendor, coords);
        _transform.AnchorEntity(vendor);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/resonator_fire.ogg"), vendor);
        _popup.PopupEntity(Loc.GetString("imperial-scepter-summon", ("user", args.User)), args.User, Content.Shared.Popups.PopupType.Medium);
    }

    private void OnScepterCharged(Entity<ImperialRunicScepterComponent> ent, ref ImperialMagicChargeEvent args)
    {
        ent.Comp.Charges = ent.Comp.MaxCharges;
        args.Charged = true;
    }

    private void OnScepterExamine(Entity<ImperialRunicScepterComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-scepter-charges", ("charges", ent.Comp.Charges)));
    }

    /// <summary>runic_pulse(): отталкивание всех в радиусе 2 при появлении.</summary>
    private void OnVendorInit(Entity<ImperialRunicVendorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.DecayAt = _timing.CurTime + TimeSpan.FromSeconds(30);
        var center = _transform.GetWorldPosition(ent);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, 2.5f))
        {
            var away = _transform.GetWorldPosition(mob) - center;
            if (away.LengthSquared() < 0.01f)
                away = new Vector2(0, 1);
            _popup.PopupEntity(Loc.GetString("imperial-runic-vendor-repel"), mob, mob, Content.Shared.Popups.PopupType.MediumCaution);
            _throwing.TryThrow(mob, away.Normalized() * 4f, 8f, recoil: false);
        }
    }

    /// <summary>runic_explosion(): лёгкий взрыв радиусом 2.</summary>
    private void RunicExplosion(EntityUid vendor)
    {
        _explosion.QueueExplosion(_transform.GetMapCoordinates(vendor), "Default", 30, 5, 8, vendor);
        QueueDel(vendor);
    }

    private void UpdateVendors()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialRunicVendorComponent>();
        var decayed = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var vendor))
        {
            if (now >= vendor.DecayAt)
                decayed.Add(uid);
        }

        foreach (var uid in decayed)
        {
            _popup.PopupEntity(Loc.GetString("imperial-runic-vendor-decay", ("vendor", uid)), uid, Content.Shared.Popups.PopupType.Small);
            QueueDel(uid);
        }
    }

    #endregion

    #region Молоты

    /// <summary>yeet_shock(): оглушение, падение и полёт прочь.</summary>
    private void YeetShock(EntityUid hammer, EntityUid target)
    {
        _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(1.5));
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(10));
        Spawn("EffectSparks", Transform(target).Coordinates);
        _popup.PopupEntity(Loc.GetString("imperial-mjolnir-shock", ("target", target)), target, Content.Shared.Popups.PopupType.LargeCaution);
        var away = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(hammer);
        if (away.LengthSquared() < 0.01f)
            away = _random.NextVector2();
        _throwing.TryThrow(target, away.Normalized() * 20f, 12f, recoil: false);
    }

    private void OnMjolnirHit(Entity<ImperialMjolnirComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !TryComp<WieldableComponent>(ent, out var wield) || !wield.Wielded)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (HasComp<MobStateComponent>(hit))
                YeetShock(args.User, hit);
        }
    }

    private void OnMjolnirThrowHit(Entity<ImperialMjolnirComponent> ent, ref ThrowDoHitEvent args)
    {
        if (HasComp<MobStateComponent>(args.Target))
            YeetShock(ent, args.Target);
    }

    /// <summary>vortex(): всё в радиусе 5 тянется к точке удара, существа падают на 2 секунды.</summary>
    private void OnSingularityHit(Entity<ImperialSingularityHammerComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !TryComp<WieldableComponent>(ent, out var wield) || !wield.Wielded || _timing.CurTime < ent.Comp.NextCharge)
            return;

        ent.Comp.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(10);
        if (args.HitEntities.Count == 0)
            return;

        var center = Transform(args.HitEntities[0]).Coordinates;
        foreach (var hit in args.HitEntities)
        {
            if (HasComp<MobStateComponent>(hit))
                _damage.TryChangeDamage(hit, new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } }, origin: args.User);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/marauder.ogg"), args.User);
        var pullTo = _transform.ToMapCoordinates(center).Position;
        foreach (var thing in _lookup.GetEntitiesInRange(center, 5f, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (thing == args.User || Transform(thing).Anchored)
                continue;

            if (HasComp<MobStateComponent>(thing))
                _stun.TryUpdateParalyzeDuration(thing, TimeSpan.FromSeconds(2));

            var toward = pullTo - _transform.GetWorldPosition(thing);
            if (toward.LengthSquared() > 0.25f)
                _throwing.TryThrow(thing, toward.Length() > 3f ? toward.Normalized() * 3f : toward, 6f, recoil: false);
        }
    }

    #endregion

    #region Телеграм-скипетр

    /// <summary>ranged_interact_with_atom(): телепорт в видимую свободную точку до 8 клеток.</summary>
    private void OnTeleportRod(Entity<ImperialTeleportRodComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var target = args.ClickLocation;
        if (!target.TryDistance(EntityManager, Transform(user).Coordinates, out var distance) || distance > ent.Comp.Range)
        {
            _popup.PopupEntity(Loc.GetString("imperial-telerod-too-far"), user, user);
            return;
        }

        if (_transform.GetGrid(target) is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid)
            && _maps.TryGetTileRef(gridUid, grid, _maps.LocalToTile(gridUid, grid, target), out var tile)
            && _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
        {
            _popup.PopupEntity(Loc.GetString("imperial-telerod-obstructed"), user, user);
            return;
        }

        args.Handled = true;
        var start = Transform(user).Coordinates;
        var gifted = HasComp<ImperialMagicallyGiftedComponent>(user);

        // Не маг: неточность до двух клеток.
        if (!gifted)
            target = target.Offset(_random.NextVector2(2f));

        Spawn("ImperialTeleportFluxEffect", start);
        _transform.SetCoordinates(user, target);
        _transform.AttachToGridOrMap(user);
        Spawn("ImperialTeleportFluxEffect", Transform(user).Coordinates);
        _audio.PlayPvs(TeleportSound, user, AudioParams.Default.WithPitchScale(0.5f));

        // teleport_flux: повторный прыжок в течение 6 секунд — 15 урона.
        var flux = EnsureComp<ImperialTeleportFluxComponent>(user);
        if (_timing.CurTime < flux.End)
        {
            _popup.PopupEntity(Loc.GetString("imperial-telerod-too-soon"), user, user, Content.Shared.Popups.PopupType.MediumCaution);
            _damage.TryChangeDamage(user, new DamageSpecifier { DamageDict = { ["Blunt"] = 15 } }, true);
        }

        flux.End = _timing.CurTime + TimeSpan.FromSeconds(6);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateWhistles();
        UpdateVendors();
    }
}

/// <summary>Постоянное «рентгеновское зрение» от покупки шара предсказаний.</summary>
[RegisterComponent]
public sealed partial class ImperialPermanentXrayComponent : Component;
