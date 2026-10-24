using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Imperial.Uplink.Roles;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Uplink.Roles;

public sealed partial class UplinkRoleSystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;

    // HIS_GRACE_* из __DEFINES/combat.dm
    private const float GraceSatiated = 0;
    private const float GraceFamished = 100;
    private const float GraceStarving = 120;
    private const float GraceConsumeOwner = 140;
    private const float GraceFallAsleep = 160;
    private const float GraceForceBonus = 4;
    private const string GraceContainer = "his_grace_meals";

    private static readonly SoundSpecifier GraceAwakenSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/pope_entry.ogg");
    private static readonly SoundSpecifier GraceEatSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/eatfood.ogg");

    private void InitializeHisGrace()
    {
        SubscribeLocalEvent<UplinkHisGraceComponent, UseInHandEvent>(OnGraceUse);
        SubscribeLocalEvent<UplinkHisGraceComponent, MeleeHitEvent>(OnGraceHit);
        SubscribeLocalEvent<UplinkHisGraceComponent, ExaminedEvent>(OnGraceExamined);
    }

    private void OnGraceExamined(Entity<UplinkHisGraceComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Awakened)
        {
            args.PushMarkup(Loc.GetString("uplink-grace-latched"));
            return;
        }

        var key = ent.Comp.Bloodthirst switch
        {
            < 20 => "uplink-grace-satiated",
            < 60 => "uplink-grace-peckish",
            < 100 => "uplink-grace-hungry",
            < 120 => "uplink-grace-examine-famished",
            < 140 => "uplink-grace-examine-starving",
            _ => "uplink-grace-consume",
        };
        args.PushMarkup(Loc.GetString(key));
    }

    /// <summary>attack_self: открыть защёлку — Его Милость пробуждается.</summary>
    private void OnGraceUse(Entity<UplinkHisGraceComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Awakened)
            return;

        args.Handled = true;
        ent.Comp.Awakened = true;
        ent.Comp.Bloodthirst = 1;
        ent.Comp.ForceBonus = GraceForceBonus * GetMeals(ent).Count;
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
        _metaData.SetEntityName(ent, Loc.GetString("uplink-grace-name-awake"));
        _audio.PlayPvs(GraceAwakenSound, ent);
        _popup.PopupEntity(Loc.GetString("uplink-grace-awaken"), ent, PopupType.LargeCaution);
    }

    private List<EntityUid> GetMeals(EntityUid grace)
    {
        return _container.TryGetContainer(grace, GraceContainer, out var container)
            ? container.ContainedEntities.ToList()
            : new List<EntityUid>();
    }

    /// <summary>attack: упавшего Он пожирает, остальным — урон с бонусом за съеденных.</summary>
    private void OnGraceHit(Entity<UplinkHisGraceComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (ent.Comp.Awakened && ent.Comp.ForceBonus > 0)
            args.BonusDamage += new DamageSpecifier(args.BaseDamage) * (ent.Comp.ForceBonus / ent.Comp.BaseForce);

        if (!ent.Comp.Awakened)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!TryComp<MobStateComponent>(hit, out var state) || state.CurrentState == MobState.Alive)
                continue;

            Consume(ent, hit);
            break;
        }
    }

    /// <summary>consume: съесть жертву, подкрепиться, вырасти в силе.</summary>
    private void Consume(Entity<UplinkHisGraceComponent> ent, EntityUid meal)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = 200;
        _damageable.TryChangeDamage(meal, damage, true);
        _audio.PlayPvs(GraceEatSound, ent);
        _popup.PopupEntity(Loc.GetString("uplink-grace-devour", ("target", meal)), meal, PopupType.LargeCaution);

        var container = _container.EnsureContainer<Container>(ent, GraceContainer);
        _container.Insert(meal, container);
        ent.Comp.ForceBonus += GraceForceBonus;
        ent.Comp.PrevBloodthirst = ent.Comp.Bloodthirst;
        ent.Comp.Bloodthirst = ent.Comp.PrevBloodthirst < GraceConsumeOwner
            ? Math.Max(container.ContainedEntities.Count, 1)
            : GraceConsumeOwner;

        ent.Comp.Victims = container.ContainedEntities.Count(HasComp<MindContainerComponent>);
        if (ent.Comp.Victims >= 25)
        {
            ent.Comp.Ascended = true;
            ent.Comp.ForceBonus += 15;
            _popup.PopupEntity(Loc.GetString("uplink-grace-ascend"), ent, PopupType.LargeCaution);
        }

        UpdateGraceStats(ent);
    }

    private void UpdateHisGrace(TimeSpan now)
    {
        var query = EntityQueryEnumerator<UplinkHisGraceComponent>();
        while (query.MoveNext(out var uid, out var grace))
        {
            if (!grace.Awakened || now < grace.NextTick)
                continue;

            grace.NextTick = now + TimeSpan.FromSeconds(1);
            var ent = new Entity<UplinkHisGraceComponent>(uid, grace);
            if (grace.Bloodthirst <= GraceSatiated)
            {
                Drowse(ent);
                continue;
            }

            // process(): голод растёт тем быстрее, чем больше Он съел.
            var increment = grace.Bloodthirst < GraceConsumeOwner && !grace.Ascended
                ? 1 + MathF.Floor(GetMeals(uid).Count * 0.5f)
                : 1;
            AdjustBloodthirst(ent, increment);

            var holder = Transform(uid).ParentUid;
            if (_hands.IsHolding(holder, uid))
            {
                if (grace.Bloodthirst >= GraceConsumeOwner)
                {
                    // Он набрасывается на хозяина.
                    _popup.PopupEntity(Loc.GetString("uplink-grace-turns", ("target", holder)), holder, PopupType.LargeCaution);
                    RemComp<UnremoveableComponent>(uid);
                    _stun.TryAddParalyzeDuration(holder, TimeSpan.FromSeconds(6));
                    var damage = new DamageSpecifier();
                    damage.DamageDict["Blunt"] = 100;
                    _damageable.TryChangeDamage(holder, damage, true);
                    continue;
                }

                GraceBuff(holder);
                continue;
            }

            if (grace.Bloodthirst < GraceConsumeOwner)
                continue;

            if (grace.Bloodthirst >= GraceFallAsleep)
            {
                Drowse(ent);
                continue;
            }

            // Без хозяина в бешенстве Он сам охотится в радиусе двух клеток.
            var coords = _transform.GetMapCoordinates(uid);
            var targets = _lookup.GetEntitiesInRange<MobStateComponent>(coords, 2f).ToList();
            if (targets.Count == 0)
                continue;

            var target = _random.Pick(targets);
            _transform.SetMapCoordinates(uid, _transform.GetMapCoordinates(target));
            if (target.Comp.CurrentState == MobState.Alive)
            {
                var damage = new DamageSpecifier();
                damage.DamageDict["Blunt"] = grace.BaseForce + grace.ForceBonus;
                _damageable.TryChangeDamage(target.Owner, damage, true);
                _popup.PopupEntity(Loc.GetString("uplink-grace-lunge", ("target", target.Owner)), target, PopupType.MediumCaution);
                AdjustBloodthirst(ent, -5);
            }
            else
            {
                Consume(ent, target);
            }
        }
    }

    /// <summary>status_effect/his_grace: регенерация и иммунитет к оглушению у держащего.</summary>
    private void GraceBuff(EntityUid holder)
    {
        var heal = new DamageSpecifier();
        foreach (var type in new[] { "Blunt", "Slash", "Piercing", "Heat", "Poison", "Asphyxiation" })
        {
            heal.DamageDict[type] = -1;
        }

        _damageable.TryChangeDamage(holder, heal, true, false);
        _stun.TryUnstun(holder);
        _stun.SetKnockdownTime(holder, TimeSpan.Zero);
    }

    private void AdjustBloodthirst(Entity<UplinkHisGraceComponent> ent, float amount)
    {
        ent.Comp.PrevBloodthirst = ent.Comp.Bloodthirst;
        if (ent.Comp.Ascended)
            return;

        ent.Comp.Bloodthirst = ent.Comp.PrevBloodthirst < GraceConsumeOwner
            ? Math.Clamp(ent.Comp.Bloodthirst + amount, GraceSatiated, GraceConsumeOwner)
            : Math.Clamp(ent.Comp.Bloodthirst + amount, GraceConsumeOwner, GraceFallAsleep);

        UpdateGraceStats(ent);
    }

    /// <summary>update_stats: от «очень голоден» Его нельзя выпустить из рук; урон не ниже 10/15.</summary>
    private void UpdateGraceStats(Entity<UplinkHisGraceComponent> ent)
    {
        var bloodthirst = ent.Comp.Bloodthirst;
        var holder = Transform(ent).ParentUid;
        if (bloodthirst >= GraceFamished && bloodthirst < GraceConsumeOwner)
        {
            EnsureComp<UnremoveableComponent>(ent);
            if (bloodthirst >= GraceStarving && ent.Comp.PrevBloodthirst < GraceStarving)
            {
                ent.Comp.ForceBonus = Math.Max(ent.Comp.ForceBonus, 15);
                _popup.PopupEntity(Loc.GetString("uplink-grace-starving"), holder, holder, PopupType.LargeCaution);
            }
            else if (ent.Comp.PrevBloodthirst < GraceFamished)
            {
                ent.Comp.ForceBonus = Math.Max(ent.Comp.ForceBonus, 10);
                _popup.PopupEntity(Loc.GetString("uplink-grace-famished"), holder, holder, PopupType.LargeCaution);
            }
        }
        else
        {
            RemComp<UnremoveableComponent>(ent);
        }
    }

    /// <summary>drowse: засыпает и захлопывает защёлку.</summary>
    private void Drowse(Entity<UplinkHisGraceComponent> ent)
    {
        if (!ent.Comp.Awakened || ent.Comp.Ascended)
            return;

        _popup.PopupEntity(Loc.GetString("uplink-grace-drowse"), ent, PopupType.Medium);
        _metaData.SetEntityName(ent, Loc.GetString("uplink-grace-name-asleep"));
        ent.Comp.Awakened = false;
        ent.Comp.Bloodthirst = 0;
        ent.Comp.ForceBonus = 0;
        RemComp<UnremoveableComponent>(ent);
    }
}
