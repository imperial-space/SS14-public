using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared.Atmos.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Voidwalker;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>/mob/living/basic/voidwalker (и /sunwalker).</summary>
[RegisterComponent]
public sealed partial class ImperialVoidwalkerComponent : Component
{
    /// <summary>can_do_abductions: у гелиарха нет.</summary>
    [DataField]
    public bool CanAbduct = true;

    /// <summary>Гелиарх: поджигает, оставляет огонь, жжёт глаза.</summary>
    [DataField]
    public bool Sunwalker;

    [DataField]
    public TimeSpan KidnapTime = TimeSpan.FromSeconds(6);

    /// <summary>conversions_remaining.</summary>
    [DataField]
    public int ConversionsRemaining = 2;

    [DataField]
    public TimeSpan ConversionCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Урон по не-людям (rclick_damage_type = BRUTE).</summary>
    [DataField]
    public DamageSpecifier ObjectDamage = new() { DamageDict = { ["Blunt"] = 13 } };

    /// <summary>charge_damage.</summary>
    [DataField]
    public float DashDamage = 20f;

    [DataField]
    public EntProtoId Loot = "ImperialCosmicSkull";

    [ViewVariables]
    public bool Kidnapping;

    [ViewVariables]
    public TimeSpan NextConversion;

    /// <summary>space_camo: после боя маскировки нет 5 секунд.</summary>
    [ViewVariables]
    public TimeSpan NextCamo;

    [ViewVariables]
    public TimeSpan LastDamaged;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public bool Grounded;

    [ViewVariables]
    public bool Dashing;

    [ViewVariables]
    public bool DiveAttempt;
}

/// <summary>Пустоходец нырнул под станцию (/obj/effect/dummy/phased_mob/space_dive).</summary>
[RegisterComponent]
public sealed partial class ImperialSpaceDivingComponent : Component
{
    [ViewVariables]
    public Dictionary<string, (int Layer, int Mask)> Fixtures = new();

    [ViewVariables]
    public bool Surfacing;
}

/// <summary>/datum/status_effect/void_chomped: 15 секунд жертва не может умереть.</summary>
[RegisterComponent]
public sealed partial class ImperialVoidChompedComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>/obj/effect/decal/cleanable/vomit/nebula: отсюда можно похищать и нырять.</summary>
[RegisterComponent]
public sealed partial class ImperialNebulaVomitComponent : Component;

