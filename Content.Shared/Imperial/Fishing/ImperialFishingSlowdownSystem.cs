using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Тяжёлая рыба или полный переносной аквариум замедляют того, кто их держит (slowdown / GET_FISH_SLOWDOWN).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishingSlowdownComponent : Component
{
    /// <summary>Замедление в единицах TG (добавка к задержке шага).</summary>
    [DataField, AutoNetworkedField]
    public float Slowdown;
}

public sealed class ImperialFishingSlowdownSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    /// <summary>Базовая задержка шага TG, к которой прибавляется замедление.</summary>
    private const float BaseDelay = 2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFishingSlowdownComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<ImperialFishingSlowdownComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<ImperialFishingSlowdownComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefresh);
    }

    public void SetSlowdown(EntityUid uid, float slowdown)
    {
        if (slowdown <= 0)
        {
            if (!RemComp<ImperialFishingSlowdownComponent>(uid))
                return;
        }
        else
        {
            var comp = EnsureComp<ImperialFishingSlowdownComponent>(uid);
            if (MathHelper.CloseTo(comp.Slowdown, slowdown))
                return;

            comp.Slowdown = slowdown;
            Dirty(uid, comp);
        }

        if (_container.TryGetContainingContainer(uid, out var container))
            _movement.RefreshMovementSpeedModifiers(container.Owner);
    }

    private void OnEquipped(Entity<ImperialFishingSlowdownComponent> ent, ref GotEquippedHandEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnUnequipped(Entity<ImperialFishingSlowdownComponent> ent, ref GotUnequippedHandEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnRefresh(Entity<ImperialFishingSlowdownComponent> ent, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        var modifier = Math.Clamp(BaseDelay / (BaseDelay + ent.Comp.Slowdown), 0.25f, 1f);
        args.Args.ModifySpeed(modifier, modifier);
    }
}
