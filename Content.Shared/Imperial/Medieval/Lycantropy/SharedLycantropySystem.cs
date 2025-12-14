using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Dash;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

public abstract partial class SharedLycantropySystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WerewolfMoonRageComponent, MapInitEvent>(RefreshMoveSpeed);
        SubscribeLocalEvent<WerewolfMoonRageComponent, ComponentShutdown>(RefreshMoveSpeed);
        SubscribeLocalEvent<WerewolfHotBloodComponent, MapInitEvent>(RefreshMoveSpeed);
        SubscribeLocalEvent<WerewolfHotBloodComponent, ComponentShutdown>(RefreshMoveSpeed);

        SubscribeLocalEvent<WerewolfMoonRageComponent, RefreshMovementSpeedModifiersEvent>(OnMoonSpeedModify);
        SubscribeLocalEvent<WerewolfMoonRageComponent, BeforeStaminaDamageEvent>(OnMoonStaminaModify);
        SubscribeLocalEvent<WerewolfHotBloodComponent, RefreshMovementSpeedModifiersEvent>(OnHotBloodSpeedModify);
        SubscribeLocalEvent<WerewolfBloodHuntComponent, CheckDashStaminaCostModifiersEvent>(OnBloodHuntDash);
        SubscribeLocalEvent<WerewolfBloodHuntComponent, CheckDashCooldownModifiersEvent>(OnBloodHuntDashDelay);
        SubscribeLocalEvent<WerewolfShadowDashComponent, StartCollideEvent>(OnSDashCollide);
        SubscribeLocalEvent<WerewolfShadowDashComponent, DashEndedEvent>(OnSDashEnded);
    }

    private void RefreshMoveSpeed(EntityUid uid, Component comp, EntityEventArgs args)
        => _moveSpeed.RefreshMovementSpeedModifiers(uid);

    private void OnMoonSpeedModify(EntityUid uid, WerewolfMoonRageComponent comp, RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(comp.Modifier);

    private void OnMoonStaminaModify(EntityUid uid, WerewolfMoonRageComponent comp, BeforeStaminaDamageEvent args)
        => args.Value *= 0;

    private void OnHotBloodSpeedModify(EntityUid uid, WerewolfHotBloodComponent comp, RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(comp.Modifier);

    private void OnBloodHuntDash(EntityUid uid, WerewolfBloodHuntComponent comp, ref CheckDashStaminaCostModifiersEvent args)
    {
        args.Modifier *= 0;
    }

    private void OnBloodHuntDashDelay(EntityUid uid, WerewolfBloodHuntComponent comp, ref CheckDashCooldownModifiersEvent args)
    {
        args.Modifier *= 0;
    }

    private void OnSDashCollide(EntityUid uid, WerewolfShadowDashComponent comp, ref StartCollideEvent args)
    {
        if (!HasComp<StaminaComponent>(args.OtherEntity) || !_timing.IsFirstTimePredicted)
            return;

        if (!TryComp<MedievalDashComponent>(uid, out var dash) || !dash.IsDashing)
            return;

        _stamina.TakeStaminaDamage(args.OtherEntity, 200, source: uid, ignoreResist: true);
        RemComp<WerewolfShadowDashComponent>(uid);
    }

    private void OnSDashEnded(EntityUid uid, WerewolfShadowDashComponent comp, ref DashEndedEvent args)
    {
        RemComp(uid, comp);
    }
}

[Serializable, NetSerializable]
public enum WerewolfBloodFeelVisuals
{
    Active
}
