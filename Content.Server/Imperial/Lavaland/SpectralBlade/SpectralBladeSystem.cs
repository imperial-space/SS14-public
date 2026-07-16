using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Imperial.Lavaland.SpectralBlade;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.SpectralBlade;

public sealed class SpectralBladeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpectralBladeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<SpectralBladeSpiritComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);
    }

    private void OnInteractHand(EntityUid uid, SpectralBladeComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        // Only ghosts can infuse the blade
        if (!HasComp<GhostComponent>(args.User))
            return;

        var now = _timing.CurTime;

        // Cooldown check per ghost
        if (component.InfusionHistory.TryGetValue(args.User, out var lastTime))
        {
            if ((now - lastTime).TotalSeconds < component.InfusionCooldown)
                return;
        }

        // Max charge check
        var maxCharges = GetMaxCharges(component);
        if (component.ChargeLevel >= maxCharges)
            return;

        component.ChargeLevel++;
        component.InfusionHistory[args.User] = now;

        UpdateBladeDamage(uid, component);
        args.Handled = true;
    }

    private void OnGhostRoleSpawnerUsed(EntityUid uid, SpectralBladeSpiritComponent component, GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<SpectralBladeComponent>(args.Spawner, out var blade))
            return;

        _xform.SetParent(args.Spawned, args.Spawner);

        if (blade.ChargeLevel >= GetMaxCharges(blade))
            return;

        blade.ChargeLevel++;
        UpdateBladeDamage(args.Spawner, blade);
    }

    private static int GetMaxCharges(SpectralBladeComponent component)
    {
        return (int) MathF.Ceiling((component.MaxDamage - component.BaseDamage) / component.ChargeStep);
    }

    private void UpdateBladeDamage(EntityUid uid, SpectralBladeComponent component)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        var newDamage = component.BaseDamage + component.ChargeLevel * component.ChargeStep;
        newDamage = Math.Min(newDamage, component.MaxDamage);

        var spec = new DamageSpecifier();
        spec.DamageDict.Add("Slash", FixedPoint2.New((float)newDamage));
        melee.Damage = spec;
        Dirty(uid, melee);
    }
}
