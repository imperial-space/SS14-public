using Content.Shared.Drunk;
using Content.Shared.Movement.Systems;
using Content.Shared.Throwing;

namespace Content.Shared.Imperial.Quirks;

/// <summary>Эффекты причуд, которые клиент должен предсказывать: скорость, бросок, опьянение.</summary>
public sealed class SharedImperialQuirkSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialQuirkSpeedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ImperialQuirkSpeedComponent, AfterAutoHandleStateEvent>(OnSpeedState);
        SubscribeLocalEvent<ImperialQuirkThrowingArmComponent, BeforeThrowEvent>(OnThrow);
        SubscribeLocalEvent<ImperialQuirkAlcoholToleranceComponent, SharedDrunkSystem.DrunkEvent>(OnDrunk);
    }

    /// <summary>Сервер меняет множители и вызывает это, чтобы скорость пересчиталась.</summary>
    public void SetSpeed(Entity<ImperialQuirkSpeedComponent> ent, float walk, float sprint)
    {
        if (MathHelper.CloseTo(ent.Comp.Walk, walk) && MathHelper.CloseTo(ent.Comp.Sprint, sprint))
            return;

        ent.Comp.Walk = walk;
        ent.Comp.Sprint = sprint;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshSpeed(Entity<ImperialQuirkSpeedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Walk, ent.Comp.Sprint);
    }

    private void OnSpeedState(Entity<ImperialQuirkSpeedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnThrow(Entity<ImperialQuirkThrowingArmComponent> ent, ref BeforeThrowEvent args)
    {
        args.ThrowSpeed *= ent.Comp.Multiplier;
    }

    private void OnDrunk(Entity<ImperialQuirkAlcoholToleranceComponent> ent, ref SharedDrunkSystem.DrunkEvent args)
    {
        args.Duration *= ent.Comp.Multiplier;
    }
}
