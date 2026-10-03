using Content.Shared.Hands;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared.Imperial.NullRod;

/// <summary>
/// Применяет случайный модификатор скорости аритмичного ножа к тому, кто держит его в руке.
/// Сам модификатор меняет сервер, клиент получает его через состояние компонента.
/// </summary>
public sealed class NullRodArrhythmicSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodArrhythmicComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);
        SubscribeLocalEvent<NullRodArrhythmicComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<NullRodArrhythmicComponent, GotUnequippedHandEvent>(OnUnequipped);
    }

    private void OnRefreshSpeed(Entity<NullRodArrhythmicComponent> ent, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    private void OnEquipped(Entity<NullRodArrhythmicComponent> ent, ref GotEquippedHandEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnUnequipped(Entity<NullRodArrhythmicComponent> ent, ref GotUnequippedHandEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.User);
    }
}
