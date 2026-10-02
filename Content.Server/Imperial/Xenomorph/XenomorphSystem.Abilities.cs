using System.Linq;
using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

public sealed partial class XenomorphSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly EntProtoId NeurotoxinProjectile = "ImperialXenoNeurotoxin";
    private static readonly EntProtoId AcidOverlay = "ImperialXenoAcidOverlay";
    private static readonly SoundSpecifier TailSweepSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/tail_swing.ogg");
    private static readonly SoundSpecifier SpitSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/alien_spitacid.ogg");
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    /// <summary>Кислота обрабатывается раз в секунду (SSprocessing).</summary>
    private static readonly TimeSpan AcidTick = TimeSpan.FromSeconds(1);

    /// <summary>corrosion_acid_volume и MOVABLE_ACID_VOLUME_MAX.</summary>
    private const float CorrosionVolume = 1000;
    private const float MovableAcidVolumeMax = 300;

    /// <summary>Стены, которые в SS13 укреплённые (explosive_resistance ≥ 2) или неразрушимые.</summary>
    private static readonly string[] AcidProofWalls =
    {
        "Reinforced", "Plastitanium", "Indestructible", "Shuttle", "Vault", "Necropolis", "Force", "Invisible", "Debug",
    };

    /// <summary>repulse/xeno: max_throw, aoe_radius.</summary>
    private const int TailSweepMaxThrow = 5;
    private const float TailSweepRadius = 2;

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<XenomorphComponent, XenoNeurotoxinActionEvent>(OnNeurotoxin);
        SubscribeLocalEvent<XenomorphComponent, XenoAcidActionEvent>(OnAcid);
        SubscribeLocalEvent<XenomorphComponent, XenoLeapActionEvent>(OnLeap);
        SubscribeLocalEvent<XenoLeaperComponent, ThrowDoHitEvent>(OnLeapHit);
        SubscribeLocalEvent<XenoLeaperComponent, StopThrowEvent>(OnLeapStop);
        SubscribeLocalEvent<XenomorphComponent, XenoTailSweepActionEvent>(OnTailSweep);
        SubscribeLocalEvent<XenomorphComponent, XenoSneakActionEvent>(OnSneak);
        SubscribeLocalEvent<XenomorphComponent, XenoHideActionEvent>(OnHide);
        SubscribeLocalEvent<XenomorphComponent, XenoDevourActionEvent>(OnDevour);
        SubscribeLocalEvent<XenoStomachComponent, XenoDevourDoAfterEvent>(OnDevourDoAfter);
        SubscribeLocalEvent<XenomorphComponent, XenoRegurgitateActionEvent>(OnRegurgitate);
        SubscribeLocalEvent<XenoAcidComponent, ComponentShutdown>(OnAcidShutdown);
    }

    private void UpdateAbilities(TimeSpan now)
    {
        var acids = EntityQueryEnumerator<XenoAcidComponent>();
        while (acids.MoveNext(out var uid, out var acid))
        {
            if (now < acid.NextTick)
                continue;

            acid.NextTick = now + AcidTick;
            var seconds = (float) AcidTick.TotalSeconds;

            // process_turf: стена держится 30 секунд и рассыпается.
            if (acid.Wall)
            {
                acid.WallIntegrity -= seconds;
                if (acid.WallIntegrity <= 0)
                {
                    _popup.PopupEntity(Loc.GetString("xeno-acid-wall-collapse", ("target", uid)), uid, PopupType.MediumCaution);
                    QueueDel(uid);
                }
                else if (acid.WallIntegrity <= 4)
                    _popup.PopupEntity(Loc.GetString("xeno-acid-wall-crumble", ("target", uid)), uid, PopupType.SmallCaution);
                continue;
            }

            if (acid.Volume <= 0)
            {
                RemCompDeferred<XenoAcidComponent>(uid);
                continue;
            }

            // process_movable: min(1 + round(sqrt(power × volume) × 0.3), 300) урона в секунду.
            if (HasComp<DamageableComponent>(uid))
            {
                var damage = Math.Min(1 + MathF.Round(MathF.Sqrt(acid.Power * acid.Volume) * 0.3f), 300) * seconds;
                _damageable.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Heat"] = damage } }, ignoreResistances: true);
            }
            else
            {
                // Предметы без прочности просто растворяются.
                QueueDel(uid);
                continue;
            }

            // Распад кислоты: ACID_DECAY_BASE + ACID_DECAY_SCALING × round(sqrt(volume)).
            acid.Volume -= (1 + MathF.Round(MathF.Sqrt(acid.Volume))) * seconds;
        }
    }

    #region Нейротоксин

    /// <summary>acid/neurotoxin: плевок, наносящий 65 урона выносливости.</summary>
    private void OnNeurotoxin(Entity<XenomorphComponent> ent, ref XenoNeurotoxinActionEvent args)
    {
        if (args.Handled || !TrySpendPlasma(ent, args.PlasmaCost))
            return;

        args.Handled = true;
        var from = _xform.GetMapCoordinates(ent);
        var to = _xform.ToMapCoordinates(args.Target);
        var direction = to.Position - from.Position;
        if (direction == Vector2.Zero)
            return;

        var spit = Spawn(NeurotoxinProjectile, from);
        _gun.ShootProjectile(spit, direction, Vector2.Zero, null, ent, 20f);
        _audio.PlayPvs(SpitSound, ent);
    }

    #endregion

    #region Кислота

    /// <summary>acid/corrosion: облить предмет кислотой, разъедающей его со временем. Не действует на живых.</summary>
    private void OnAcid(Entity<XenomorphComponent> ent, ref XenoAcidActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        // «Doesn't work on creatures!»
        if (HasComp<MobStateComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("xeno-acid-creature"), ent, ent);
            return;
        }

        var wall = _tag.HasTag(target, WallTag) && !IsXenoStructure(target);
        if (wall && IsAcidProofWall(target)
            || !wall && !HasComp<DamageableComponent>(target) && !HasComp<ItemComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("xeno-acid-invalid"), ent, ent);
            return;
        }

        if (!TrySpendPlasma(ent, args.PlasmaCost))
            return;

        args.Handled = true;

        // Повторная кислота складывается (InheritComponent), объём ограничен MOVABLE_ACID_VOLUME_MAX.
        if (TryComp<XenoAcidComponent>(target, out var existing))
        {
            existing.Volume = Math.Min(existing.Volume + CorrosionVolume, MovableAcidVolumeMax);
        }
        else
        {
            var acid = AddComp<XenoAcidComponent>(target);
            acid.Wall = wall;
            acid.Volume = Math.Min(CorrosionVolume, MovableAcidVolumeMax);
            acid.NextTick = _timing.CurTime + AcidTick;
            acid.Overlay = SpawnAttachedTo(AcidOverlay, Transform(target).Coordinates);
        }

        _popup.PopupEntity(Loc.GetString("xeno-acid-applied", ("xeno", ent.Owner), ("target", target)), target, PopupType.MediumCaution);
    }

    /// <summary>Укреплённые стены (explosive_resistance ≥ 2) кислота не берёт.</summary>
    private bool IsAcidProofWall(EntityUid wall)
    {
        if (MetaData(wall).EntityPrototype is not { } proto)
            return true;

        foreach (var parent in _proto.EnumerateParents(proto, includeSelf: true))
        {
            foreach (var marker in AcidProofWalls)
            {
                if (parent.ID.Contains(marker, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private bool IsXenoStructure(EntityUid uid)
    {
        return MetaData(uid).EntityPrototype?.ID.StartsWith("ImperialXeno", StringComparison.Ordinal) == true;
    }

    private void OnAcidShutdown(Entity<XenoAcidComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Overlay is { } overlay)
            QueueDel(overlay);
    }

    #endregion

    #region Прыжок охотника

    private void OnLeap(Entity<XenomorphComponent> ent, ref XenoLeapActionEvent args)
    {
        if (args.Handled || !TryComp<XenoLeaperComponent>(ent, out var leaper) || leaper.Leaping)
            return;

        if (_gravity.IsWeightless(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("xeno-leap-no-gravity"), ent, ent);
            return;
        }

        args.Handled = true;
        var from = _xform.GetMapCoordinates(ent);
        var to = _xform.ToMapCoordinates(args.Target);
        var direction = to.Position - from.Position;
        if (direction.Length() > leaper.MaxDistance)
            direction = Vector2.Normalize(direction) * leaper.MaxDistance;

        leaper.Leaping = true;
        _throwing.TryThrow(ent, direction, leaper.Speed, ent, doSpin: false);
    }

    /// <summary>throw_impact: живая цель опрокидывается на 5 с, удар о стену оглушает самого охотника.</summary>
    private void OnLeapHit(Entity<XenoLeaperComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.Leaping)
            return;

        ent.Comp.Leaping = false;
        if (HasComp<MobStateComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("xeno-leap-pounce", ("xeno", ent.Owner), ("target", args.Target)), args.Target, PopupType.LargeCaution);
            _stun.TryUpdateParalyzeDuration(args.Target, ent.Comp.TargetParalyze);
            return;
        }

        if (TryComp<PhysicsComponent>(args.Target, out var physics) && physics.Hard && physics.BodyType == Robust.Shared.Physics.BodyType.Static)
        {
            _popup.PopupEntity(Loc.GetString("xeno-leap-smash", ("xeno", ent.Owner), ("target", args.Target)), ent, PopupType.MediumCaution);
            _stun.TryUpdateParalyzeDuration(ent, ent.Comp.SelfParalyze);
        }
    }

    private void OnLeapStop(Entity<XenoLeaperComponent> ent, ref StopThrowEvent args)
    {
        ent.Comp.Leaping = false;
    }

    #endregion

    #region Удар хвостом

    /// <summary>
    /// repulse/xeno: всё незакреплённое в радиусе 2 отлетает. На одной клетке с хвостом — паралич 10 с и 5 урона,
    /// остальные — паралич 4 с и полёт на clamp(5 − (dist − 2), 3, 5) клеток. Королевские особи сильны и не падают сами.
    /// </summary>
    private void OnTailSweep(Entity<XenomorphComponent> ent, ref XenoTailSweepActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(TailSweepSound, ent);
        _audio.PlayPvs(RoarSound, ent);

        var center = _xform.GetMapCoordinates(ent);
        foreach (var victim in _lookup.GetEntitiesInRange(center, TailSweepRadius, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (victim == ent.Owner || Transform(victim).Anchored || _container.IsEntityInContainer(victim)
                || !HasComp<PhysicsComponent>(victim) || HasComp<Content.Shared.Ghost.GhostComponent>(victim))
            {
                continue;
            }

            var offset = _xform.GetMapCoordinates(victim).Position - center.Position;
            var distance = (int) MathF.Round(Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)));
            if (distance == 0)
            {
                if (!HasComp<MobStateComponent>(victim))
                    continue;

                _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(10));
                _damageable.TryChangeDamage(victim, new DamageSpecifier { DamageDict = { ["Blunt"] = 5 } });
                _popup.PopupEntity(Loc.GetString("xeno-tail-sweep-slam", ("xeno", ent.Owner)), victim, victim, PopupType.LargeCaution);
                continue;
            }

            if (HasComp<MobStateComponent>(victim))
            {
                _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(4));
                _popup.PopupEntity(Loc.GetString("xeno-tail-sweep-thrown", ("xeno", ent.Owner)), victim, victim, PopupType.LargeCaution);
            }

            var range = Math.Clamp(TailSweepMaxThrow - Math.Clamp(distance - 2, 0, distance), 3, TailSweepMaxThrow);
            _throwing.TryThrow(victim, Vector2.Normalize(offset) * range, 10f, ent, pushbackRatio: 0, compensateFriction: true);
        }
    }

    #endregion

    #region Скрытность и «спрятаться»

    /// <summary>sneak/alien: сливаться с тенями (прозрачность 25/255).</summary>
    private void OnSneak(Entity<XenomorphComponent> ent, ref XenoSneakActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (RemComp<XenoSneakingComponent>(ent))
        {
            RemComp<StealthComponent>(ent);
            _actions.SetToggled(args.Action.Owner, false);
            _popup.PopupEntity(Loc.GetString("xeno-sneak-off"), ent, ent);
            return;
        }

        EnsureComp<XenoSneakingComponent>(ent);
        var stealth = EnsureComp<StealthComponent>(ent);
        _stealth.SetVisibility(ent, 0.1f, stealth);
        _actions.SetToggled(args.Action.Owner, true);
        _popup.PopupEntity(Loc.GetString("xeno-sneak-on"), ent, ent);
    }

    /// <summary>larva/hide: спрятаться под столами и предметами.</summary>
    private void OnHide(Entity<XenomorphComponent> ent, ref XenoHideActionEvent args)
    {
        if (args.Handled || !TryComp<XenoLarvaComponent>(ent, out var larva))
            return;

        args.Handled = true;
        larva.Hidden = !larva.Hidden;
        _appearance.SetData(ent, XenoVisuals.Hidden, larva.Hidden);
        _actions.SetToggled(args.Action.Owner, larva.Hidden);
        _popup.PopupEntity(Loc.GetString(larva.Hidden ? "xeno-hide-on" : "xeno-hide-off", ("xeno", ent.Owner)), ent, PopupType.Small);
    }

    #endregion

    #region Пожирание

    /// <summary>devour_lad: пожрать того, кого тащишь (13,5 с).</summary>
    private void OnDevour(Entity<XenomorphComponent> ent, ref XenoDevourActionEvent args)
    {
        if (args.Handled || !TryComp<XenoStomachComponent>(ent, out var stomach))
            return;

        var target = args.Target;
        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling != target
            || !HasComp<MobStateComponent>(target) || HasComp<XenomorphComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("xeno-devour-need-pull"), ent, ent);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("xeno-devour-start", ("xeno", ent.Owner), ("target", target)), ent, PopupType.LargeCaution);
        _audio.PlayPvs(stomach.DevourSound, target);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, stomach.DevourTime, new XenoDevourDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            NeedHand = false,
        });
    }

    private void OnDevourDoAfter(Entity<XenoStomachComponent> ent, ref XenoDevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || TerminatingOrDeleted(target))
            return;

        args.Handled = true;
        var container = _container.EnsureContainer<Container>(ent, XenoStomachComponent.ContainerId);
        if (_container.Insert(target, container))
            _popup.PopupEntity(Loc.GetString("xeno-devour-done", ("xeno", ent.Owner), ("target", target)), ent, PopupType.LargeCaution);
    }

    /// <summary>
    /// stomach/on_life: раз в три тика кислота желудка (сила 75) разъедает содержимое.
    /// stomach/alien/content_died: умерших переваривает полностью.
    /// </summary>
    private void DigestStomach(EntityUid uid, TimeSpan now)
    {
        if (!TryComp<XenoStomachComponent>(uid, out var stomach)
            || !_container.TryGetContainer(uid, XenoStomachComponent.ContainerId, out var container))
        {
            return;
        }

        foreach (var victim in container.ContainedEntities.ToArray())
        {
            if (_mobState.IsDead(victim))
                QueueDel(victim);
        }

        if (now < stomach.NextDigest)
            return;

        stomach.NextDigest = now + stomach.DigestInterval;
        foreach (var victim in container.ContainedEntities.ToArray())
        {
            if (HasComp<DamageableComponent>(victim))
                _damageable.TryChangeDamage(victim, stomach.Digestion, interruptsDoAfters: false);
            else
                QueueDel(victim);
        }
    }

    private void OnRegurgitate(Entity<XenomorphComponent> ent, ref XenoRegurgitateActionEvent args)
    {
        if (args.Handled || !TryComp<XenoStomachComponent>(ent, out var stomach))
            return;

        args.Handled = true;
        if (!_container.TryGetContainer(ent, XenoStomachComponent.ContainerId, out var container) || container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("xeno-regurgitate-empty"), ent, ent);
            return;
        }

        // eject_stomach: содержимое разлетается веером ±45° перед ксеноморфом на 4 клетки.
        var facing = _xform.GetWorldRotation(ent);
        foreach (var thing in _container.EmptyContainer(container, true, Transform(ent).Coordinates))
        {
            var angle = facing + Angle.FromDegrees(_random.NextFloat(-45, 45));
            _throwing.TryThrow(thing, angle.ToWorldVec() * 4, 15f, ent, pushbackRatio: 0);
        }

        _audio.PlayPvs(stomach.RegurgitateSound, ent);
        _popup.PopupEntity(Loc.GetString("xeno-regurgitate", ("xeno", ent.Owner)), ent, PopupType.MediumCaution);
    }

    #endregion
}
