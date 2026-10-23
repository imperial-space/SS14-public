using System.Linq;
using System.Numerics;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard;

/// <summary>/datum/component/wormborn: после смерти — перерождение огромным червём.</summary>
[RegisterComponent]
public sealed partial class ImperialWormbornComponent : Component;

/// <summary>/datum/component/dejavu/wizard: раз в 60 секунд — назад в прошлое место и состояние.</summary>
[RegisterComponent]
public sealed partial class ImperialDejavuComponent : Component
{
    [ViewVariables]
    public TimeSpan NextRewind;

    [ViewVariables]
    public EntityCoordinates? Position;

    [ViewVariables]
    public DamageSpecifier? Damage;
}

/// <summary>/datum/component/heart_eater: вырывать сердца у беззащитных и есть их ради силы.</summary>
[RegisterComponent]
public sealed partial class ImperialHeartEaterComponent : Component
{
    /// <summary>Накопленное снижение урона (damage_modifier).</summary>
    [ViewVariables]
    public float Resistance;
}

/// <summary>Вырванное сердце: съесть — исцеление и сила.</summary>
[RegisterComponent]
public sealed partial class ImperialRippedHeartComponent : Component;

/// <summary>/datum/component/slime_friends: раз в 20 секунд — злой взрослый слайм-союзник.</summary>
[RegisterComponent]
public sealed partial class ImperialSlimeFriendsComponent : Component
{
    [ViewVariables]
    public TimeSpan NextSlime;
}

/// <summary>transparence: снаряды пролетают насквозь, здоровье ×0,75, проклятие-преследователь.</summary>
[RegisterComponent]
public sealed partial class ImperialTransparentComponent : Component
{
    [ViewVariables]
    public bool StalkerSent;
}

/// <summary>/datum/brain_trauma/magic/stalker: призрак медленно ползёт к жертве; рядом — рвёт когтями.</summary>
[RegisterComponent]
public sealed partial class ImperialStalkerComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    /// <summary>on_life(): тик раз в 2 секунды.</summary>
    [ViewVariables]
    public TimeSpan NextTick;
}

/// <summary>Сегмент хвоста магического червя (/datum/component/mob_chain).</summary>
[RegisterComponent]
public sealed partial class ImperialWormSegmentComponent : Component
{
    [ViewVariables]
    public EntityUid Front;

    [ViewVariables]
    public EntityUid Head;
}

/// <summary>magnetism: всё в радиусе 5 притягивается к магу.</summary>
[RegisterComponent]
public sealed partial class ImperialMagnetismComponent : Component
{
    [ViewVariables]
    public TimeSpan NextPull;
}

