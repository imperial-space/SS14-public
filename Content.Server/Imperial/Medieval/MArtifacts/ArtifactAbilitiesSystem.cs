

using System.Linq;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Body.Systems;
using Content.Server.MedievalMeleeResource;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Flash;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Artifacts;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nocturn.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Wieldable.Components;

namespace Content.Server.Imperial.Medieval.Artifacts;

public sealed class ArtifacAbilitiestSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly IEntitySystemManager _ent = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ArtifactAddDamageOnInitComponent, ArtifactInit>(DamageInit);
        SubscribeLocalEvent<ArtifactIgniteOnDamageComponent, ArtifactInit>(IgniteInit);
        SubscribeLocalEvent<ArtifactMultiplyDamageComponent, ArtifactInit>(MultiplyInit);
        SubscribeLocalEvent<ArtifactVampirismOnHitComponent, MeleeHitEvent>(VampirismHit);
        SubscribeLocalEvent<ArtifactPenetrateDamageComponent, ArtifactInit>(PenetrateInit);
        SubscribeLocalEvent<ArtifactActionAddComponent, ArtifactInit>(ActionInit);
        SubscribeLocalEvent<ArtifactActionAddComponent, GetItemActionsEvent>(ActionsAdd);
        SubscribeLocalEvent<ArtifactLightWeaponComponent, ArtifactInit>(LightInit);
        SubscribeLocalEvent<ArtifactLightWeaponComponent, MeleeHitEvent>(LightHit);
        SubscribeLocalEvent<ArtifactPoisonComponent, MeleeHitEvent>(PoisonInit);
        SubscribeLocalEvent<ArtifactMidasComponent, MeleeHitEvent>(MidasHit);
        SubscribeLocalEvent<ArtifactThrowComponent, ArtifactInit>(ThrowHit);
        SubscribeLocalEvent<ArtifactExplosionMeleeComponent, MeleeHitEvent>(ExplosionHit);
        SubscribeLocalEvent<ArtifactRangeComponent, ArtifactInit>(RangeInit);
        SubscribeLocalEvent<ArtifactSpeedComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(SpeedApply);
        SubscribeLocalEvent<ArtifactDurabilityComponent, ArtifactInit>(DuraInit);
        SubscribeLocalEvent<ArtifactStaminaDamageComponent, ArtifactInit>(StaminaInit);
    }
    private void StaminaInit(EntityUid _, ArtifactStaminaDamageComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        EnsureComp<StaminaDamageOnHitComponent>(uid).Damage = component.Damage;
    }
    private void DuraInit(EntityUid _, ArtifactDurabilityComponent component, ArtifactInit args)
    {
        if (TryComp<MedievalMeleeResourceComponent>(args.Uid, out var resource))
            resource.ResourceWaste = component.NewWaste;
    }
    private void SpeedApply(EntityUid _, ArtifactSpeedComponent component, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.ModifySpeed(component.Multiplier);
    }
    private void RangeInit(EntityUid _, ArtifactRangeComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        EnsureComp<MeleeWeaponComponent>(uid).Range *= component.Multiplier;
    }
    private void ExplosionHit(EntityUid _, ArtifactExplosionMeleeComponent component, MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        _explosion.QueueExplosion(args.HitEntities.First(), "MedievalDefault", 5f, 2f, 10f, user: args.User);
    }
    private void ThrowHit(EntityUid _, ArtifactThrowComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        var newcomp = EnsureComp<MeleeThrowOnHitComponent>(uid);
        newcomp.Distance = component.Distance;
    }
    private void MidasHit(EntityUid _, ArtifactMidasComponent component, MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            if (!HasComp<HumanoidAppearanceComponent>(target))
                continue;
            if (component.Blacklist.Contains(target))
                continue;
            component.Blacklist.Add(target);
            for (int i = 0; i < component.Amount; i++)
            {
                var coin = Spawn("MedievalRevent", Transform(target).Coordinates);
                _physics.SetLinearVelocity(coin, _random.NextVector2(8.0f, 8.0f));
            }
        }
    }
    private void PoisonInit(EntityUid _, ArtifactPoisonComponent component, MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            if (!HasComp<SolutionContainerManagerComponent>(target))
                continue;
            _blood.TryAddToChemicals(target, new(new List<ReagentQuantity>()
            {
                new("Amatoxin", FixedPoint2.New(component.Amount))
            }, new()));
        }
    }
    private void LightHit(EntityUid _, ArtifactLightWeaponComponent component, MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            if (!HasComp<HumanoidAppearanceComponent>(target))
                continue;
            _flash.Flash(target, args.User, args.Weapon, TimeSpan.FromSeconds(component.FlashTime * 1000), 1f, true, true);
        }
    }
    private void LightInit(EntityUid _, ArtifactLightWeaponComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        var light = EnsureComp<PointLightComponent>(uid);
    }
    public TimeSpan Timer = new();
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);
    public TimeSpan NextTimer = TimeSpan.FromSeconds(5);
    public override void Update(float frameTime)
    {
        if (_timing.CurTime < Timer)
            return;
        Timer = _timing.CurTime + Cooldown;
        foreach (var component in EntityQuery<ArtifactHungerHealComponent>())
        {
            if (!_container.TryGetContainingContainer(EnsureComp<ArtifactAbilityComponent>(component.Owner).OwnerUid, out var container))
                continue;
            if (TryComp<HungerComponent>(container.Owner, out var hunger))
                _hunger.ModifyHunger(container.Owner, component.Amount, hunger);
            if (TryComp<ThirstComponent>(container.Owner, out var thirst))
                _thirst.ModifyThirst(container.Owner, thirst, component.Amount);
        }
    }
    private void ActionInit(EntityUid _, ArtifactActionAddComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        foreach (var action in component.Actions)
        {
            EntityUid? funny = null;
            _actionContainer.EnsureAction(uid, ref funny, action);
            if (funny == null)
                continue;
            component.ActionsCreated.Add(funny.Value);
        }
    }
    private void ActionsAdd(EntityUid _, ArtifactActionAddComponent component, GetItemActionsEvent args)
    {
        foreach (var action in component.ActionsCreated)
        {
            args.AddAction(action);
        }
    }
    private void PenetrateInit(EntityUid _, ArtifactPenetrateDamageComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        var comp = EnsureComp<MeleeWeaponComponent>(uid);
        comp.ResistanceBypass = true;
    }
    private void VampirismHit(EntityUid ouruid, ArtifactVampirismOnHitComponent component, MeleeHitEvent args)
    {
        var uid = EnsureComp<ArtifactAbilityComponent>(ouruid).OwnerUid;
        var passed = false;
        foreach (var target in args.HitEntities)
        {
            if (!HasComp<HumanoidAppearanceComponent>(target))
                continue;
            passed = true;
            break;
        }
        if (!passed)
            return;
        var damage = args.BaseDamage + args.BonusDamage;
        if (!damage.AnyPositive())
            return;
        var heal = new FixedPoint2();
        foreach (var d in damage.DamageDict)
        {
            if (d.Value.Float() < 0)
                continue;

            heal += FixedPoint2.New(d.Value.Float() * -(HasComp<NocturnComponent>(uid) ? component.Multiplier + component.AdditionalMultiplier : component.Multiplier));
        }
        var heald = new DamageSpecifier();
        foreach (var type in _proto.EnumeratePrototypes<DamageTypePrototype>())
        {
            heald.DamageDict[type.ID] = heal;
        }
        _damage.TryChangeDamage(args.User, heald, true, false, null, uid);
        if (TryComp<NocturnComponent>(args.User, out var nocturn))
            nocturn.BloodLevel += component.BloodRestore;
    }
    private void DamageInit(EntityUid _, ArtifactAddDamageOnInitComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;
        melee.Damage += component.Damage;
        _ent.GetEntitySystem<MedievalMeleeResourceSystem>().OnStart(uid, Comp<MedievalMeleeResourceComponent>(uid), new());
    }
    private void IgniteInit(EntityUid _, ArtifactIgniteOnDamageComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        var newcomp = EnsureComp<IgniteOnMeleeHitComponent>(uid);
        newcomp.FireStacks = component.FireStacks;
    }
    private void MultiplyInit(EntityUid _, ArtifactMultiplyDamageComponent component, ArtifactInit args)
    {
        var uid = args.Uid;
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;
        melee.Damage *= component.Multiplier;
        if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            wield.BonusDamage *= component.Multiplier;

        _ent.GetEntitySystem<MedievalMeleeResourceSystem>().OnStart(uid, Comp<MedievalMeleeResourceComponent>(uid), new());
    }
}
