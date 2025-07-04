using System.Linq;
using System.Numerics;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.PhaseSpace;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.ActionBlocker;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Imperial.Dash;


public sealed partial class MedievalDashSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.MedievalDash, new PointerInputCmdHandler(DashButtonPressed))
            .Register<MedievalDashSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<MedievalDashComponent>();

        while (enumerator.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.DashEndTime) continue;
            if (!component.IsDashing) continue;

            component.IsDashing = false;


            if (_net.IsClient) return;

            RemComp<PhaseSpaceShadowComponent>(uid);
        }
    }

    private bool DashButtonPressed(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
    {
        if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
            return false;

        if (!CanDash(player, out var component))
            return false;

        if (!TryComp<PhysicsComponent>(player, out var physicsComponent) || physicsComponent.LinearVelocity == Vector2.Zero)
            return false;

        if (!component.RequiredBodyStatus.Contains(physicsComponent.BodyStatus))
            return false;

        if (!TryComp<InputMoverComponent>(player, out var inputMoverComponent) ||
            (inputMoverComponent.HeldMoveButtons & MoveButtons.AnyDirection) == 0)
            return false;

        var targetRotation = physicsComponent.LinearVelocity.ToAngle();

        var force = new Vector2(component.Force * 2);
        var forceDirection = targetRotation - Angle.FromDegrees(45);

        var impulse = forceDirection.RotateVec(force);
        var dashTime = TimeSpan.FromSeconds(component.Force / 990 / physicsComponent.Mass);

        var staminaEv = new CheckDashStaminaCostModifiersEvent(1f);
        RaiseLocalEvent(player, ref staminaEv);

        if (!_staminaSystem.TryTakeStamina(player, component.StaminaDamage * staminaEv.Modifier, ignoreResistances: true))
            return false;

        var distEv = new CheckDashDistanceModifiersEvent(1f);
        RaiseLocalEvent(player, ref distEv);

        // TODO модификатор расстояния

        _physicsSystem.ApplyLinearImpulse(player, impulse);

        var shadowComponent = EnsureComp<PhaseSpaceShadowComponent>(player);

        shadowComponent.ShadowUpdateRate = TimeSpan.Zero;
        shadowComponent.PositionUpdateRate = TimeSpan.Zero;

        component.DashEndTime = dashTime + _timing.CurTime;

        var cooldownEv = new CheckDashCooldownModifiersEvent(1f);
        RaiseLocalEvent(player, ref cooldownEv, true);

        component.NextDash = _timing.CurTime + component.DashReloadTime + TimeSpan.FromSeconds(staminaEv.Modifier);

        component.DashButtonPressedTick = _timing.CurTick;

        component.IsDashing = true;



        return false;
    }

    private bool CanDash(EntityUid uid, [NotNullWhen(true)] out MedievalDashComponent? component)
    {
        if (!TryComp(uid, out component))
            return false;

        var isSimulationTick = _timing.CurTick == component.DashButtonPressedTick;

        if (component.IsDashing && !isSimulationTick)
            return false;

        if (_timing.CurTime < component.NextDash && !isSimulationTick)
            return false;

        if (!_actionBlockerSystem.CanMove(uid))
            return false;

        var ev = new CanDashEvent();
        RaiseLocalEvent(uid, ref ev);
        return !ev.Cancelled;
    }
}
