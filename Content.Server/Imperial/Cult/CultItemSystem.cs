using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>Предметы культа (cult_items.dm, cult_armor.dm).</summary>
public sealed partial class CultItemSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly CultRuneSystem _runes = default!;
    [Dependency] private readonly CultStructureSystem _structures = default!;
    [Dependency] private readonly ImperialCultRuleSystem _rule = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedEnsnareableSystem _ensnare = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergency = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private const int MaxShuttleCurses = 3;
    private static readonly EntProtoId BondAction = "ActionCultBloodBond";

    public override void Initialize()
    {
        base.Initialize();

        InitializeMirror();
        SubscribeLocalEvent<CultWeaponComponent, AttemptMeleeEvent>(OnWeaponAttempt);
        SubscribeLocalEvent<CultWeaponComponent, GotEquippedHandEvent>(OnWeaponPickup);
        SubscribeLocalEvent<CultistComponent, BeforeDamageChangedEvent>(OnCultistDamage);

        SubscribeLocalEvent<CultBolaComponent, ThrowDoHitEvent>(OnBolaHit);
        SubscribeLocalEvent<CultBolaComponent, GotEquippedHandEvent>(OnBolaPickup);

        SubscribeLocalEvent<CultWhetstoneComponent, AfterInteractEvent>(OnWhetstone);
        SubscribeLocalEvent<CultCurseOrbComponent, UseInHandEvent>(OnCurseOrb);
        SubscribeLocalEvent<CultVeilShifterComponent, UseInHandEvent>(OnShifter);
        SubscribeLocalEvent<CultVeilShifterComponent, ExaminedEvent>(OnShifterExamined);

        SubscribeLocalEvent<CultHalberdComponent, ThrowDoHitEvent>(OnHalberdHit);
        SubscribeLocalEvent<CultHalberdComponent, ComponentShutdown>(OnHalberdShutdown);
        SubscribeLocalEvent<CultistComponent, CultBloodBondActionEvent>(OnBloodBond);
        SubscribeLocalEvent<CultistComponent, CultRecallBloodSpearActionEvent>(OnLegacyRecall);

        SubscribeLocalEvent<CultBloodBoltComponent, Robust.Shared.Physics.Events.PreventCollideEvent>(OnBoltCollide);

        SubscribeLocalEvent<CultBloodBeamComponent, AfterInteractEvent>(OnBeamInteract);
        SubscribeLocalEvent<CultistComponent, CultBloodBeamDoAfterEvent>(OnBeamDoAfter);

        SubscribeLocalEvent<CultMirrorShieldComponent, ThrowDoHitEvent>(OnMirrorThrowHit);

        SubscribeLocalEvent<CultBlindfoldComponent, GotEquippedEvent>(OnBlindfoldEquipped);
        SubscribeLocalEvent<CultBlindfoldComponent, GotUnequippedEvent>(OnBlindfoldUnequipped);
        SubscribeLocalEvent<CultHardenedArmorComponent, GotEquippedEvent>(OnArmorEquipped);
        SubscribeLocalEvent<CultHardenedArmorComponent, GotUnequippedEvent>(OnArmorUnequipped);
    }

    #region Оружие

    /// <summary>cultblade/attack(): не-культиста отбрасывает и ранит.</summary>
    private void OnWeaponAttempt(Entity<CultWeaponComponent> ent, ref AttemptMeleeEvent args)
    {
        if (ent.Comp.FreeUse || !ent.Comp.PunishAttack || _cult.IsCultist(args.User))
            return;
        args.Cancelled = true;
        var user = args.User;
        _cult.Paralyze(user, 10);
        _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
        _popup.PopupEntity(Loc.GetString("cult-blade-shove-others", ("user", user)), user, Filter.PvsExcept(user), true, PopupType.MediumCaution);
        _cult.Message(user, Loc.GetString("cult-blade-shove-self"));
        _cult.Damage(user, "Slash", _random.NextFloat(ent.Comp.Force / 2f, ent.Comp.Force));
    }

    private void OnWeaponPickup(Entity<CultWeaponComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.WarnPickup && !ent.Comp.FreeUse && !_cult.IsCultist(args.User))
            _cult.Message(args.User, Loc.GetString("cult-blade-pickup-warning"));
    }

    /// <summary>hit_reaction(): парирование ближних атак культистом.</summary>
    private void OnCultistDamage(Entity<CultistComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || args.Origin is not { } origin || origin == ent.Owner || !args.Damage.AnyPositive())
            return;
        // Снаряд: источник урона (стрелок) дальше 2 тайлов.
        var a = _transform.GetMapCoordinates(ent);
        var b = _transform.GetMapCoordinates(origin);
        var ranged = HasComp<ProjectileComponent>(origin) || a.MapId != b.MapId || (a.Position - b.Position).Length() > 2f;

        foreach (var held in _hands.EnumerateHeld(ent.Owner).ToList())
        {
            if (TryComp<CultMirrorShieldComponent>(held, out var mirror) && TryMirrorBlock(ent, held, mirror, ranged, ref args))
                return;
        }

        // Парирование — только ближний бой.
        if (ranged)
            return;

        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!TryComp<CultParryComponent>(held, out var parry))
                continue;
            var chance = parry.Chance;
            if (TryComp<WieldableComponent>(held, out var wield) && wield.Wielded)
                chance *= parry.WieldedMultiplier;
            if (!_random.Prob(chance))
                continue;

            args.Cancelled = true;
            _cult.Effect("CultEffectSparks", Transform(ent).Coordinates, randomDir: true);
            _audio.PlayPvs(CultSounds.Parry, ent);
            _popup.PopupEntity(Loc.GetString("cult-parry", ("user", ent), ("item", held)), ent, PopupType.MediumCaution);
            return;
        }
    }

    #endregion

    #region Бола

    private void OnBolaHit(Entity<CultBolaComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!_cult.IsCultist(args.Target) || !TryComp<EnsnaringComponent>(ent, out var ensnaring))
            return;
        // Проходит сквозь культистов.
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMilliseconds(1), () =>
        {
            if (!TerminatingOrDeleted(ent) && ensnaring.Ensnared != null)
                _ensnare.ForceFree(ent, ensnaring);
        });
    }

    /// <summary>bola/cult/attack_hand(): не-культиста опутывает.</summary>
    private void OnBolaPickup(Entity<CultBolaComponent> ent, ref GotEquippedHandEvent args)
    {
        var user = args.User;
        if (_cult.IsCultist(user) || !HasComp<HumanoidProfileComponent>(user) || !TryComp<EnsnaringComponent>(ent, out var ensnaring))
            return;

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromMilliseconds(1), () =>
        {
            if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(user))
                return;
            _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
            if (_ensnare.TryEnsnare(user, ent, ensnaring))
            {
                _cult.Message(user, Loc.GetString("cult-bola-life"));
            }
            else
            {
                _cult.Message(user, Loc.GetString("cult-blade-pickup-warning"));
                _cult.Paralyze(user, 6);
            }
        });
    }

    #endregion

    #region Точило

    /// <summary>sharpener/cult: +5 урона до 40, одно применение, префикс «darkened».</summary>
    private void OnWhetstone(Entity<CultWhetstoneComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<MeleeWeaponComponent>(target, out var melee))
            return;
        args.Handled = true;
        var user = args.User;

        if (ent.Comp.Uses <= 0)
        {
            _cult.Message(user, Loc.GetString("cult-whetstone-used", ("stone", ent)));
            return;
        }
        if (!HasComp<Content.Shared.Kitchen.Components.SharpComponent>(target) && !HasComp<CultWeaponComponent>(target))
        {
            _cult.Message(user, Loc.GetString("cult-whetstone-not-sharp", ("target", target)));
            return;
        }
        if (HasComp<CultSharpenedComponent>(target))
        {
            _cult.Message(user, Loc.GetString("cult-whetstone-already", ("target", target)));
            return;
        }

        var total = melee.Damage.GetTotal().Float();
        if (total >= ent.Comp.Max)
        {
            _cult.Message(user, Loc.GetString("cult-whetstone-too-sharp", ("target", target)));
            return;
        }

        var add = MathF.Min(ent.Comp.Increment, ent.Comp.Max - total);
        Robust.Shared.Prototypes.ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> type = melee.Damage.DamageDict.Count > 0 ? melee.Damage.DamageDict.MaxBy(p => p.Value).Key : "Slash";
        melee.Damage.DamageDict[type] += FixedPoint2.New(add);
        Dirty(target, melee);
        EnsureComp<CultSharpenedComponent>(target);
        _meta.SetEntityName(target, Loc.GetString("cult-whetstone-prefix", ("prefix", ent.Comp.Prefix), ("name", Name(target))));
        _cult.Message(user, Loc.GetString("cult-whetstone-sharpen", ("target", target), ("stone", ent)));
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/BloodCult/unsheath.ogg"), ent);
        ent.Comp.Uses--;
        _appearance().SetData(ent, CultVisuals.State, ent.Comp.Uses == 0 ? "cult_sharpener_used" : "cult_sharpener");
    }

    private SharedAppearanceSystem _appearance() => EntityManager.System<SharedAppearanceSystem>();

    #endregion

    #region Сфера проклятия

    /// <summary>shuttle_curse/attack_self(): +3 минуты к прибытию шаттла.</summary>
    private void OnCurseOrb(Entity<CultCurseOrbComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.User;

        if (!_cult.IsCultist(user))
        {
            _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
            _cult.Paralyze(user, 10);
            _cult.Message(user, Loc.GetString("cult-curse-shoved", ("orb", ent)));
            return;
        }

        var rule = _rule.EnsureRule();
        if (rule.Comp.ShuttleCurses >= MaxShuttleCurses)
        {
            _cult.Message(user, Loc.GetString("cult-curse-solid"));
            _cult.Message(user, Loc.GetString("cult-curse-exhausted"));
            return;
        }
        if (EntityQuery<CultNarSieComponent>().Any())
        {
            _cult.Message(user, Loc.GetString("cult-curse-narsie"));
            return;
        }
        if (_roundEnd.ExpectedCountdownEnd == null || _emergency.EmergencyShuttleArrived)
            return;

        if (!DelayShuttle(TimeSpan.FromMinutes(3)))
            return;

        if (rule.Comp.ShuttleCurses == 0)
            rule.Comp.FirstCurseTime = _timing.CurTime;
        rule.Comp.ShuttleCurses++;
        _cult.Message(user, Loc.GetString("cult-curse-shatter"));
        _audio.PlayPvs(CultSounds.GlassBreak1, user, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));

        if (rule.Comp.RemainingCurses.Count == 0 && rule.Comp.ShuttleCurses == 1)
        {
            for (var i = 1; i <= 11; i++)
                rule.Comp.RemainingCurses.Add($"cult-curse-announce-{i}");
        }
        var message = rule.Comp.RemainingCurses.Count > 0
            ? Loc.GetString(_random.PickAndTake(rule.Comp.RemainingCurses))
            : Loc.GetString("cult-curse-announce-fallback");
        message += " " + Loc.GetString("cult-curse-announce-delay");
        _chat().DispatchGlobalAnnouncement(message, Loc.GetString("cult-curse-sender"), true, CultSounds.Notice1, Color.Gold);

        // Все проклятия за 10 секунд — Транспортный Департамент в ярости.
        if (rule.Comp.ShuttleCurses >= MaxShuttleCurses && _timing.CurTime < rule.Comp.FirstCurseTime + TimeSpan.FromSeconds(10))
        {
            var omfg = Loc.GetString($"cult-curse-omfg-{_random.Next(1, 8)}");
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(_random.NextFloat(2f, 6f)), () =>
                _chat().DispatchGlobalAnnouncement(omfg, Loc.GetString("cult-curse-omfg-sender"), true, CultSounds.AnnounceSyndi, Color.Gold));
        }

        var left = MaxShuttleCurses - rule.Comp.ShuttleCurses;
        _cult.Message(user, left switch
        {
            <= 0 => Loc.GetString("cult-curse-left-none"),
            1 => Loc.GetString("cult-curse-left-one"),
            _ => Loc.GetString("cult-curse-left-many", ("count", left)),
        });
        QueueDel(ent);
    }

    private Content.Server.Chat.Systems.ChatSystem _chat() => EntityManager.System<Content.Server.Chat.Systems.ChatSystem>();

    /// <summary>SSshuttle.emergency.setTimer(): сдвинуть таймер прибытия шаттла.</summary>
    private bool DelayShuttle(TimeSpan delay)
    {
        var field = typeof(RoundEndSystem).GetField("_countdownTokenSource", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(_roundEnd) is not CancellationTokenSource old || _roundEnd.ExpectedCountdownEnd is not { } end)
            return false;

        old.Cancel();
        var source = new CancellationTokenSource();
        field.SetValue(_roundEnd, source);

        var newEnd = end + delay;
        _roundEnd.ExpectedCountdownEnd = newEnd;
        _roundEnd.CantRecall = true;
        var remaining = newEnd - _timing.CurTime;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;
        Robust.Shared.Timing.Timer.Spawn(remaining, _emergency.DockEmergencyShuttle, source.Token);
        RaiseLocalEvent(RoundEndSystemChangedEvent.Default);
        return true;
    }

    #endregion

    #region Сдвигатель завесы

    private void OnShifterExamined(Entity<CultVeilShifterComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Uses > 0
            ? Loc.GetString("cult-shifter-uses", ("uses", ent.Comp.Uses))
            : Loc.GetString("cult-shifter-drained"));
    }

    /// <summary>cult_shift/attack_self(): телепорт на 9 тайлов вперёд вместе с тем, кого тащишь.</summary>
    private void OnShifter(Entity<CultVeilShifterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.User;

        if (ent.Comp.Uses <= 0 || !HasComp<HumanoidProfileComponent>(user))
        {
            _cult.Message(user, Loc.GetString("cult-shifter-dull", ("item", ent)));
            return;
        }
        if (!_cult.IsCultist(user))
        {
            _hands.TryDrop(user, ent.Owner, checkActionBlocker: false);
            _transform.SetCoordinates(ent, Transform(ent).Coordinates.Offset(_random.NextAngle().ToVec()));
            _cult.Message(user, Loc.GetString("cult-shifter-flicker", ("item", ent)));
            return;
        }

        var xform = Transform(user);
        var origin = xform.Coordinates;
        var forward = _transform.GetWorldRotation(xform).GetCardinalDir().ToVec();
        var side = new Vector2(-forward.Y, forward.X);

        EntityCoordinates? dest = null;
        for (var attempt = 0; attempt < 10 && dest == null; attempt++)
        {
            var errX = _random.Next(-3, 4);
            var errY = _random.Next(-1, 2) + 1;
            var offset = forward * (9 + errY - 1) + side * errX;
            var candidate = origin.Offset(offset);
            if (!_structures.IsBlocked(candidate))
                dest = candidate;
        }

        if (dest == null || _cult.IsBlessed(dest.Value))
        {
            _audio.PlayPvs(CultSounds.GhostItemAttack, ent);
            _popup.PopupEntity(Loc.GetString("cult-shifter-failed"), user, user);
            return;
        }

        EntityUid? pulled = TryComp<PullerComponent>(user, out var puller) ? puller.Pulling : null;
        if (pulled != null)
            _runes.Teleport(pulled.Value, dest.Value);
        _runes.Teleport(user, dest.Value);
        if (pulled != null && TryComp<PullableComponent>(pulled, out var pullable))
            _pulling.TryStartPull(user, pulled.Value, pullableComp: pullable);

        ent.Comp.Uses--;
        if (ent.Comp.Uses <= 0)
            _appearance().SetData(ent, CultVisuals.State, "shifter_drained");

        _cult.Effect("CultEffectPhaseOut", origin, xform.LocalRotation);
        _cult.Effect("CultEffectPhaseIn", dest.Value, xform.LocalRotation);
        _audio.PlayPvs(CultSounds.PortalEnter, origin, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
        _audio.PlayPvs(CultSounds.PhaseIn, dest.Value, AudioParams.Default.WithVolume(CultSystem.Db(25)).WithVariation(0.05f));
        _audio.PlayPvs(CultSounds.PortalEnter, dest.Value, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
    }

    #endregion

    #region Кровавая алебарда

    public void BindHalberd(EntityUid halberd, EntityUid owner)
    {
        if (!TryComp<CultHalberdComponent>(halberd, out var comp))
            return;
        comp.Owner = owner;
        EntityUid? action = null;
        if (_actions.AddAction(owner, ref action, BondAction))
            comp.BondAction = action;
    }

    private void OnHalberdShutdown(Entity<CultHalberdComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Owner is { } owner && ent.Comp.BondAction is { } action)
            _actions.RemoveAction(owner, action);
    }

    private void OnLegacyRecall(Entity<CultistComponent> ent, ref CultRecallBloodSpearActionEvent args)
    {
        var bond = new CultBloodBondActionEvent();
        OnBloodBond(ent, ref bond);
        args.Handled = bond.Handled;
    }

    /// <summary>halberd/Activate(): вернуть алебарду в руку.</summary>
    private void OnBloodBond(Entity<CultistComponent> ent, ref CultBloodBondActionEvent args)
    {
        EntityUid? halberd = null;
        var query = EntityQueryEnumerator<CultHalberdComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Owner == ent.Owner)
                halberd = uid;
        }
        if (halberd == null || _hands.IsHolding(ent.Owner, halberd.Value))
            return;

        var a = _transform.GetMapCoordinates(halberd.Value);
        var b = _transform.GetMapCoordinates(ent);
        if (a.MapId != b.MapId || (a.Position - b.Position).Length() > 10f)
        {
            _cult.Message(ent, Loc.GetString("cult-halberd-far"));
            return;
        }

        args.Handled = true;
        var container = Transform(halberd.Value).ParentUid;
        if (HasComp<MobStateComponent>(container) && _hands.IsHolding(container, halberd.Value))
        {
            _hands.TryDrop(container, halberd.Value, checkActionBlocker: false);
            _popup.PopupEntity(Loc.GetString("cult-halberd-pulled", ("target", container)), container, PopupType.MediumCaution);
        }
        else if (!Transform(halberd.Value).ParentUid.IsValid() || Transform(halberd.Value).GridUid != Transform(halberd.Value).ParentUid
                 && Transform(halberd.Value).MapUid != Transform(halberd.Value).ParentUid)
        {
            _transform.AttachToGridOrMap(halberd.Value);
        }
        _throwing.TryThrow(halberd.Value, Transform(ent).Coordinates, 10f, ent);
    }

    /// <summary>halberd/throw_impact(): ловит культист, не-культист падает, алебарда ломается.</summary>
    private void OnHalberdHit(Entity<CultHalberdComponent> ent, ref ThrowDoHitEvent args)
    {
        var target = args.Target;
        if (!HasComp<MobStateComponent>(target))
            return;

        if (_cult.IsCultist(target) && _hands.TryPickupAnyHand(target, ent))
        {
            _audio.PlayPvs(CultSounds.ThrowTap, target, AudioParams.Default.WithVolume(CultSystem.Db(50)));
            _popup.PopupEntity(Loc.GetString("cult-halberd-catch", ("target", target), ("item", ent)), target, PopupType.Medium);
            return;
        }
        if (_cult.BlocksMagic(target) || _cult.IsCultist(target))
        {
            _popup.PopupEntity(Loc.GetString("cult-halberd-bounce", ("target", target), ("item", ent)), target, PopupType.Medium);
            return;
        }

        _cult.Paralyze(target, 5);
        var coords = Transform(target).Coordinates;
        _popup.PopupCoordinates(Loc.GetString("cult-halberd-shatter", ("item", ent)), coords, PopupType.MediumCaution);
        _cult.Effect("CultEffectSparks", coords, randomDir: true);
        Spawn("PuddleBlood", coords);
        _audio.PlayPvs(CultSounds.GlassBreak3, coords);
        QueueDel(ent);
    }

    #endregion

    #region Кровавый залп

    /// <summary>arcane_barrage/blood/prehit_pierce(): культистов лечит и пролетает насквозь.</summary>
    private void OnBoltCollide(Entity<CultBloodBoltComponent> ent, ref Robust.Shared.Physics.Events.PreventCollideEvent args)
    {
        var other = args.OtherEntity;
        if (!_cult.IsCultAligned(other))
            return;
        args.Cancelled = true;
        if (ent.Comp.Healed.Add(other))
            HealByBlood(other, 4, 5);
    }

    /// <summary>Лечение культиста от кровавого залпа/луча.</summary>
    public void HealByBlood(EntityUid target, float unholy, float construct)
    {
        if (HasComp<CultConstructComponent>(target) || HasComp<CultShadeComponent>(target))
        {
            _cult.HealAll(target, construct);
            return;
        }
        if (_mobState.IsDead(target))
            return;
        if (_solutions.TryGetSolution(target, "chemicals", out var sol, out _))
            _solutions.TryAddReagent(sol.Value, "UnholyWater", FixedPoint2.New(unholy), out _);
    }

    #endregion

    #region Кровавый луч

    private void OnBeamInteract(Entity<CultBloodBeamComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        StartBeam(ent, args.User, args.ClickLocation);
    }

    public void StartBeam(Entity<CultBloodBeamComponent> ent, EntityUid user, EntityCoordinates target)
    {
        if (ent.Comp.Charging || ent.Comp.Firing || !HasComp<HumanoidProfileComponent>(user))
            return;

        var from = _transform.GetWorldPosition(user);
        var to = _transform.ToMapCoordinates(target).Position;
        ent.Comp.Angle = (to - from).ToWorldAngle();
        ent.Comp.Charging = true;
        ent.Comp.User = user;
        ent.Comp.Pulses = 0;
        ent.Comp.NextPulse = _timing.CurTime;
        _audio.PlayPvs(CultSounds.LightningChargeup, user);

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(9), new CultBloodBeamDoAfterEvent { Firing = false }, user, used: ent)
        {
            BreakOnMove = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            ent.Comp.Charging = false;
    }

    private void OnBeamDoAfter(Entity<CultistComponent> ent, ref CultBloodBeamDoAfterEvent args)
    {
        if (args.Handled || args.Used is not { } used || !TryComp<CultBloodBeamComponent>(used, out var beam))
            return;
        args.Handled = true;

        foreach (var effect in beam.ChargeEffects)
        {
            if (!TerminatingOrDeleted(effect))
                QueueDel(effect);
        }
        beam.ChargeEffects.Clear();

        if (!args.Firing)
        {
            beam.Charging = false;
            if (args.Cancelled)
                return;

            beam.Firing = true;
            beam.Pulses = 0;
            beam.Spread = 40f;
            beam.Second = false;
            beam.NextPulse = _timing.CurTime;
            beam.Shield = Spawn("CultShieldWeak", Transform(ent).Coordinates);
            _cult.Immobilize(ent, true);

            var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(9), new CultBloodBeamDoAfterEvent { Firing = true }, ent, used: used)
            {
                BreakOnMove = false,
                BreakOnDamage = false,
            };
            _doAfter.TryStartDoAfter(doAfter);
            return;
        }

        if (!args.Cancelled)
        {
            _cult.Paralyze(ent, 4);
            _cult.Message(ent, Loc.GetString("cult-beam-exhausted"));
        }
        EndBeam((used, beam));
    }

    private void EndBeam(Entity<CultBloodBeamComponent> beam)
    {
        beam.Comp.Firing = false;
        if (beam.Comp.User is { } user)
            _cult.Immobilize(user, false);
        if (beam.Comp.Shield is { } shield && !TerminatingOrDeleted(shield))
            QueueDel(shield);
        QueueDel(beam);
    }

    private void UpdateBeams(TimeSpan now)
    {
        var query = EntityQueryEnumerator<CultBloodBeamComponent>();
        while (query.MoveNext(out var uid, out var beam))
        {
            if (beam.User is not { } user || now < beam.NextPulse)
                continue;

            // charge(): 12 вспышек по 1.5 с.
            if (beam.Charging)
            {
                beam.Pulses++;
                beam.NextPulse = now + TimeSpan.FromSeconds(1.5);
                if (beam.Pulses > 12)
                    continue;
                var coords = Transform(user).Coordinates;
                foreach (var e in beam.ChargeEffects)
                {
                    if (!TerminatingOrDeleted(e))
                        QueueDel(e);
                }
                beam.ChargeEffects.Clear();
                if (beam.Pulses < 4)
                {
                    beam.ChargeEffects.Add(_runes.SpawnRuneSpawn(coords, "rune1inner", 179, 3f, Color.Red));
                }
                else
                {
                    beam.ChargeEffects.Add(_runes.SpawnRuneSpawn(coords, "rune5words", 181, 3f, Color.Red));
                    _cult.Effect("CultEffectPhaseOut", coords, Transform(user).LocalRotation);
                }
                continue;
            }

            if (!beam.Firing)
                continue;

            // pewpew(): 12 лучей парами.
            if (beam.Pulses >= 12)
                continue;
            beam.Pulses++;
            float angle;
            if (beam.Second)
            {
                angle = (float) beam.Angle.Degrees - beam.Spread;
                beam.Spread -= 8f;
                beam.NextPulse = now;
            }
            else
            {
                angle = (float) beam.Angle.Degrees + beam.Spread;
                beam.NextPulse = now + TimeSpan.FromSeconds(1.5);
            }
            beam.Second = !beam.Second;
            FireBeam(user, Angle.FromDegrees(angle));
        }
    }

    private void FireBeam(EntityUid user, Angle angle)
    {
        var xform = Transform(user);
        _audio.PlayPvs(CultSounds.ExitBlood, user, AudioParams.Default.WithVolume(CultSystem.Db(75)).WithVariation(0.05f));
        _cult.Effect("CultEffectPhaseIn", xform.Coordinates, xform.LocalRotation);

        var origin = _transform.GetWorldPosition(xform);
        var dir = angle.ToWorldVec();
        var mapId = xform.MapID;
        var end = origin + dir * 40f;
        var hitMobs = new HashSet<EntityUid>();

        for (var i = 1; i <= 40; i++)
        {
            var point = origin + dir * i;
            var coords = new MapCoordinates(point, mapId);
            var entCoords = _transform.ToCoordinates(coords);
            if (_cult.IsBlessed(entCoords))
            {
                end = point;
                _audio.PlayPvs(CultSounds.Parry, entCoords, AudioParams.Default.WithVolume(CultSystem.Db(50)));
                break;
            }
            _structures.NarsieActTurf(entCoords);

            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.5f))
            {
                if (mob.Owner == user || !hitMobs.Add(mob.Owner))
                    continue;
                if (_cult.IsCultAligned(mob))
                {
                    _cult.Effect("CultEffectSparks", Transform(mob).Coordinates, randomDir: true);
                    HealByBlood(mob, 7, 15);
                    continue;
                }
                _cult.Paralyze(mob, 2);
                _cult.Damage(mob, "Blunt", 45, origin: user);
                _audio.PlayPvs(CultSounds.Wail, mob, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
                _cult.Scream(mob);
            }
        }

        _cult.Beam(user, _transform.ToCoordinates(new MapCoordinates(end, mapId)), "CultBeamBlood", 0.7f);
    }

    #endregion

    #region Зеркальный щит

    private void OnMirrorThrowHit(Entity<CultMirrorShieldComponent> ent, ref ThrowDoHitEvent args)
    {
        var target = args.Target;
        if (!HasComp<MobStateComponent>(target))
            return;
        if (_cult.BlocksMagic(target) || _cult.IsCultist(target))
        {
            _popup.PopupEntity(Loc.GetString("cult-halberd-bounce", ("target", target), ("item", ent)), target, PopupType.Medium);
            return;
        }
        _cult.Paralyze(target, 3);
        _cult.Effect("CultEffectSparks", Transform(target).Coordinates, randomDir: true);
        _audio.PlayPvs(CultSounds.GlassBreak3, target);
        QueueDel(ent);
    }

    #endregion

    #region Повязка и броня

    private void OnBlindfoldEquipped(Entity<CultBlindfoldComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.EYES) == 0)
            return;
        ent.Comp.Wearer = args.EquipTarget;
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(2);
    }

    private void OnBlindfoldUnequipped(Entity<CultBlindfoldComponent> ent, ref GotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
    }

    private void OnArmorEquipped(Entity<CultHardenedArmorComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.OUTERCLOTHING) == 0)
            return;
        ent.Comp.Wearer = args.EquipTarget;
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
        if (!_cult.IsCultist(args.EquipTarget))
            _cult.Message(args.EquipTarget, Loc.GetString("cult-armor-burn"));
    }

    private void OnArmorUnequipped(Entity<CultHardenedArmorComponent> ent, ref GotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
    }

    private void UpdateWorn(TimeSpan now)
    {
        var blindfolds = EntityQueryEnumerator<CultBlindfoldComponent>();
        while (blindfolds.MoveNext(out _, out var blindfold))
        {
            if (blindfold.Wearer is not { } wearer || now < blindfold.NextTick)
                continue;
            blindfold.NextTick = now + TimeSpan.FromSeconds(2);
            if (!_cult.IsCultist(wearer))
                _blindable.AdjustEyeDamage(wearer, 1);
        }

        var armors = EntityQueryEnumerator<CultHardenedArmorComponent>();
        while (armors.MoveNext(out _, out var armor))
        {
            if (armor.Wearer is not { } wearer || now < armor.NextTick)
                continue;
            armor.NextTick = now + TimeSpan.FromSeconds(1);
            if (_cult.IsCultist(wearer) || _mobState.IsDead(wearer))
                continue;
            if (_random.Prob(0.15f))
            {
                _cult.Damage(wearer, "Slash", 5);
                _cult.Message(wearer, Loc.GetString("cult-armor-wound"));
            }
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        UpdateBeams(now);
        UpdateWorn(now);
        UpdateMirror(now);
    }
}
