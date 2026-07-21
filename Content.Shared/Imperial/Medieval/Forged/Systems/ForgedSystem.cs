using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Medieval.Forged;
using Content.Shared.Body.Events;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Movement.Systems;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion;

namespace Content.Shared.Forged;

public sealed class ForgedSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly ForgedAbilitySystem _forgedAbility = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ForgedComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<ForgedComponent, BeingGibbedEvent>(OnGibbed);

        SubscribeLocalEvent<ForgedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ForgedComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ForgedComponent, GetExplosionResistanceEvent>(OnExplosionResistance);

        SubscribeLocalEvent<ForgedComponent, InteractUsingEvent>(OnReloadCrossbow);
    }

    private void OnMapInit(EntityUid uid, ForgedComponent component, MapInitEvent args)
    {
        foreach (var (state, moduleUid) in component.FittedModules)
        {
            if (!TryComp<ForgedModuleComponent>(moduleUid, out var module)) continue;

            if (module.ModuleSlot == "head")
            {
                var container = _containerSystem.EnsureContainer<ContainerSlot>(uid, "forgedhead");
                _containerSystem.Insert(moduleUid, container);
            }
            else if (module.ModuleSlot == "eyes")
            {
                var container = _containerSystem.EnsureContainer<ContainerSlot>(uid, "forgedeyes");
                _containerSystem.Insert(moduleUid, container);
            }
            else if (module.ModuleSlot == "core")
            {
                SetupCore(uid, moduleUid);
            }
            else
            {
                var container = _containerSystem.EnsureContainer<ContainerSlot>(uid, module.ModuleSlot);
                _containerSystem.Insert(moduleUid, container);
            }
            Timer.Spawn(0, () =>
            {
                if (module.AbilityId != null) _forgedAbility.ExecuteAbility(uid, moduleUid, module.AbilityId);
            });
        }

        if (TryComp<AppearanceComponent>(uid, out var appearance)) UpdateAppearance((uid, component, appearance));

        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(uid);
    }

    private void SetupCore(EntityUid uid, EntityUid moduleId)
    {
        Timer.Spawn(0, () =>
        {
            var test = _bodySystem.GetBodyChildren(uid).ToList();
            EntityUid? torsoId = null;
            foreach (var part in _bodySystem.GetBodyChildren(uid))
            {
                if (part.Component.PartType == BodyPartType.Torso)
                {
                    torsoId = part.Id;
                    break;
                }
            }
            if (torsoId == null) return;

            foreach (var organ in _bodySystem.GetBodyOrgans(uid))
            {
                if (HasComp<StomachComponent>(organ.Id))
                {
                    _bodySystem.RemoveOrgan(organ.Id, organ.Component);
                    QueueDel(organ.Id);
                    break;
                }
            }

            _bodySystem.InsertOrgan(torsoId.Value, moduleId, "stomach");
        });
    }

    private void OnGibbed(EntityUid uid, ForgedComponent component, BeingGibbedEvent args)
    {
        foreach (var (slotId, moduleUid) in component.FittedModules)
        {
            if (TerminatingOrDeleted(moduleUid)) continue;
            if (!TryComp<ForgedModuleComponent>(moduleUid, out var module)) continue;
            if (_containerSystem.TryGetContainer(uid, slotId, out var container))
            {
                if (!container.Contains(moduleUid))
                {
                    continue;
                }

                _containerSystem.Remove(moduleUid, container, force: true);

                if (slotId == "torso" || module.AbilityId == "Torso_Explosion")
                {
                    QueueDel(moduleUid);
                    continue;
                }

                if (_random.Prob(0.5f))
                {
                    QueueDel(moduleUid);
                }
            }
        }
    }
    private void UpdateAppearance(Entity<ForgedComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, logMissing: false)) return;

        foreach (ForgedVisuals visualKey in Enum.GetValues(typeof(ForgedVisuals)))
        {
            string key = visualKey.ToString();
            if (ent.Comp1.FittedModules.TryGetValue(key, out var moduleUid) && moduleUid.IsValid() && TryComp<ForgedModuleComponent>(moduleUid, out var module))
            {
                ForgedVisualsPacket packet = new ForgedVisualsPacket(module.LayerState, module.RsiPath);
                _appearanceSystem.SetData(ent, visualKey, packet, ent.Comp2);
            }
            else
            {
                ForgedVisualsPacket packet = new ForgedVisualsPacket("blank", new ResPath("Imperial/Medieval/Forged/wooden.rsi"));
                _appearanceSystem.SetData(ent, visualKey, packet, ent.Comp2);
            }
        }
    }

    private float GetModuleSpeedModifier(ForgedComponent component)
    {
        float speedMod = 1f;

        foreach (var (state, moduleUid) in component.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(moduleUid, out var module))
                speedMod += module.SpeedModifier;
        }

        return Math.Max(0.1f, speedMod);
    }

    private float GetModuleResistanceModifier(ForgedComponent component)
    {
        float damageMod = 1f;

        foreach (var (state, moduleUid) in component.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(moduleUid, out var module))
                damageMod -= module.ResistanceModifier;
        }

        return Math.Max(0.01f, damageMod);
    }

    private void OnExplosionResistance(EntityUid uid, ForgedComponent component, ref GetExplosionResistanceEvent args)
    {
        float mod = GetModuleResistanceModifier(component);
        
        args.DamageCoefficient *= mod;
    }

    private void OnRefreshSpeed(EntityUid uid, ForgedComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        float mod = GetModuleSpeedModifier(component);
        args.ModifySpeed(mod, mod);
    }

    private void OnDamageModify(EntityUid uid, ForgedComponent component, DamageModifyEvent args)
    {
        float mod = GetModuleResistanceModifier(component);
        args.Damage *= mod;
    }

    private void OnReloadCrossbow(EntityUid forgedUid, ForgedComponent comp, InteractUsingEvent args)
    {
        if (args.Handled) return;

        if (!_hands.TryGetActiveItem(forgedUid, out var activeItem)) return;

        ProtoId<TagPrototype> tag = "ForgedArmCrossbow";
        if (!_tagSystem.HasTag(activeItem.Value, tag)) return;

        var weaponInteractArgs = new InteractUsingEvent(args.User, args.Used, activeItem.Value, args.ClickLocation);
        RaiseLocalEvent(activeItem.Value, weaponInteractArgs);
        if (weaponInteractArgs.Handled) args.Handled = true;
    }
}
