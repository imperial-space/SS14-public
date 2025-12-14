using System.Numerics;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.Farmer;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Sprint;


public sealed partial class MedievalSprintSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifierSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSprintComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedRefresh);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<MedievalSprintComponent, InputMoverComponent, StaminaComponent, PhysicsComponent>();

        while (enumerator.MoveNext(out var uid, out var component, out var inputMoverComponent, out var staminaComponent, out var physicsComponent))
        {
            if (_timing.CurTime <= component.NextStaminaDamageTime)
                continue;

            if ((inputMoverComponent.HeldMoveButtons & MoveButtons.Walk) == 0 && inputMoverComponent.HeldMoveButtons != MoveButtons.Walk)
            {
                component.Sprinting = false;
                continue;
            }

            if (physicsComponent.LinearVelocity == Vector2.Zero || staminaComponent.Critical)
            {
                component.Sprinting = false;
                continue;
            }

            if (!EnoughStamina(component, staminaComponent))
            {
                component.Sprinting = false;
                continue;
            }

            component.Sprinting = true;

            if (component.Tried)
                _speedModifierSystem.RefreshMovementSpeedModifiers(uid);

            var ev = new GetSprintStaminaDamageModifiersEvent(1f);
            RaiseLocalEvent(uid, ref ev);

            var stam = component.StaminaDamage;
            if (HasComp<FarmerBoostComponent>(uid))
                stam *= 0.3f;

            _staminaSystem.TryTakeStamina(uid, stam * ev.Modifier, ignoreResist: true);

            component.Tried = false;
            component.NextStaminaDamageTime = _timing.CurTime + component.StaminaGainPeriod;

            if (!EnoughStamina(component, staminaComponent))
            {
                _speedModifierSystem.RefreshMovementSpeedModifiers(uid);
                component.Tried = true;
            }
        }
    }

    private void OnSpeedRefresh(EntityUid uid, MedievalSprintComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<StaminaComponent>(uid, out var staminaComponent))
            return;

        if (EnoughStamina(component, staminaComponent))
            return;


        args.ModifySpeed(component.SprintSpeedModifierWhenTried, 1f);
    }

    #region Helpers

    private bool EnoughStamina(MedievalSprintComponent component, StaminaComponent staminaComponent)
    {
        var ev = new CanSprintEvent();
        RaiseLocalEvent(component.Owner, ref ev);
        if (ev.Cancelled)
            return false;

        return staminaComponent.StaminaDamage / staminaComponent.CritThreshold <= component.MinStaminaToSprintPrecent;
    }

    #endregion
}
