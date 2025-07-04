using Content.Shared.Buckle.Components;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.MobRiding
{
    public sealed partial class RideableSprintSystem : EntitySystem
    {
        [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;
        [Dependency] private readonly INetManager _netManager = default!;

        private sealed class SprintInputCmdHandler : InputCmdHandler
        {
            public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            {
                if (session?.AttachedEntity is not { Valid: true } player || !entManager.EntityExists(player))
                    return false;

                entManager.RaisePredictiveEvent(new ToggleRideSprintEvent(message.State == BoundKeyState.Down));
                return true;
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RideableSprintComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedRefresh);
            SubscribeAllEvent<ToggleRideSprintEvent>(OnToggleSprint);



            CommandBinds.Builder
                .Bind(ContentKeyFunctions.MedievalDash, new SprintInputCmdHandler())
                .Register<RideableSprintSystem>();
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if(_netManager.IsClient)
                return;

            var enumerator = EntityQueryEnumerator<BuckleComponent, InputMoverComponent>();

            while (enumerator.MoveNext(out var player, out var buckle, out var input))
            {
                if (!buckle.BuckledTo.HasValue)
                    continue;

                var rideable = buckle.BuckledTo.Value;
                if (!TryComp<RideableSprintComponent>(rideable, out var rideableSprintComp))
                    continue;

                if (rideableSprintComp.Sprinting)
                    rideableSprintComp.CurrentTime = MathF.Min(rideableSprintComp.CurrentTime + frameTime, rideableSprintComp.AccelerationTime);
                else
                    rideableSprintComp.CurrentTime = 0;
                var progress = rideableSprintComp.CurrentTime / rideableSprintComp.AccelerationTime;

                rideableSprintComp.CurrentSpeedModifier = MathHelper.Lerp(
                    rideableSprintComp.BaseSpeedModifier,
                    rideableSprintComp.MaxSpeedModifier,
                    progress);

                rideableSprintComp.Dirty();

                _speedModifier.RefreshMovementSpeedModifiers(rideable);
            }
        }

        private void OnSpeedRefresh(EntityUid ride, RideableSprintComponent comp, RefreshMovementSpeedModifiersEvent args)
        {
            args.ModifySpeed(comp.CurrentSpeedModifier, comp.CurrentSpeedModifier);
        }

        private void OnToggleSprint(ToggleRideSprintEvent ev, EntitySessionEventArgs args)
        {
            var player = args.SenderSession.AttachedEntity;

            if (!TryComp<BuckleComponent>(player, out var buckle))
                return;

            if (!buckle.BuckledTo.HasValue)
                return;

            var rideable = buckle.BuckledTo.Value;

            if (!TryComp<RideableSprintComponent>(rideable, out var sprintComp))
                return;

            sprintComp.Sprinting = ev.Sprinting;
        }
    }
}

[NetSerializable, Serializable]
public sealed class ToggleRideSprintEvent : EntityEventArgs
{
    public bool Sprinting;
    public ToggleRideSprintEvent(bool sprinting)
    {
        Sprinting = sprinting;
    }
}