public sealed class ImperialVoidwalkerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly Content.Shared.StatusEffectNew.StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly ImperialVoidedSystem _voided = default!;
    [Dependency] private readonly ImperialVoidSystem _void = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly EntProtoId SlurredStatus = "StatusEffectSlurred";
    private static readonly EntProtoId ForcedSleepStatus = "StatusEffectForcedSleeping";
    private static readonly SoundSpecifier ShatterSound = new SoundCollectionSpecifier("GlassBreak");
    private static readonly string[] WeakWallMarkers = { "Reinforced", "Plastitanium", "Titanium", "Indestructible", "Shuttle", "Diamond", "Uranium" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialVoidwalkerComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(Content.Server.NPC.HTN.HTNSystem) });
        SubscribeLocalEvent<ImperialVoidwalkerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, UseAttemptEvent>((_, _, args) => args.Cancel());
        SubscribeLocalEvent<ImperialVoidwalkerComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, MobStateChangedEvent>(OnStateChanged);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidKidnapDoAfterEvent>(OnKidnapDoAfter);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, DoAfterAttemptEvent<ImperialVoidKidnapDoAfterEvent>>(OnKidnapAttempt);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidUnsettleActionEvent>(OnUnsettle);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidUnsettleDoAfterEvent>(OnUnsettleDoAfter);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, DoAfterAttemptEvent<ImperialVoidUnsettleDoAfterEvent>>(OnUnsettleAttempt);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidTelepathyActionEvent>(OnTelepathy);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidDashActionEvent>(OnDash);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ThrowDoHitEvent>(OnDashHit);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, LandEvent>(OnDashLand);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidConvertWallActionEvent>(OnConvertWall);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidConvertWallDoAfterEvent>(OnConvertWallDoAfter);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidDiveDoAfterEvent>(OnDiveDoAfter);
        SubscribeLocalEvent<ImperialVoidwalkerComponent, ImperialVoidSurfaceDoAfterEvent>(OnSurfaceDoAfter);
        SubscribeLocalEvent<ImperialNebulaVomitComponent, InteractHandEvent>(OnVomitInteract);

        SubscribeLocalEvent<ImperialSpaceDivingComponent, InteractionAttemptEvent>((_, _, args) => args.Cancelled = true);
        SubscribeLocalEvent<ImperialSpaceDivingComponent, AttackAttemptEvent>((_, _, args) => args.Cancel());
        SubscribeLocalEvent<ImperialSpaceDivingComponent, BeforeDamageChangedEvent>((_, _, args) => args.Cancelled = true);

        SubscribeLocalEvent<ImperialVoidChompedComponent, UpdateMobStateEvent>(OnChompedUpdateState, after: new[] { typeof(MobThresholdSystem) });
    }

    private static readonly string[] Names =
    {
        "Пустоход", "Сквозящий", "Звёздный странник", "Межзвёздник", "Тот-что-между", "Бездонный", "Стеклорождённый", "Шёпот пустоты",
    };

    private void OnMapInit(Entity<ImperialVoidwalkerComponent> ent, ref MapInitEvent args)
    {
        // Без игрока пустоходец не действует (в TG его просто нет, пока не нашёлся призрак).
        EntityManager.System<Content.Server.NPC.Systems.NPCSystem>().SleepNPC(ent);
        // unique_setup(): имя из voidwalker_names.
        if (!ent.Comp.Sunwalker)
            _metaSetName(ent, _random.Pick(Names));
    }

    private void _metaSetName(EntityUid uid, string name)
    {
        EntityManager.System<MetaDataSystem>().SetEntityName(uid, name);
    }

    #region Космос

    /// <summary>home_turf = /turf/open/space: космос — клетка без пола или вне грида (не на планете).</summary>
    public bool IsSpace(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid == null)
            return xform.MapUid is not { } map || !HasComp<BiomeComponent>(map);

        if (_turf.GetTileRef(xform.Coordinates) is not { } tile || _turf.IsSpace(tile))
            return true;

        // В TG решётка лежит поверх космоса, в SS14 это отдельная плитка.
        return _tileDefs[tile.Tile.TypeId].ID == "Lattice";
    }

    public bool OnNebulaVomit(EntityUid uid)
    {
        return _lookup.GetEntitiesInRange<ImperialNebulaVomitComponent>(Transform(uid).Coordinates, 0.5f).Count > 0;
    }

    private bool OnPlanet(EntityUid uid)
    {
        return Transform(uid).MapUid is { } map && HasComp<BiomeComponent>(map);
    }

    #endregion

    #region Ближний бой и похищение

    /// <summary>early_melee_attack(): по людям — удушение и попытка похищения, по прочему — тупой урон.</summary>
    private void OnMeleeHit(Entity<ImperialVoidwalkerComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != ent.Owner)
            return;

        RevealCamo(ent);
        var anyObject = false;
        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<HumanoidProfileComponent>(hit))
            {
                anyObject = true;
                continue;
            }

            if (ent.Comp.Sunwalker)
            {
                // igniter: поджигает тех, кого бьёт.
                if (TryComp<FlammableComponent>(hit, out var flammable))
                {
                    _flammable.AdjustFireStacks(hit, 2f, flammable, ignite: true);
                }
                Spawn("EffectSparks", Transform(hit).Coordinates);
                continue;
            }

            if (!ent.Comp.CanAbduct)
                continue;

            // void_chomped: жертва 15 секунд не может умереть.
            EnsureComp<ImperialVoidChompedComponent>(hit).Until = _timing.CurTime + TimeSpan.FromSeconds(15);
            if (!TryKidnap(ent, hit))
            {
                args.Handled = true;
                continue;
            }

            if (_mobs.IsCritical(hit) && !HasComp<ImperialVoidedComponent>(hit))
            {
                _popup.PopupEntity(Loc.GetString("imperial-voidwalker-in-crit"), hit, ent);
                _stun.TryUpdateStunDuration(hit, TimeSpan.FromSeconds(5));
                args.Handled = true;
            }
        }

        if (anyObject)
            args.BonusDamage += ent.Comp.ObjectDamage;
    }

    /// <summary>try_kidnap(): true — продолжать бить.</summary>
    private bool TryKidnap(Entity<ImperialVoidwalkerComponent> ent, EntityUid victim)
    {
        if (HasComp<ImperialVoidedComponent>(victim))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-already-voided"), victim, ent);
            Spawn("ImperialVoidUnsettleWave", Transform(victim).Coordinates);
            _status.TryUpdateStatusEffectDuration(victim, ForcedSleepStatus, TimeSpan.FromSeconds(30));
            return false;
        }

        if (_mobs.IsDead(victim))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-victim-dead"), victim, ent);
            return false;
        }

        if (_mobs.IsAlive(victim))
            return true;

        if (!IsSpace(victim) && !OnNebulaVomit(victim))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-not-in-space"), victim, ent, PopupType.Medium);
            return false;
        }

        if (!ent.Comp.Kidnapping)
            StartKidnap(ent, victim);
        return false;
    }

    /// <summary>kidnap(): 6 секунд, жертва парализована.</summary>
    private void StartKidnap(Entity<ImperialVoidwalkerComponent> ent, EntityUid victim)
    {
        _stun.TryUpdateParalyzeDuration(victim, ent.Comp.KidnapTime);
        // В невесомости оба дрейфуют — гасим скорость, а движение не прерывает похищение (только расстояние).
        _physics.SetLinearVelocity(victim, Vector2.Zero);
        _physics.SetLinearVelocity(ent, Vector2.Zero);
        ent.Comp.Kidnapping = true;
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.KidnapTime, new ImperialVoidKidnapDoAfterEvent(), ent, victim)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            RequireCanInteract = false,
            DistanceThreshold = 2.5f,
            AttemptFrequency = AttemptFrequency.EveryTick,
        });

        if (!started)
        {
            ent.Comp.Kidnapping = false;
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-kidnap-start", ("victim", victim)), ent, ent, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-kidnap-victim"), victim, victim, PopupType.LargeCaution);
    }

    /// <summary>check_incapacitated(): жертва должна оставаться без сознания.</summary>
    private void OnKidnapAttempt(Entity<ImperialVoidwalkerComponent> ent, ref DoAfterAttemptEvent<ImperialVoidKidnapDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is { } victim && (_mobs.IsAlive(victim) || _mobs.IsDead(victim)))
            args.Cancel();
    }

    private void OnKidnapDoAfter(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidKidnapDoAfterEvent args)
    {
        ent.Comp.Kidnapping = false;
        if (args.Cancelled)
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-kidnap-failed"), ent, ent);
        if (args.Cancelled || args.Handled || args.Target is not { } victim || _mobs.IsAlive(victim) || _mobs.IsDead(victim))
            return;

        args.Handled = true;
        TakeThem(ent, victim);
    }

    /// <summary>take_them(): проклятие пустоты, исцеление и поездка на огоньке.</summary>
    private void TakeThem(Entity<ImperialVoidwalkerComponent> ent, EntityUid victim)
    {
        if (HasComp<HumanoidProfileComponent>(victim))
            _voided.MakeVoided(victim, false);

        Spawn("ImperialVoidUnsettleWave", Transform(victim).Coordinates);
        ent.Comp.ConversionsRemaining++;
        _void.SendToVoid(victim);
        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-kidnap-done", ("victim", victim), ("count", ent.Comp.ConversionsRemaining)), ent, ent, PopupType.Medium);
    }

    /// <summary>NODEATH: пока действует укус, жертва застревает в критическом состоянии.</summary>
    private void OnChompedUpdateState(Entity<ImperialVoidChompedComponent> ent, ref UpdateMobStateEvent args)
    {
        if (args.State == MobState.Dead && _timing.CurTime < ent.Comp.Until)
            args.State = MobState.Critical;
    }

    /// <summary>glass_pacifist: окна не бьёт, а нежно трётся о них.</summary>
    private void OnAttackAttempt(Entity<ImperialVoidwalkerComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Weapon is { } weapon && weapon.Owner != ent.Owner)
        {
            // pick_and_drop_only: предметы можно только носить.
            args.Cancel();
            return;
        }

        if (args.Target is not { } target || !_tag.HasTag(target, WindowTag))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-nuzzles", ("user", ent.Owner), ("window", target)), target);
        Spawn("EffectHearts", Transform(target).Coordinates);
    }

    #endregion

    #region Урон, смерть, регенерация

    private void OnDamaged(Entity<ImperialVoidwalkerComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        ent.Comp.LastDamaged = _timing.CurTime;
        RevealCamo(ent);
        // debris_bleeder: стекло сыплется от ударов.
        var brute = delta.DamageDict.Where(d => d.Key.Id is "Blunt" or "Slash" or "Piercing").Sum(d => d.Value.Float());
        if (brute >= 5f && _random.Prob(0.5f))
            Spawn(_random.Pick(new[] { "ShardGlass", "ShardGlassPlasma", "ShardGlassReinforced" }), Transform(ent).Coordinates);
        if (brute >= 20f)
            _audio.PlayPvs(ShatterSound, ent);
    }

    /// <summary>death(): осколки, космический череп, звон стекла.</summary>
    private void OnStateChanged(Entity<ImperialVoidwalkerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var coords = Transform(ent).Coordinates;
        for (var i = 0; i < _random.Next(4, 7); i++)
        {
            Spawn(_random.Pick(new[] { "ShardGlass", "ShardGlass", "ShardGlassPlasma", "ShardGlassReinforced" }), coords.Offset(_random.NextVector2(0.4f)));
        }

        if (!ent.Comp.Sunwalker)
            Spawn(ent.Comp.Loot, coords);
        _audio.PlayPvs(ShatterSound, coords);
        if (TryComp<ImperialSpaceDivingComponent>(ent, out var diving))
            Surface(ent, diving);
        QueueDel(ent);
    }

    private void OnRefreshSpeed(Entity<ImperialVoidwalkerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // grounded_voidwalker: гравитация вдали от станции тяготит.
        if (ent.Comp.Grounded)
            args.ModifySpeed(0.5f);
    }

    /// <summary>vision_hurting гелиарха: смотреть на него вредно для глаз.</summary>
    private void OnExamined(Entity<ImperialVoidwalkerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Sunwalker && args.Examiner != ent.Owner && TryComp<BlindableComponent>(args.Examiner, out var blindable))
            _blindable.AdjustEyeDamage((args.Examiner, blindable), 1);
    }

    #endregion

    #region Маскировка

    /// <summary>force_exit_camo(): после боя 5 секунд без маскировки.</summary>
    public void RevealCamo(EntityUid uid)
    {
        if (!TryComp<ImperialVoidwalkerComponent>(uid, out var walker))
            return;

        walker.NextCamo = _timing.CurTime + TimeSpan.FromSeconds(5);
        if (TryComp<StealthComponent>(uid, out var stealth) && stealth.Enabled)
            _stealth.SetEnabled(uid, false, stealth);
    }

    #endregion

    #region Ныряние под станцию

    /// <summary>space_dive/bump(): из космоса упёрся в закреплённое — нырнуть за 3 секунды.</summary>
    private void OnCollide(Entity<ImperialVoidwalkerComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.DiveAttempt || HasComp<ImperialSpaceDivingComponent>(ent) || ent.Comp.Dashing || !args.OtherFixture.Hard)
            return;

        var other = args.OtherEntity;
        if (!Transform(other).Anchored || HasComp<Content.Shared.Doors.Components.DoorComponent>(other) || !IsSpace(ent))
            return;

        // Окна пустоходец проходит насквозь, о них не ныряют.
        if (HasComp<ImperialGlassPasserComponent>(ent) && _tag.HasTag(other, WindowTag))
            return;

        ent.Comp.DiveAttempt = true;
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(3), new ImperialVoidDiveDoAfterEvent(), ent, other)
        {
            BreakOnMove = true,
            RequireCanInteract = false,
            DistanceThreshold = 2f,
        });

        if (!started)
            ent.Comp.DiveAttempt = false;
    }

    private void OnDiveDoAfter(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidDiveDoAfterEvent args)
    {
        ent.Comp.DiveAttempt = false;
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        Dive(ent, Transform(target).Coordinates);
    }

    /// <summary>space_dive/try_enter(): из туманной рвоты тоже можно нырнуть (за секунду).</summary>
    private void OnVomitInteract(Entity<ImperialNebulaVomitComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !TryComp<ImperialVoidwalkerComponent>(args.User, out var walker) || HasComp<ImperialSpaceDivingComponent>(args.User))
            return;

        args.Handled = true;
        walker.DiveAttempt = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(1), new ImperialVoidDiveDoAfterEvent(), args.User, ent)
        {
            BreakOnMove = true,
            RequireCanInteract = false,
        });
    }

    /// <summary>dive(): невидим, бесплотен, неуязвим; руки пусты.</summary>
    public void Dive(EntityUid uid, EntityCoordinates where)
    {
        foreach (var held in _hands.EnumerateHeld(uid).ToList())
        {
            _hands.TryDrop(uid, held);
        }

        foreach (var vomit in _lookup.GetEntitiesInRange<ImperialNebulaVomitComponent>(where, 0.5f))
        {
            Spawn("ImperialVoidUnsettleWave", Transform(vomit).Coordinates);
            QueueDel(vomit);
        }

        _transform.SetCoordinates(uid, where);
        _physics.SetLinearVelocity(uid, Vector2.Zero);
        var diving = EnsureComp<ImperialSpaceDivingComponent>(uid);
        _visibility.SetLayer(uid, (ushort) VisibilityFlags.Ghost);
        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetVisibilityMask(uid, eye.VisibilityMask | (int) VisibilityFlags.Ghost, eye);

        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                diving.Fixtures[id] = (fixture.CollisionLayer, fixture.CollisionMask);
                _physics.SetCollisionLayer(uid, id, fixture, 0, fixtures);
                _physics.SetCollisionMask(uid, id, fixture, 0, fixtures);
            }
        }

        if (TryComp<StealthComponent>(uid, out var stealth))
            _stealth.SetEnabled(uid, false, stealth);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/ethereal_exit.ogg"), where);
    }

    private void OnSurfaceDoAfter(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidSurfaceDoAfterEvent args)
    {
        if (!TryComp<ImperialSpaceDivingComponent>(ent, out var diving))
            return;

        diving.Surfacing = false;
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (OnNebulaVomit(ent))
        {
            foreach (var vomit in _lookup.GetEntitiesInRange<ImperialNebulaVomitComponent>(Transform(ent).Coordinates, 0.5f))
            {
                Spawn("ImperialVoidUnsettleWave", Transform(vomit).Coordinates);
            }
        }

        Surface(ent, diving);
    }

    /// <summary>surface(): вынырнуть в космос или из туманной рвоты.</summary>
    public void Surface(EntityUid uid, ImperialSpaceDivingComponent diving)
    {
        _visibility.SetLayer(uid, (ushort) VisibilityFlags.Normal);
        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetVisibilityMask(uid, eye.VisibilityMask & ~(int) VisibilityFlags.Ghost, eye);

        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                if (!diving.Fixtures.TryGetValue(id, out var saved))
                    continue;

                _physics.SetCollisionLayer(uid, id, fixture, saved.Layer, fixtures);
                _physics.SetCollisionMask(uid, id, fixture, saved.Mask, fixtures);
            }
        }

        RemComp<ImperialSpaceDivingComponent>(uid);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/ethereal_exit.ogg"), uid);
    }

    #endregion

    #region Способности

    /// <summary>unsettle: пристально смотреть 6 секунд на того, кто тебя видит.</summary>
    private void OnUnsettle(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidUnsettleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<MobStateComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-unsettle-invalid"), ent, ent);
            return;
        }

        if (!IsStaring(ent, args.Target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-unsettle-cant-see"), ent, ent);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-unsettle-start"), ent, ent);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(6), new ImperialVoidUnsettleDoAfterEvent(), ent, args.Target)
        {
            BreakOnMove = false,
            RequireCanInteract = false,
            DistanceThreshold = 9f,
            AttemptFrequency = AttemptFrequency.EveryTick,
            Hidden = true,
        });
    }

    /// <summary>check_if_staring(): цель не слепа и видит пустоходца.</summary>
    private bool IsStaring(EntityUid walker, EntityUid target)
    {
        if (TryComp<BlindableComponent>(target, out var blindable) && blindable.IsBlind)
            return false;

        return _examine.InRangeUnOccluded(target, walker, 9f);
    }

    private void OnUnsettleAttempt(Entity<ImperialVoidwalkerComponent> ent, ref DoAfterAttemptEvent<ImperialVoidUnsettleDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is { } target && !IsStaring(ent, target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-unsettle-broken"), ent, ent);
            args.Cancel();
        }
    }

    /// <summary>spookify(): 2 секунды паралича, 80 урона выносливости, невнятная речь, крик.</summary>
    private void OnUnsettleDoAfter(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidUnsettleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(2));
        _stamina.TakeStaminaDamage(target, 80f);
        _status.TryUpdateStatusEffectDuration(target, SlurredStatus, TimeSpan.FromSeconds(10));
        EntityManager.System<Content.Server.Chat.Systems.ChatSystem>().TryEmoteWithChat(target, "Scream");
        Spawn("ImperialVoidUnsettleWave", Transform(ent).Coordinates);
        Spawn("ImperialVoidUnsettleWave", Transform(target).Coordinates);
        RevealCamo(ent);
    }

    /// <summary>telepathy/voidwalker: передать мысль выбранному существу.</summary>
    private void OnTelepathy(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidTelepathyActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var target = args.Target;
        _dialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("imperial-voidwalker-telepathy-title"),
            Loc.GetString("imperial-voidwalker-telepathy-prompt"), message =>
            {
                message = message.Trim();
                if (message.Length == 0 || TerminatingOrDeleted(target))
                    return;

                SendTelepathy(ent, target, message);
            });
    }

    private void SendTelepathy(EntityUid walker, EntityUid target, string message)
    {
        foreach (var (who, key) in new[] { (target, "imperial-voidwalker-telepathy-received"), (walker, "imperial-voidwalker-telepathy-sent") })
        {
            if (!_mind.TryGetMind(who, out _, out var mind) || mind.UserId is not { } userId
                || !_players.TryGetSessionById(userId, out var session))
                continue;

            var text = Loc.GetString(key, ("message", message), ("target", Name(target)));
            var plain = Robust.Shared.Utility.FormattedMessage.RemoveMarkupOrThrow(text);
            _chatManager.ChatMessageToOne(ChatChannel.Server, plain, text, default, false, session.Channel);
        }
    }

    /// <summary>charge/voidwalker: рывок только в космос.</summary>
    private void OnDash(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidDashActionEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.Sunwalker && _turf.GetTileRef(args.Target) is { } tile && !_turf.IsSpace(tile))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-dash-space-only"), ent, ent);
            return;
        }

        args.Handled = true;
        ent.Comp.Dashing = true;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/curse1.ogg"), ent);
        _throwing.TryThrow(ent, args.Target, 15f, ent, 0f, compensateFriction: true);
    }

    private void OnDashHit(Entity<ImperialVoidwalkerComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.Dashing || !HasComp<MobStateComponent>(args.Target))
            return;

        var damage = new DamageSpecifier { DamageDict = { [ent.Comp.Sunwalker ? "Heat" : "Blunt"] = ent.Comp.DashDamage } };
        _damage.TryChangeDamage(args.Target, damage, origin: ent);
        _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(1), true);
    }

    /// <summary>sunwalker/charge_end(): огонь вокруг места приземления.</summary>
    private void OnDashLand(Entity<ImperialVoidwalkerComponent> ent, ref LandEvent args)
    {
        if (!ent.Comp.Dashing)
            return;

        ent.Comp.Dashing = false;
        if (ent.Comp.Sunwalker)
            HotspotAround(ent, 1);
    }

    private void HotspotAround(EntityUid uid, int range)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid)
            return;

        var center = _transform.GetGridTilePositionOrDefault((uid, xform));
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                _atmos.HotspotExpose(grid, center + new Vector2i(x, y), 1000f, 100f, uid, true);
            }
        }
    }

    /// <summary>try_convert_wall(): 8 секунд — и стена с соседями становится окнами пустоты.</summary>
    private void OnConvertWall(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidConvertWallActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.ConversionsRemaining <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-wall-need-kidnaps"), ent, ent);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextConversion)
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-wall-cooldown",
                ("time", (int) Math.Ceiling((ent.Comp.NextConversion - _timing.CurTime).TotalSeconds))), ent, ent);
            return;
        }

        if (!IsConvertibleWall(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-wall-too-strong"), ent, ent);
            return;
        }

        args.Handled = true;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/blind.ogg"), args.Target);
        Spawn("ImperialVoidUnsettleWave", Transform(args.Target).Coordinates);
        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-wall-opening"), ent, ent);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(8), new ImperialVoidConvertWallDoAfterEvent(), ent, args.Target)
        {
            BreakOnMove = true,
            RequireCanInteract = false,
            Hidden = true,
        });
    }

    /// <summary>check_wall_validity(): прочные стены (hardness &lt; 40) не поддаются.</summary>
    private bool IsConvertibleWall(EntityUid wall)
    {
        if (!_tag.HasTag(wall, WallTag) || !Transform(wall).Anchored)
            return false;

        var id = MetaData(wall).EntityPrototype?.ID ?? string.Empty;
        return !WeakWallMarkers.Any(id.Contains);
    }

    private void OnConvertWallDoAfter(Entity<ImperialVoidwalkerComponent> ent, ref ImperialVoidConvertWallDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } wall || ent.Comp.ConversionsRemaining <= 0 || TerminatingOrDeleted(wall))
            return;

        args.Handled = true;
        var walls = new List<EntityUid> { wall };
        foreach (var near in _lookup.GetEntitiesInRange(Transform(wall).Coordinates, 1.5f, LookupFlags.Static))
        {
            if (near != wall && IsConvertibleWall(near))
                walls.Add(near);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/blind.ogg"), wall);
        foreach (var target in walls)
        {
            var coords = Transform(target).Coordinates;
            Spawn("ImperialVoidUnsettleWave", coords);
            QueueDel(target);
            Spawn("Grille", coords);
            Spawn("ImperialVoidWindow", coords);
        }

        ent.Comp.ConversionsRemaining--;
        ent.Comp.NextConversion = _timing.CurTime + ent.Comp.ConversionCooldown;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<ImperialVoidwalkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var walker, out var xform))
        {
            var inSpace = IsSpace(uid);
            var diving = TryComp<ImperialSpaceDivingComponent>(uid, out var dive);

            // space_camo: в космосе почти невидим.
            if (!walker.Sunwalker && TryComp<StealthComponent>(uid, out var stealth))
            {
                var camo = inSpace && !diving && now >= walker.NextCamo;
                if (stealth.Enabled != camo)
                    _stealth.SetEnabled(uid, camo, stealth);
            }

            // move_check(): нырнувший всплывает над космосом или туманной рвотой.
            if (diving && dive != null && !dive.Surfacing && (inSpace || OnNebulaVomit(uid)))
            {
                dive.Surfacing = true;
                var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(1), new ImperialVoidSurfaceDoAfterEvent(), uid)
                {
                    BreakOnMove = true,
                    RequireCanInteract = false,
                });
                if (!started)
                    dive.Surfacing = false;
            }

            if (now < walker.NextTick)
                continue;

            walker.NextTick = now + TimeSpan.FromSeconds(1);

            // regenerator: 2 ед. в секунду в космосе, если 6 секунд не били.
            if (inSpace && now > walker.LastDamaged + TimeSpan.FromSeconds(6) && TryComp<DamageableComponent>(uid, out var damageable)
                && _damage.GetTotalDamage((uid, damageable)) > 0)
            {
                _damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Blunt"] = -2, ["Slash"] = -2, ["Piercing"] = -2, ["Heat"] = -2 } }, true);
            }

            // planet_allergy: гравитация планет давит.
            if (OnPlanet(uid) && !_gravity.IsWeightless(uid))
                _damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);

            var grounded = !_gravity.IsWeightless(uid) && EntityManager.System<Content.Server.Station.Systems.StationSystem>().GetOwningStation(uid) == null && !inSpace;
            if (grounded != walker.Grounded)
            {
                walker.Grounded = grounded;
                _speed.RefreshMovementSpeedModifiers(uid);
            }

            // sunwalker/process(): жар вокруг и иногда огонь.
            if (walker.Sunwalker && !diving && xform.GridUid is { } grid)
            {
                var tile = _transform.GetGridTilePositionOrDefault((uid, xform));
                _atmos.HotspotExpose(grid, tile, 1000f, 100f, uid, _random.Prob(0.02f));
            }
        }

        var chomped = EntityQueryEnumerator<ImperialVoidChompedComponent>();
        while (chomped.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.Until)
                RemCompDeferred<ImperialVoidChompedComponent>(uid);
        }
    }
}
