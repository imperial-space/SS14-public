using Content.Shared.Movement.Systems;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Захват шеи гарротой (GRAB_NECK): жертва почти не может отойти от душителя.</summary>
public sealed class UplinkGarrotedSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;

    private const float GarrotedSpeed = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkGarrotedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<UplinkGarrotedComponent, ComponentStartup>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<UplinkGarrotedComponent, ComponentShutdown>(OnRefreshMovementSpeedModifiers);
    }

    private void OnRefreshSpeed(Entity<UplinkGarrotedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.ModifySpeed(GarrotedSpeed, GarrotedSpeed);
    }

    private void OnRefreshMovementSpeedModifiers<T>(Entity<UplinkGarrotedComponent> ent, ref T args)
    {
        _speed.RefreshMovementSpeedModifiers(ent);
    }

}
