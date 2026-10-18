using Robust.Shared.Random;
using System.Numerics;
using Content.Shared.Armor;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>datum/martial_art/the_sleeping_carp (sleeping_carp.dm).</summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly Robust.Shared.Containers.SharedContainerSystem _container = default!;

    private const string CarpFaction = "Dragon";

    private const string CarpWristWrench = "HG";
    private const string CarpLaunchKick = "HD";
    private const string CarpDropKick = "DD";
    private const string CarpKneeStomach = "GH";

    /// <summary>exempt_objects: с этим оружием в руках стиль не страдает.</summary>
    private static readonly ProtoId<TagPrototype>[] CarpExemptTags =
    [
        "Knife", "KitchenKnife", "CombatKnife", "UtilityKnife", "Spear", "Toolbox", "Mop", "MopAdv", "MopBasic",
        "BaseballBat", "CaneBlade", "Crowbar", "PlushieCarp", "PlushieCarpHolo", "PlushieCarpMagi",
    ];

    /// <summary>CARP_STYLE_FACTOR у одежды, которая уже есть в SS14.</summary>
    private static readonly HashSet<string> CarpStyleClothing =
    [
        "ClothingHeadHatHoodCarp", "ClothingOuterSuitCarp", "ClothingShoesBootsLaceup", "ClothingOuterCoatTrench",
        "ClothingOuterCoatTrenchLawyer", "ClothingHeadHatTacticalMaidHeadband", "ClothingUniformJumpsuitChameleon",
        "ClothingOuterChameleon", "ClothingEyesChameleon", "ClothingHandsChameleon", "ClothingHeadHatChameleon",
        "ClothingMaskGasChameleon", "ClothingMaskGasVoiceChameleon", "ClothingShoesChameleon",
    ];

    private void InitializeCarp()
    {
        SubscribeLocalEvent<MartialArtsComponent, ProjectileReflectAttemptEvent>(OnProjectileReflect);
    }

    private bool CarpCheckStreak(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        var streak = ent.Comp.Streak;
        if (streak.Contains(CarpWristWrench))
        {
            ResetStreak(ent.Comp);
            return CarpWristWrenchMove(ent, target);
        }

        if (streak.Contains(CarpLaunchKick))
        {
            ResetStreak(ent.Comp);
            return CarpLaunchKickMove(ent, target);
        }

        if (streak.Contains(CarpDropKick))
        {
            ResetStreak(ent.Comp);
            return CarpDropKickMove(ent, target);
        }

        if (streak.Contains(CarpKneeStomach))
        {
            ResetStreak(ent.Comp);
            return CarpKneeStomachMove(ent, target);
        }

        return false;
    }

    /// <summary>Скрежещущие зубы: выкрутить руку и выбить всё из рук.</summary>
    private bool CarpWristWrenchMove(EntityUid user, EntityUid target)
    {
        if (!HasComp<HandsComponent>(target))
            return false;

        PopupPair(user, target, "martial-carp-wrist-others", "martial-carp-wrist-self", "martial-carp-wrist-target");
        Sound(target, "/Audio/Weapons/punch1.ogg", -6f);
        Brute(target, 20, user);
        DropAll(target);
        return true;
    }

    /// <summary>Сокрушительный удар волны: отправить в полёт на семь клеток.</summary>
    private bool CarpLaunchKickMove(EntityUid user, EntityUid target)
    {
        PopupPair(user, target, "martial-carp-launch-others", "martial-carp-launch-self", "martial-carp-launch-target");
        Sound(user, "/Audio/Effects/hit_kick.ogg");
        ThrowAway(target, user, 7, 4);
        Brute(target, 15, user);
        return true;
    }

    /// <summary>Под килем: сбить с ног или обезоружить лежачего.</summary>
    private bool CarpDropKickMove(EntityUid user, EntityUid target)
    {
        Sound(user, "/Audio/Effects/hit_kick.ogg");
        if (!IsLying(target))
        {
            Knockdown(target, TimeSpan.FromSeconds(4));
            PopupPair(user, target, "martial-carp-keelhaul-others", "martial-carp-keelhaul-self", "martial-carp-keelhaul-target");
        }
        else
        {
            DropAll(target);
            PopupPair(user, target, "martial-carp-keelhaul-down-others", "martial-carp-keelhaul-down-self", "martial-carp-keelhaul-down-target");
        }

        Stamina(target, 40, user);
        Confuse(target, TimeSpan.FromSeconds(10));
        Blind(target, TimeSpan.FromSeconds(2));
        return true;
    }

    /// <summary>Крушение Кракена: колено в живот, сбитое дыхание и немота.</summary>
    private bool CarpKneeStomachMove(EntityUid user, EntityUid target)
    {
        Sound(user, "/Audio/Effects/hit_kick.ogg");
        PopupPair(user, target, "martial-carp-kraken-others", "martial-carp-kraken-self", "martial-carp-kraken-target");
        Stamina(target, 20, user);
        Paralyze(target, TimeSpan.FromSeconds(1));
        Silence(target, TimeSpan.FromSeconds(5));
        LoseBreath(target, 5);
        return true;
    }

    private MartialResult CarpGrab(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (!CarpCanDeflect(ent))
            return MartialResult.Invalid;

        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, "G", target);
        if (CarpCheckStreak(ent, target))
            return MartialResult.Success;

        Sound(target, "/Audio/Weapons/punch1.ogg", -6f);

        // Нервный зажим: выдохшегося противника — в отключку.
        if (!_mobs.IsDead(target) && IsConscious(target) && StaminaLoss(target) >= 80)
        {
            PopupPair(ent, target, "martial-carp-pinch-others", "martial-carp-pinch-self", "martial-carp-pinch-target");
            Sleep(target, TimeSpan.FromSeconds(10));
        }

        Stamina(target, 20, ent);
        return MartialResult.Invalid; // обычный захват
    }

    private MartialResult CarpHarm(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (TrySnapNeck(ent, target))
            return MartialResult.Success;

        if (CheckBlock(target, ent, 10, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, "H", target);
        return CarpCheckStreak(ent, target) ? MartialResult.Success : MartialResult.Invalid;
    }

    private MartialResult CarpDisarm(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (!CarpCanDeflect(ent))
            return MartialResult.Invalid;

        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, "D", target);
        if (CarpCheckStreak(ent, target))
            return MartialResult.Success;

        Sound(target, "/Audio/Weapons/punch1.ogg", -6f);
        Stamina(target, 20, ent);
        return MartialResult.Invalid; // обычный толчок
    }

    /// <summary>can_deflect(): только в боевом режиме, на ногах и не в мехе.</summary>
    private bool CarpCanDeflect(EntityUid user)
    {
        if (!_combat.IsInCombatMode(user) || !IsConscious(user) || HasComp<Content.Shared.Stunnable.StunnedComponent>(user))
            return false;

        return !_container.IsEntityInContainer(user);
    }

    /// <summary>hit_by_projectile(): пули отлетают в случайную сторону.</summary>
    private void OnProjectileReflect(Entity<MartialArtsComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled || GetActive(ent.Comp) is not { Kind: MartialArtKind.SleepingCarp } || !CarpCanDeflect(ent))
            return;

        var avoidance = Math.Clamp(100 + CarpStyleCheck(ent), 0, 100);
        if (!_random.Prob(avoidance / 100f))
            return;

        if (!TryComp<PhysicsComponent>(args.ProjUid, out var physics))
            return;

        var speed = physics.LinearVelocity.Length();
        var angle = _random.NextAngle();
        _physics.SetLinearVelocity(args.ProjUid, angle.ToVec() * speed, body: physics);
        _xform.SetLocalRotation(args.ProjUid, angle);

        args.Component.Shooter = ent;
        args.Component.Weapon = ent;
        Dirty(args.ProjUid, args.Component);
        args.Cancelled = true;

        PopupPair(ent, ent, "martial-carp-deflect-others", "martial-carp-deflect-self");
        Sound(ent, "/Audio/Weapons/Guns/Hits/bullet_hit.ogg");
    }

    /// <summary>check_dodge(): в стиле можно увернуться и от удара в упор.</summary>
    private bool CarpDodge(Entity<MartialArtsComponent> ent, MartialAttackType type)
    {
        if (!CarpCanDeflect(ent) || type is MartialAttackType.Projectile or MartialAttackType.Thrown)
            return false;

        var avoidance = Math.Clamp(CarpStyleCheck(ent), 0, 75);
        if (!_random.Prob(avoidance / 100f))
            return false;

        PopupPair(ent, ent, "martial-carp-dodge-others", "martial-carp-dodge-self");
        Sound(ent, "/Audio/Weapons/punchmiss.ogg", -6f);
        return true;
    }

    /// <summary>
    /// carp_style_check(): чем больше «карпа» в одежде и чем меньше брони и громоздкого в руках,
    /// тем вернее уклонение.
    /// </summary>
    private int CarpStyleCheck(EntityUid user)
    {
        // Космический карп или дракон и так карп настолько, насколько можно.
        if (!IsHuman(user))
            return _faction.IsMember(user, CarpFaction) ? 100 : 0;

        var points = 0;
        var malus = 0f;

        // Голова, грудь и ноги: по 20 очков за одежду в стиле и минус процент брони от ударов.
        foreach (var slot in new[] { "head", "mask", "outerClothing", "jumpsuit", "shoes" })
        {
            if (!_inventory.TryGetSlotEntity(user, slot, out var item))
                continue;

            if (IsCarpStyle(item.Value))
                points += 20;

            if (slot == "shoes" || !TryComp<ArmorComponent>(item, out var armor))
                continue;

            var modifiers = armor.Modifiers;
            var coefficients = modifiers.Coefficients;
            if (coefficients.TryGetValue("Blunt", out var blunt))
                malus += (1f - blunt) * 100f;
        }

        if (TryComp<HandsComponent>(user, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld((user, hands)))
            {
                if (_tag.HasAnyTag(held, CarpExemptTags) || HasComp<BoStaffComponent>(held))
                    continue;

                if (!TryComp<ItemComponent>(held, out var item))
                    continue;

                var weightClass = _item.GetSizePrototype(item.Size).Weight;
                if (weightClass <= 2)
                    continue;

                var wielded = TryComp<WieldableComponent>(held, out var wield) && wield.Wielded;
                malus += weightClass * 10 * (wielded ? 2 : 1);
            }
        }

        if (IsLying(user))
            points -= 30;

        return points - (int) malus;
    }

    private bool IsCarpStyle(EntityUid item)
    {
        return HasComp<CarpStyleFactorComponent>(item)
               || MetaData(item).EntityPrototype?.ID is { } id && CarpStyleClothing.Contains(id);
    }
}
