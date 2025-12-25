using Content.Server.Cult.Components;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.DurabilityDisplay.Components;
using Content.Shared.Imperial.Medieval.MedievalItemRustComponent;
using Content.Shared.Interaction;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.MedievalMeleeResource;

public sealed class MedievalMeleeResourceSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalMeleeResourceComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<MedievalMeleeResourceComponent, ComponentStartup>(OnStart);
        SubscribeLocalEvent<MeleeWeaponComponent, ComponentStartup>(OnWeaponStart);
        SubscribeLocalEvent<MedievalMeleeRepairItemComponent, BeforeRangedInteractEvent>(OnUseInHand);
        SubscribeLocalEvent<MedievalMeleeRepairStructureComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MedievalMeleeRepairStructureComponent, MeleeRepairDoAfterEvent>(OnIgnitionDoAfter);
    }

    private void OnInteractUsing(EntityUid uid, MedievalMeleeRepairStructureComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<MedievalMeleeResourceComponent>(args.Used))
            return;

        var doAfterArgs =
            new DoAfterArgs(EntityManager, args.User, 4f, new MeleeRepairDoAfterEvent(), uid, args.Used, uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnDropItem = true
            };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnIgnitionDoAfter(EntityUid uid,
        MedievalMeleeRepairStructureComponent comp,
        MeleeRepairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        OnUse(args.Target, args.User, args.Used, comp.Resource);
        args.Handled = true;
    }

    public void OnUseInHand(EntityUid uid, MedievalMeleeRepairItemComponent comp, BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;

        OnUse(args.Target, args.User, uid, comp.Resource);
    }

    public void OnUse(EntityUid? target, EntityUid user, EntityUid? used, float resources)
    {
        if (target is not { } targetEnt)
            return;
        if (!TryComp<MedievalMeleeResourceComponent>(targetEnt, out var resource))
            return;

        if (HasComp<MedievalMeleeRepairManComponent>(user))
            resource.Resource += resources * 2;
        else
        {
            resource.Resource += resources;
            resource.ResourceWaste = MathF.Min(resource.ResourceWaste * 1.05f, resource.MaxResource);
        }

        if (resource.Resource > resource.MaxResource)
            resource.Resource = resource.MaxResource;

        Dirty(targetEnt, resource);

        _audioSystem.PlayPvs(resource.EffectSoundOnRepair, targetEnt);

        CheckResource(targetEnt, resource);

        if (used is { } usedEnt && HasComp<MedievalMeleeRepairItemComponent>(usedEnt))
        {
            var deleteEnt = usedEnt;
            Timer.Spawn(0, () => QueueDel(deleteEnt));
        }
    }


    public void CheckResource(EntityUid uid, MedievalMeleeResourceComponent component)
    {
        if (TryComp<MedievalItemRustComponent>(uid, out var rustComponent))
        {
            rustComponent.RustPercentage = 1.0f - component.Resource / component.MaxResource;

            Dirty(uid, rustComponent);
        }

        var newDurability = DurabilityDisplayComponent.Durability.Broken;

        if (component.Resource > 100f)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                weapon.Damage = component.UpDamage;

            if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            {
#pragma warning disable RA0002
                wield.BonusDamage = component.UpWieldDamage;
#pragma warning restore RA0002
            }

            component.DamageState = "Up";
            newDurability = DurabilityDisplayComponent.Durability.Up;
        }
        else if (component.Resource > 80f)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                weapon.Damage = component.FullDamage;

            if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            {
#pragma warning disable RA0002
                wield.BonusDamage = component.FullWieldDamage;
#pragma warning restore RA0002
            }

            component.DamageState = "Full";
            newDurability = DurabilityDisplayComponent.Durability.Full;
        }
        else if (component.Resource > 60f)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                weapon.Damage = component.AlmostFullDamage;

            if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            {
#pragma warning disable RA0002
                wield.BonusDamage = component.AlmostFullWieldDamage;
#pragma warning restore RA0002
            }

            component.DamageState = "AlmostFull";
            newDurability = DurabilityDisplayComponent.Durability.AlmostFull;
        }
        else if (component.Resource > 40f)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                weapon.Damage = component.DamagedDamage;

            if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            {
#pragma warning disable RA0002
                wield.BonusDamage = component.DamagedWieldDamage;
#pragma warning restore RA0002
            }

            component.DamageState = "Damaged";
            newDurability = DurabilityDisplayComponent.Durability.Damaged;
        }
        else if (component.Resource > 20f)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                weapon.Damage = component.BadlyDamagedDamage;

            if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            {
#pragma warning disable RA0002
                wield.BonusDamage = component.BadlyDamagedWieldDamage;
#pragma warning restore RA0002
            }

            component.DamageState = "BadlyDamaged";
            newDurability = DurabilityDisplayComponent.Durability.BadlyDamaged;
        }
        else if (component.Resource > 0f)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                weapon.Damage = component.BrokenDamage;

            if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            {
#pragma warning disable RA0002
                wield.BonusDamage = component.BrokenWieldDamage;
#pragma warning restore RA0002
            }

            component.DamageState = "Broken";
            newDurability = DurabilityDisplayComponent.Durability.Broken;
        }

        if (component.Resource == 0)
        {
            _audioSystem.PlayStatic(
                component.EffectSoundOnBreak,
                Filter.Pvs(uid.ToCoordinates(), 1, EntityManager, _playerManager),
                uid.ToCoordinates(),
                true
            );

            QueueDel(uid);
        }

        if (TryComp<DurabilityDisplayComponent>(uid, out var dur))
        {
            dur.Dub = newDurability;
            Dirty(uid, dur);
        }
    }


    private void OnWeaponStart(EntityUid uid, MeleeWeaponComponent component, ComponentStartup args)
    {
        if (HasComp<ExaminerComponent>(uid))
            return;

        EnsureComp<MedievalMeleeResourceComponent>(uid);
        EnsureComp<CultBloodMeleeComponent>(uid);
        EnsureComp<DurabilityDisplayComponent>(uid);
    }

    public void OnStart(EntityUid uid, MedievalMeleeResourceComponent component, ComponentStartup args)
    {
        if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
        {
            if (component.BaseDamage is null)
                component.BaseDamage = weapon.Damage;

            RebuildDamageFromBase(uid, component, weapon);
        }

        if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
        {
            if (component.BaseWieldBonus is null)
                component.BaseWieldBonus = wield.BonusDamage;

            RebuildWieldBonusFromBase(uid, component, wield);
        }

        Dirty(uid, component);
    }


    public void RebuildDamageFromBase(
        EntityUid uid,
        MedievalMeleeResourceComponent component,
        MeleeWeaponComponent? weapon = null)
    {
        if (weapon == null && !TryComp(uid, out weapon))
            return;

        var baseDamage = component.BaseDamage ?? weapon.Damage;
        var q = component.QualityMultiplier <= 0f ? 1f : component.QualityMultiplier;

        component.UpDamage = baseDamage * (q * component.UpModifier);
        component.FullDamage = baseDamage * (q * component.FullModifier);
        component.AlmostFullDamage = baseDamage * (q * component.AlmostFullModifier);
        component.DamagedDamage = baseDamage * (q * component.DamagedModifier);
        component.BadlyDamagedDamage = baseDamage * (q * component.BadlyDamagedModifier);
        component.BrokenDamage = baseDamage * (q * component.BrokenModifier);
    }

    public void RebuildWieldBonusFromBase(
        EntityUid uid,
        MedievalMeleeResourceComponent component,
        IncreaseDamageOnWieldComponent? wield = null)
    {
        if (wield == null && !TryComp(uid, out wield))
            return;

        var baseBonus = component.BaseWieldBonus ?? wield.BonusDamage;
        var q = component.QualityMultiplier <= 0f ? 1f : component.QualityMultiplier;

        component.UpWieldDamage = baseBonus * (q * component.UpModifier);
        component.FullWieldDamage = baseBonus * (q * component.FullModifier);
        component.AlmostFullWieldDamage = baseBonus * (q * component.AlmostFullModifier);
        component.DamagedWieldDamage = baseBonus * (q * component.DamagedModifier);
        component.BadlyDamagedWieldDamage = baseBonus * (q * component.BadlyDamagedModifier);
        component.BrokenWieldDamage = baseBonus * (q * component.BrokenModifier);
    }

    private void OnMeleeHit(EntityUid uid, MedievalMeleeResourceComponent component, MeleeHitEvent args)
    {
        component.Resource -= component.ResourceWaste * CheckBadTarget(uid, component, args);

        if (component.Resource < 0f)
            component.Resource = 0f;
        else if (component.Resource > component.MaxResource)
            component.Resource = component.MaxResource;

        Dirty(uid, component);
        CheckResource(uid, component);
        // Full
        // AlmostFull
        // Damaged
        // BadlyDamaged
        // Broken
    }

    public float CheckBadTarget(EntityUid uid, MedievalMeleeResourceComponent component, MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            if (TryComp<MedievalMeleeBadTargetComponent>(hit, out var target) &&
                target.SafeToHitGroup != component.SafeToHitGroup)
                return target.BreakMultiplier;
        }

        return 1.0f;
    }
}