/// <summary>Перки мага: работают вне логова.</summary>
public sealed class ImperialWizardPerksSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ImperialWizardRuleSystem _wizard = default!;
    [Dependency] private readonly Content.Shared.Hands.EntitySystems.SharedHandsSystem _hands = default!;

    private static readonly TimeSpan DejavuInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SlimeInterval = TimeSpan.FromSeconds(20);
    private static readonly EntProtoId[] Slimes = { "MobAdultSlimesBlueAngry", "MobAdultSlimesGreenAngry", "MobAdultSlimesYellowAngry" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialWormbornComponent, MobStateChangedEvent>(OnWormbornDeath);
        SubscribeLocalEvent<ImperialHeartEaterComponent, MeleeHitEvent>(OnHeartRip);
        SubscribeLocalEvent<ImperialRippedHeartComponent, UseInHandEvent>(OnHeartEat);
        SubscribeLocalEvent<ImperialHeartEaterComponent, DamageModifyEvent>(OnHeartEaterDamage);
        SubscribeLocalEvent<ImperialTransparentComponent, PreventCollideEvent>(OnTransparentCollide);
    }

    private bool Away(EntityUid uid)
    {
        return !_wizard.InDen(uid);
    }

    #region Червь

    /// <summary>second_breath(): тело оказывается внутри червя, разум — в черве.</summary>
    private void OnWormbornDeath(Entity<ImperialWormbornComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !Away(ent))
            return;

        RemComp<ImperialWormbornComponent>(ent);
        var worm = Spawn("ImperialWizardWorm", _transform.GetMapCoordinates(ent));
        if (_mind.TryGetMind(ent, out var mindId, out var mind))
            _mind.TransferTo(mindId, worm, mind: mind);

        var container = _containers.EnsureContainer<ContainerSlot>(worm, "imperial_worm_corpse");
        _containers.Insert(ent.Owner, container, force: true);
        _popup.PopupEntity(Loc.GetString("imperial-perk-wormborn"), worm, worm, PopupType.Large);
        BuildTail(worm);
    }

    /// <summary>build_tail(): пять сегментов цепочкой за головой, последний — хвост.</summary>
    private void BuildTail(EntityUid head)
    {
        var prev = head;
        for (var i = 0; i < 5; i++)
        {
            var segment = Spawn(i == 4 ? "ImperialWizardWormTail" : "ImperialWizardWormSegment", _transform.GetMapCoordinates(head));
            var comp = EnsureComp<ImperialWormSegmentComponent>(segment);
            comp.Front = prev;
            comp.Head = head;
            prev = segment;
        }
    }

    /// <summary>mob_chain: сегмент держится в шаге позади переднего, без головы исчезает.</summary>
    private void UpdateSegments()
    {
        var query = EntityQueryEnumerator<ImperialWormSegmentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var segment, out var xform))
        {
            if (TerminatingOrDeleted(segment.Head) || TerminatingOrDeleted(segment.Front) || _mobs.IsDead(segment.Head))
            {
                QueueDel(uid);
                continue;
            }

            var front = _transform.GetMapCoordinates(segment.Front);
            var mine = _transform.GetMapCoordinates(uid, xform);
            if (front.MapId != mine.MapId)
            {
                _transform.SetMapCoordinates(uid, front);
                continue;
            }

            var dir = mine.Position - front.Position;
            if (dir.Length() <= 0.9f)
                continue;

            _transform.SetWorldPosition(uid, front.Position + dir.Normalized() * 0.9f);
            _transform.SetWorldRotation(uid, (-dir).ToWorldAngle());
        }
    }

    #endregion

    #region Пожиратель сердец

    /// <summary>try_rip_heart(): удар голой рукой по беззащитному (крит или мёртв) вырывает сердце.</summary>
    private void OnHeartRip(Entity<ImperialHeartEaterComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != args.User)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(target) || _mobs.IsAlive(target) || HasComp<ImperialHeartRippedComponent>(target))
                continue;

            EnsureComp<ImperialHeartRippedComponent>(target);
            _damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Slash"] = 20 } }, true, origin: args.User);
            var heart = Spawn("ImperialRippedHeart", Transform(target).Coordinates);
            _meta.SetEntityName(heart, Loc.GetString("imperial-perk-heart-name", ("name", Name(target))));
            _hands.TryPickupAnyHand(args.User, heart);
            _popup.PopupEntity(Loc.GetString("imperial-perk-heart-ripped", ("user", args.User), ("target", target)), target, PopupType.LargeCaution);
            break;
        }
    }

    /// <summary>eat_eat_eat(): сильное исцеление; 50% — сопротивление урону, иначе мелкий дар.</summary>
    private void OnHeartEat(Entity<ImperialRippedHeartComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<ImperialHeartEaterComponent>(args.User, out var eater))
            return;

        args.Handled = true;
        var user = args.User;
        QueueDel(ent);
        if (TryComp<DamageableComponent>(user, out var damageable))
            _damage.SetAllDamage((user, damageable), 0);

        if (_random.Prob(0.5f) && eater.Resistance < 0.5f)
        {
            eater.Resistance += 0.1f;
            _popup.PopupEntity(Loc.GetString("imperial-perk-heart-resist"), user, user, PopupType.Large);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("imperial-perk-heart-gift"), user, user, PopupType.Large);
        }
    }

    private void OnHeartEaterDamage(Entity<ImperialHeartEaterComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Resistance > 0)
            args.Damage *= 1f - ent.Comp.Resistance;
    }

    #endregion

    #region Прозрачность

    /// <summary>maxHealth × 0,75 и полупрозрачность.</summary>
    public void MakeTransparent(EntityUid user)
    {
        EnsureComp<ImperialTransparentComponent>(user);
        EnsureComp<ImperialTransparentVisualsComponent>(user);
        foreach (var state in new[] { MobState.Critical, MobState.Dead })
        {
            if (_thresholds.TryGetThresholdForState(user, state, out var threshold))
                _thresholds.SetMobStateThreshold(user, threshold.Value * 0.75f, state);
        }
    }

    private void OnTransparentCollide(Entity<ImperialTransparentComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<ProjectileComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateSegments();
        var now = _timing.CurTime;

        var dejavu = EntityQueryEnumerator<ImperialDejavuComponent, TransformComponent>();
        while (dejavu.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!Away(uid))
            {
                comp.Position = null;
                continue;
            }

            if (comp.Position == null)
            {
                Snapshot(uid, comp);
                continue;
            }

            if (now < comp.NextRewind)
                continue;

            // rewind(): место и урон — как минуту назад.
            _popup.PopupEntity(Loc.GetString("imperial-perk-dejavu"), uid, uid);
            if (comp.Position is { } pos && !TerminatingOrDeleted(pos.EntityId))
            {
                _transform.SetCoordinates(uid, pos);
                _transform.AttachToGridOrMap(uid);
            }

            if (comp.Damage != null && TryComp<DamageableComponent>(uid, out var damageable) && !_mobs.IsDead(uid))
                _damage.SetDamage((uid, damageable), comp.Damage);

            Snapshot(uid, comp);
        }

        var slimes = EntityQueryEnumerator<ImperialSlimeFriendsComponent>();
        while (slimes.MoveNext(out var uid, out var comp))
        {
            if (!Away(uid) || _mobs.IsDead(uid))
                continue;

            if (comp.NextSlime == TimeSpan.Zero)
            {
                comp.NextSlime = now + SlimeInterval;
                continue;
            }

            if (now < comp.NextSlime)
                continue;

            comp.NextSlime = now + SlimeInterval;
            var slime = Spawn(_random.Pick(Slimes), Transform(uid).Coordinates);
            _faction.ClearFactions(slime);
            if (TryComp<NpcFactionMemberComponent>(uid, out var factions))
            {
                foreach (var faction in factions.Factions)
                {
                    _faction.AddFaction(slime, faction);
                }
            }

            if (TryComp<HungerComponent>(uid, out var hunger))
                _hunger.ModifyHunger(uid, -50, hunger);
        }

        var transparent = EntityQueryEnumerator<ImperialTransparentComponent>();
        while (transparent.MoveNext(out var uid, out var comp))
        {
            if (comp.StalkerSent || !Away(uid))
                continue;

            // make_stalker(): проклятие появляется, как только маг покидает логово.
            comp.StalkerSent = true;
            var offset = _random.NextVector2(12f, 16f);
            var stalker = Spawn("ImperialWizardStalker", _transform.GetMapCoordinates(uid).Offset(offset));
            EnsureComp<ImperialStalkerComponent>(stalker).Target = uid;
        }

        var stalkers = EntityQueryEnumerator<ImperialStalkerComponent, TransformComponent>();
        while (stalkers.MoveNext(out var uid, out var stalker, out var xform))
        {
            if (TerminatingOrDeleted(stalker.Target) || _mobs.IsDead(stalker.Target))
            {
                QueueDel(uid);
                continue;
            }

            var targetPos = _transform.GetMapCoordinates(stalker.Target);
            var myPos = _transform.GetMapCoordinates(uid, xform);
            if (targetPos.MapId != myPos.MapId)
            {
                _transform.SetMapCoordinates(uid, targetPos.Offset(_random.NextVector2(12f, 16f)));
                continue;
            }

            // Без сознания жертва призраку неинтересна.
            if (now < stalker.NextTick || !_mobs.IsAlive(stalker.Target))
                continue;

            stalker.NextTick = now + TimeSpan.FromSeconds(2);
            var dir = targetPos.Position - myPos.Position;
            if (dir.Length() <= 1.5f)
            {
                // take_bodypart_damage(rand(20, 45))
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_attack1.ogg"), stalker.Target);
                _popup.PopupEntity(Loc.GetString("imperial-perk-stalker-got-you"), stalker.Target, stalker.Target, PopupType.LargeCaution);
                _damage.TryChangeDamage(stalker.Target, new DamageSpecifier { DamageDict = { ["Slash"] = _random.Next(20, 46) } }, true);
                continue;
            }

            // SPT_PROB(30, 2 с): шаг к жертве.
            if (_random.Prob(0.51f))
                _transform.SetWorldPosition(uid, myPos.Position + dir.Normalized() * Math.Min(1f, dir.Length()));
        }

        var magnets = EntityQueryEnumerator<ImperialMagnetismComponent, TransformComponent>();
        while (magnets.MoveNext(out var uid, out var magnet, out var xform))
        {
            if (now < magnet.NextPull || !Away(uid) || _mobs.IsDead(uid))
                continue;

            magnet.NextPull = now + TimeSpan.FromSeconds(0.5);
            var center = _transform.GetWorldPosition(xform);
            foreach (var thing in _lookup.GetEntitiesInRange(xform.Coordinates, 5f, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (thing == uid || Transform(thing).Anchored || _containers.IsEntityInContainer(thing))
                    continue;

                var pos = _transform.GetWorldPosition(thing);
                var toward = center - pos;
                if (toward.Length() < 1.5f)
                    continue;

                _transform.SetWorldPosition(thing, pos + toward.Normalized() * 0.5f);
            }
        }
    }

    private void Snapshot(EntityUid uid, ImperialDejavuComponent comp)
    {
        comp.Position = Transform(uid).Coordinates;
        comp.Damage = HasComp<DamageableComponent>(uid) ? new DamageSpecifier(_damage.GetAllDamage(uid)) : null;
        comp.NextRewind = _timing.CurTime + DejavuInterval;
    }
}

/// <summary>Сердце уже вырвано.</summary>
[RegisterComponent]
public sealed partial class ImperialHeartRippedComponent : Component;
