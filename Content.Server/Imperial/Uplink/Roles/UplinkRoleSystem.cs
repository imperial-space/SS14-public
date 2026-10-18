using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.Genetics;
using Content.Server.Polymorph.Systems;
using Content.Server.Silicons.Laws;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Shared.Body;
using Content.Shared.Coordinates.Helpers;
using Content.Server.Body;
using Content.Shared.Clumsy;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Uplink.Roles;
using Content.Shared.Imperial.Uplink.Stealth;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Singularity.Components;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.Roles;

/// <summary>
/// Позиции аплинка TG для профессий (uplink_items/job.dm).
/// </summary>
public sealed partial class UplinkRoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DnaInjectorSystem _dnaInjector = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly VisualBodySystem _visualBody = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly Content.Shared.Roles.Jobs.SharedJobSystem _jobs = default!;
    [Dependency] private readonly Content.Shared.Mind.SharedMindSystem _mind = default!;
    [Dependency] private readonly Robust.Shared.Map.IMapManager _mapManager = default!;

    private static readonly SoundSpecifier HonkSound = new SoundPathSpecifier("/Audio/Items/bikehorn.ogg");
    private static readonly SoundSpecifier RevolverSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/revolver.ogg");
    private static readonly SoundSpecifier SnapSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
    private static readonly SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");

    private const string BureaucraticErrorListing = "ImperialUplinkBureaucraticError";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkClownPinComponent, AfterInteractEvent>(OnClownPinInteract);
        SubscribeLocalEvent<UplinkClownPinnedComponent, ShotAttemptedEvent>(OnClownPinnedShot);
        SubscribeLocalEvent<UplinkClownPinnedComponent, ExaminedEvent>(OnClownPinnedExamined);
        SubscribeLocalEvent<UplinkReverseGunComponent, ShotAttemptedEvent>(OnReverseShot);

        SubscribeLocalEvent<UplinkReverseBearTrapComponent, AfterInteractEvent>(OnBearTrapInteract);
        SubscribeLocalEvent<UplinkReverseBearTrapComponent, UplinkBearTrapForceDoAfterEvent>(OnBearTrapForced);
        SubscribeLocalEvent<UplinkReverseBearTrapComponent, GetVerbsEvent<AlternativeVerb>>(OnBearTrapVerbs);
        SubscribeLocalEvent<UplinkReverseBearTrapComponent, UplinkBearTrapStruggleDoAfterEvent>(OnBearTrapStruggle);

        SubscribeLocalEvent<UplinkHookProjectileComponent, ProjectileHitEvent>(OnHookHit);

        SubscribeLocalEvent<UplinkPolymorphInjectorComponent, UseInHandEvent>(OnPolymorphUse);
        SubscribeLocalEvent<UplinkPolymorphInjectorComponent, AfterInteractEvent>(OnPolymorphInteract);

        SubscribeLocalEvent<UplinkBrainwashDeviceComponent, AfterInteractEvent>(OnBrainwashInteract);
        SubscribeLocalEvent<UplinkBrainwashDeviceComponent, UplinkBrainwashDoAfterEvent>(OnBrainwashDoAfter);

        SubscribeLocalEvent<UplinkPlasticSurgeryComponent, AfterInteractEvent>(OnSurgeryInteract);
        SubscribeLocalEvent<UplinkPlasticSurgeryComponent, UplinkPlasticSurgeryDoAfterEvent>(OnSurgeryDoAfter);
        SubscribeLocalEvent<UplinkPlasticSurgeryComponent, ExaminedEvent>(OnSurgeryExamined);

        SubscribeLocalEvent<UplinkArmGunImplantComponent, UplinkToggleArmGunEvent>(OnToggleArmGun);

        SubscribeLocalEvent<UplinkMimeryBookComponent, UseInHandEvent>(OnMimeryUse);
        SubscribeLocalEvent<UplinkMimeWallEvent>(OnMimeWall);
        SubscribeLocalEvent<UplinkFingerGunEvent>(OnFingerGun);

        SubscribeLocalEvent<UplinkSyndieMmiComponent, EntGotInsertedIntoContainerMessage>(OnSyndieMmiInserted);

        SubscribeLocalEvent<UplinkSpiderInjectorComponent, AfterInteractEvent>(OnSpiderInjectorInteract);
        SubscribeLocalEvent<UplinkEvilSeedComponent, UseInHandEvent>(OnEvilSeedUse);
        SubscribeLocalEvent<UplinkBeeSmokerComponent, UseInHandEvent>(OnBeeSmokerUse);
        SubscribeLocalEvent<UplinkHauntedEightballComponent, UseInHandEvent>(OnEightballUse);

        SubscribeLocalEvent<UplinkBriefcaseGunComponent, ShotAttemptedEvent>(OnBriefcaseShot);
        SubscribeLocalEvent<UplinkDnaSyringeGunComponent, AmmoShotEvent>(OnDnaGunShot);
        SubscribeLocalEvent<UplinkDnaDartComponent, ThrowDoHitEvent>(OnDnaDartHit);

        SubscribeLocalEvent<UplinkEmitterDiskComponent, AfterInteractEvent>(OnEmitterDiskInteract);

        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnBuyFinished);

        InitializeHisGrace();
        InitializeClownCar();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var traps = EntityQueryEnumerator<UplinkReverseBearTrapComponent>();
        while (traps.MoveNext(out var uid, out var trap))
        {
            if (trap.KillAt is not { } killAt)
                continue;

            if (now >= trap.NextBeep)
            {
                var left = (killAt - now).TotalSeconds;
                trap.NextBeep = now + TimeSpan.FromSeconds(Math.Max(0.5, left - 5) > 2 ? 2 : 0.5);
                _audio.PlayPvs(BeepSound, uid, AudioParams.Default.WithVolume(-6));
            }

            if (now < killAt)
                continue;

            trap.KillAt = null;
            SnapTrap((uid, trap));
        }

        var seeds = EntityQueryEnumerator<UplinkEvilSeedComponent>();
        while (seeds.MoveNext(out var uid, out var seed))
        {
            if (seed.GrowAt is not { } growAt || now < growAt)
                continue;

            seed.GrowAt = null;
            Spawn(seed.Mob, Transform(uid).Coordinates);
            QueueDel(uid);
        }

        var balls = EntityQueryEnumerator<UplinkHauntedEightballComponent>();
        while (balls.MoveNext(out var uid, out var ball))
        {
            if (ball.AnswerAt is not { } answerAt || now < answerAt)
                continue;

            ball.AnswerAt = null;
            var answers = Loc.GetString("uplink-eightball-answers").Split('|');
            _popup.PopupEntity(_random.Pick(answers).Trim(), uid, PopupType.Medium);
        }

        UpdateHisGrace(now);
    }

    #region Клоунские бойки и обратный револьвер

    private bool IsClown(EntityUid user)
    {
        if (HasComp<ClumsyComponent>(user))
            return true;

        return _mind.TryGetMind(user, out var mindId, out _) && _jobs.MindTryGetJobId(mindId, out var job) && job == "Clown";
    }

    private void OnClownPinInteract(Entity<UplinkClownPinComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<GunComponent>(target))
            return;

        args.Handled = true;
        var pinned = EnsureComp<UplinkClownPinnedComponent>(target);
        pinned.SelfDestruct = ent.Comp.SelfDestruct;
        _popup.PopupEntity(Loc.GetString("uplink-clownpin-insert", ("gun", target)), args.User, args.User);
        QueueDel(ent);
    }

    private void OnClownPinnedExamined(Entity<UplinkClownPinnedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-clownpin-examine"));
    }

    /// <summary>pin_auth: каждый выстрел гудит; не-клоун не может выстрелить, а с самоуничтожением — взрывается.</summary>
    private void OnClownPinnedShot(Entity<UplinkClownPinnedComponent> ent, ref ShotAttemptedEvent args)
    {
        _audio.PlayPvs(HonkSound, ent);
        if (IsClown(args.User))
            return;

        args.Cancel();
        if (!ent.Comp.SelfDestruct)
        {
            _popup.PopupEntity(Loc.GetString("uplink-clownpin-deny"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("uplink-clownpin-explode", ("gun", ent.Owner)), args.User, PopupType.LargeCaution);
        _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "Default", 30f, 6f, 6f, ent, canCreateVacuum: false);
        QueueDel(ent);
    }

    /// <summary>reverse_revolver: всегда стреляет в стрелка, если он не клоун.</summary>
    private void OnReverseShot(Entity<UplinkReverseGunComponent> ent, ref ShotAttemptedEvent args)
    {
        if (IsClown(args.User))
            return;

        args.Cancel();
        var damage = new DamageSpecifier();
        damage.DamageDict["Piercing"] = ent.Comp.Damage;
        _damageable.TryChangeDamage(args.User, damage, origin: args.User);
        _audio.PlayPvs(RevolverSound, ent);
        _popup.PopupEntity(Loc.GetString("uplink-reverse-revolver", ("user", args.User)), args.User, PopupType.LargeCaution);
    }

    #endregion

    #region Обратный медвежий капкан

    private void OnBearTrapInteract(Entity<UplinkReverseBearTrapComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        args.Handled = true;
        if (_inventory.TryGetSlotEntity(target, "head", out _))
        {
            _popup.PopupEntity(Loc.GetString("uplink-beartrap-headgear"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("uplink-beartrap-forcing", ("user", args.User), ("target", target)), target, PopupType.MediumCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new UplinkBearTrapForceDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnBearTrapForced(Entity<UplinkReverseBearTrapComponent> ent, ref UplinkBearTrapForceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (_inventory.TryGetSlotEntity(target, "head", out _) || !_inventory.TryEquip(args.User, target, ent, "head", true, true))
            return;

        EnsureComp<UnremoveableComponent>(ent);
        ent.Comp.EscapeChance = 0;
        ent.Comp.KillAt = _timing.CurTime + ent.Comp.Countdown;
        ent.Comp.NextBeep = _timing.CurTime;
        _popup.PopupEntity(Loc.GetString("uplink-beartrap-locked", ("user", args.User), ("target", target)), target, PopupType.LargeCaution);
    }

    private void OnBearTrapVerbs(Entity<UplinkReverseBearTrapComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (ent.Comp.KillAt == null || Transform(ent).ParentUid != args.User)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-beartrap-struggle-verb"),
            Act = () =>
            {
                _popup.PopupEntity(Loc.GetString("uplink-beartrap-struggle"), user, user, PopupType.Medium);
                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(2), new UplinkBearTrapStruggleDoAfterEvent(), ent, used: ent)
                {
                    BreakOnMove = true,
                });
            },
        });
    }

    private void OnBearTrapStruggle(Entity<UplinkReverseBearTrapComponent> ent, ref UplinkBearTrapStruggleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.KillAt == null)
            return;

        args.Handled = true;
        if (!_random.Prob(ent.Comp.EscapeChance / 100f))
        {
            ent.Comp.EscapeChance++;
            _popup.PopupEntity(Loc.GetString("uplink-beartrap-budge"), args.User, args.User);
            return;
        }

        RemComp<UnremoveableComponent>(ent);
        _popup.PopupEntity(Loc.GetString("uplink-beartrap-free"), args.User, args.User, PopupType.Medium);
    }

    /// <summary>snap: челюсти раскрываются, разрывая голову носителя.</summary>
    private void SnapTrap(Entity<UplinkReverseBearTrapComponent> ent)
    {
        RemComp<UnremoveableComponent>(ent);
        _audio.PlayPvs(SnapSound, ent);
        var victim = Transform(ent).ParentUid;
        if (!_inventory.TryGetSlotEntity(victim, "head", out var head) || head != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("uplink-beartrap-snap-empty"), ent, PopupType.Medium);
            return;
        }

        _popup.PopupEntity(Loc.GetString("uplink-beartrap-snap", ("target", victim)), victim, PopupType.LargeCaution);
        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = 400;
        _damageable.TryChangeDamage(victim, damage, true);
    }

    #endregion

    #region Мясной крюк

    /// <summary>hook/on_hit: цель притягивается к стрелку.</summary>
    private void OnHookHit(Entity<UplinkHookProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter || Transform(args.Target).Anchored)
            return;

        var dir = _transform.GetWorldPosition(shooter) - _transform.GetWorldPosition(args.Target);
        var length = dir.Length();
        if (length < 1f)
            return;

        _throwing.TryThrow(args.Target, dir * ((length - 1f) / length), 10f, shooter, recoil: false);
        _popup.PopupEntity(Loc.GetString("uplink-hook-snag", ("target", args.Target)), args.Target, PopupType.MediumCaution);
    }

    #endregion

    #region Сыворотка магиллитиса

    private void OnPolymorphUse(Entity<UplinkPolymorphInjectorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ApplyPolymorph(ent, args.User);
    }

    private void OnPolymorphInteract(Entity<UplinkPolymorphInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        args.Handled = true;
        ApplyPolymorph(ent, target);
    }

    private void ApplyPolymorph(Entity<UplinkPolymorphInjectorComponent> ent, EntityUid target)
    {
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/hypospray.ogg"), target);
        _polymorph.PolymorphEntity(target, ent.Comp.Polymorph);
        QueueDel(ent);
    }

    #endregion

    #region Промывка мозгов и пластическая хирургия

    private static bool IsOperable(EntityManager entMan, EntityUid target)
    {
        return entMan.HasComponent<Content.Shared.Bed.Sleep.SleepingComponent>(target)
               || entMan.TryGetComponent<MobStateComponent>(target, out var state) && state.CurrentState != Content.Shared.Mobs.MobState.Alive;
    }

    private void OnBrainwashInteract(Entity<UplinkBrainwashDeviceComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        args.Handled = true;
        if (!IsOperable(EntityManager, target))
        {
            _popup.PopupEntity(Loc.GetString("uplink-surgery-awake"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(10), new UplinkBrainwashDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    /// <summary>surgery/advanced/brainwashing: жертва получает навязанную цель.</summary>
    private void OnBrainwashDoAfter(Entity<UplinkBrainwashDeviceComponent> ent, ref UplinkBrainwashDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !_player.TryGetSessionByEntity(args.User, out var session))
            return;

        args.Handled = true;
        _dialog.OpenDialog<LongString>(session, Loc.GetString("uplink-brainwash-title"), Loc.GetString("uplink-brainwash-prompt"), objective =>
        {
            var hypno = EnsureComp<UplinkHypnotizedComponent>(target);
            hypno.Phrase = Loc.GetString("uplink-brainwash-objective", ("objective", objective.String));
            hypno.NextReminder = _timing.CurTime;
        });
    }

    private void OnSurgeryExamined(Entity<UplinkPlasticSurgeryComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.TemplateName == null
            ? Loc.GetString("uplink-plastic-empty")
            : Loc.GetString("uplink-plastic-template", ("name", ent.Comp.TemplateName)));
    }

    /// <summary>
    /// advanced_plastic_surgery: сначала «сфотографировать» образец (клик по нему), затем оперировать спящую цель.
    /// </summary>
    private void OnSurgeryInteract(Entity<UplinkPlasticSurgeryComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        args.Handled = true;
        if (ent.Comp.Template == null || !IsOperable(EntityManager, target))
        {
            ent.Comp.Template = target;
            ent.Comp.TemplateName = Name(target);
            _popup.PopupEntity(Loc.GetString("uplink-plastic-scanned", ("name", ent.Comp.TemplateName)), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(10), new UplinkPlasticSurgeryDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSurgeryDoAfter(Entity<UplinkPlasticSurgeryComponent> ent, ref UplinkPlasticSurgeryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || ent.Comp.Template is not { } template || TerminatingOrDeleted(template))
            return;

        args.Handled = true;
        _visualBody.CopyAppearanceFrom(template, target);
        if (ent.Comp.TemplateName != null)
            _metaData.SetEntityName(target, ent.Comp.TemplateName);

        _popup.PopupEntity(Loc.GetString("uplink-plastic-done"), args.User, args.User);
    }

    #endregion

    #region Лазерная рука

    private void OnToggleArmGun(Entity<UplinkArmGunImplantComponent> ent, ref UplinkToggleArmGunEvent args)
    {
        args.Handled = true;
        var user = args.Performer;
        if (ent.Comp.Spawned is { } spawned && !TerminatingOrDeleted(spawned))
        {
            QueueDel(spawned);
            ent.Comp.Spawned = null;
            return;
        }

        var gun = Spawn(ent.Comp.Gun, Transform(user).Coordinates);
        if (!_hands.TryPickupAnyHand(user, gun))
        {
            QueueDel(gun);
            _popup.PopupEntity(Loc.GetString("uplink-armgun-no-hand"), user, user);
            return;
        }

        ent.Comp.Spawned = gun;
    }

    #endregion

    #region Мимика

    private void OnMimeryUse(Entity<UplinkMimeryBookComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!HasComp<Content.Shared.Abilities.Mime.MimePowersComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("uplink-mimery-not-mime"), args.User, args.User);
            return;
        }

        EntityUid? action = null;
        _actions.AddAction(args.User, ref action, ent.Comp.Action);
        _popup.PopupEntity(Loc.GetString("uplink-mimery-learn"), args.User, args.User, PopupType.Medium);
        QueueDel(ent);
    }

    [Dependency] private readonly Content.Shared.Actions.SharedActionsSystem _actions = default!;

    /// <summary>mime/speak/invisible_blockade: стена 3×1 поперёк взгляда.</summary>
    private void OnMimeWall(UplinkMimeWallEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var xform = Transform(user);
        var dir = _transform.GetWorldRotation(xform).GetCardinalDir().ToVec();
        var side = new Vector2(-dir.Y, dir.X);
        var center = xform.Coordinates.Offset(dir);
        for (var i = -1; i <= 1; i++)
        {
            Spawn("WallInvisible", center.Offset(side * i).SnapToGrid(EntityManager, _mapManager));
        }

        _popup.PopupEntity(Loc.GetString("uplink-mimery-wall", ("user", user)), user, PopupType.Small);
        args.Handled = true;
    }

    /// <summary>mime/finger_guns: в руке появляется «пистолет из пальцев» на три выстрела.</summary>
    private void OnFingerGun(UplinkFingerGunEvent args)
    {
        if (args.Handled)
            return;

        var gun = Spawn("ImperialMimeFingerGun", Transform(args.Performer).Coordinates);
        if (!_hands.TryPickupAnyHand(args.Performer, gun))
        {
            QueueDel(gun);
            return;
        }

        args.Handled = true;
    }

    #endregion

    #region ИИМ Синдиката

    private void OnSyndieMmiInserted(Entity<UplinkSyndieMmiComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var chassis = args.Container.Owner;
        if (!HasComp<BorgChassisComponent>(chassis))
            return;

        var lawset = _laws.GetLawset(ent.Comp.Laws);
        _laws.SetLaws(lawset.Laws, chassis);
    }

    #endregion

    #region Австраликус, злой росток, дымарь, восьмёрка

    private void OnSpiderInjectorInteract(Entity<UplinkSpiderInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target
            || MetaData(target).EntityPrototype?.ID != "SlimeExtractGold")
            return;

        args.Handled = true;
        var coords = Transform(target).Coordinates;
        for (var i = 0; i < ent.Comp.Amount; i++)
        {
            Spawn(ent.Comp.Spider, coords);
        }

        _popup.PopupEntity(Loc.GetString("uplink-spider-injector"), target, PopupType.MediumCaution);
        QueueDel(target);
        QueueDel(ent);
    }

    private void OnEvilSeedUse(Entity<UplinkEvilSeedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.GrowAt != null)
            return;

        args.Handled = true;
        _transform.DropNextTo(ent.Owner, args.User);
        ent.Comp.GrowAt = _timing.CurTime + ent.Comp.GrowTime;
        _popup.PopupEntity(Loc.GetString("uplink-evil-seed-plant"), ent, args.User);
    }

    private void OnBeeSmokerUse(Entity<UplinkBeeSmokerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var count = 0;
        foreach (var bee in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(_transform.GetMapCoordinates(ent), ent.Comp.Range))
        {
            var proto = MetaData(bee).EntityPrototype?.ID;
            if (proto == null || !proto.Contains("Bee"))
                continue;

            _faction.ClearFactions((bee.Owner, bee.Comp));
            _faction.AddFaction((bee.Owner, bee.Comp), new Robust.Shared.Prototypes.ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype>("Syndicate"));
            count++;
        }

        _popup.PopupEntity(Loc.GetString("uplink-bee-smoker", ("count", count)), ent, args.User);
    }

    private void OnEightballUse(Entity<UplinkHauntedEightballComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.AnswerAt != null)
            return;

        args.Handled = true;
        ent.Comp.Asker = args.User;
        ent.Comp.AnswerAt = _timing.CurTime + TimeSpan.FromSeconds(5);
        _popup.PopupEntity(Loc.GetString("uplink-eightball-shake"), ent, PopupType.Small);
    }

    #endregion

    #region Кейс-пистолет и ДНК-шприцемёт

    /// <summary>briefcase/gun: «выстрел» кейсом нажимает на спуск оружия внутри.</summary>
    private void OnBriefcaseShot(Entity<UplinkBriefcaseGunComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
        if (!_container.TryGetContainer(ent, "storagebase", out var container) || args.Used.Comp.ShootCoordinates is not { } coords)
            return;

        foreach (var item in container.ContainedEntities)
        {
            if (!TryComp<GunComponent>(item, out var gun))
                continue;

            _gun.AttemptShoot(args.User, (item, gun), coords);
            return;
        }
    }

    private void OnDnaGunShot(Entity<UplinkDnaSyringeGunComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var fired in args.FiredProjectiles)
        {
            if (HasComp<DnaInjectorComponent>(fired))
                EnsureComp<UplinkDnaDartComponent>(fired).Shooter = Transform(ent).ParentUid;
        }
    }

    private void OnDnaDartHit(Entity<UplinkDnaDartComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!TryComp<DnaInjectorComponent>(ent, out var injector) || !HasComp<HumanoidProfileComponent>(args.Target))
            return;

        _dnaInjector.Inject((ent, injector), ent.Comp.Shooter ?? args.Target, args.Target);
        RemCompDeferred<UplinkDnaDartComponent>(ent);
    }

    #endregion

    #region Диод эмиттера

    /// <summary>emitter_disk/blast: эмиттер стреляет взрывными лазерами вдвое реже.</summary>
    private void OnEmitterDiskInteract(Entity<UplinkEmitterDiskComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<EmitterComponent>(target, out var emitter))
            return;

        args.Handled = true;
        emitter.BoltType = ent.Comp.Bolt;
        emitter.FireInterval *= ent.Comp.FireIntervalMultiplier;
        emitter.FireBurstDelayMin *= ent.Comp.FireIntervalMultiplier;
        emitter.FireBurstDelayMax *= ent.Comp.FireIntervalMultiplier;
        Dirty(target, emitter);
        _popup.PopupEntity(Loc.GetString("uplink-emitter-disk"), target, args.User);
        QueueDel(ent);
    }

    #endregion

    #region Организационная путаница

    /// <summary>bureaucratic_error: случайное число вакансий на все должности станции.</summary>
    private void OnBuyFinished(ref StoreBuyFinishedEvent ev)
    {
        if (ev.PurchasedItem.ID != BureaucraticErrorListing)
            return;

        foreach (var station in _station.GetStations())
        {
            foreach (var (job, _) in _stationJobs.GetJobs(station).ToList())
            {
                if (_proto.TryIndex(job, out var jobProto))
                    _stationJobs.TrySetJobSlot(station, jobProto, _random.Next(0, 4), true);
            }
        }
    }

    #endregion
}
