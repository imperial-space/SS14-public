using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.MobRiding
{
    public sealed partial class RideableSprintSystem : AbstractRideableSystem
    {
        [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;
        [Dependency] private readonly INetManager _netManager = default!;
        [Dependency] private readonly SharedStunSystem _stun = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

        #region Handler
        private sealed class SprintInputCmdHandler : InputCmdHandler
        {
            private readonly RideableSprintSystem _rideable;

            public SprintInputCmdHandler(RideableSprintSystem system)
            {
                _rideable = system;
            }

            public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            {
                if (session?.AttachedEntity is not { Valid: true } player || !entManager.EntityExists(player))
                    return false;
                _rideable.ToggleSprint(session, message.State == BoundKeyState.Down);
                return false;
            }
        }
        #endregion

        private void ToggleSprint(ICommonSession? session, bool sprinting)
        {
            if (session == null)
                return;

            RaiseNetworkEvent(new ToggleRideSprintEvent(session.UserId, sprinting));
        }

        #region Initialization
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RideableSprintComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedRefresh);
            SubscribeLocalEvent<RideableSprintComponent, StopRideEvent>(OnUnbuckled);
            SubscribeLocalEvent<RideableSprintComponent, StartCollideEvent>(HandleCollide);

            SubscribeAllEvent<ToggleRideSprintEvent>(OnToggleSprint);
            CommandBinds.Builder
                .Bind(ContentKeyFunctions.MedievalDash, new SprintInputCmdHandler(this))
                .Register<RideableSprintSystem>();
        }
        #endregion

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            #region Sprint logic
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

                rideableSprintComp.CurrentTime = rideableSprintComp.Sprinting
                    ? MathF.Min(rideableSprintComp.CurrentTime + frameTime, rideableSprintComp.AccelerationTime)
                    : MathF.Max(rideableSprintComp.CurrentTime - frameTime * 2, 0);
                var progress = rideableSprintComp.CurrentTime / rideableSprintComp.AccelerationTime;

                rideableSprintComp.CurrentSpeedModifier = MathHelper.Lerp(
                    rideableSprintComp.BaseSpeedModifier,
                    rideableSprintComp.MaxSpeedModifier,
                    progress);

                rideableSprintComp.Dirty();

                _speedModifier.RefreshMovementSpeedModifiers(rideable);
            }
            #endregion
        }



        #region Collide
        private void HandleCollide(EntityUid uid, RideableSprintComponent comp, ref StartCollideEvent args)
        {
            if (!args.OurFixture.Hard)
                return;

            if (comp.CurrentTime < 0.02f)
                return;

            if (!TryComp<RideableComponent>(uid, out var rideable))
                return;

            if (args.OurFixtureId == rideable.PikeShapeId)
                return;


            if (HasComp<BodyComponent>(args.OtherEntity)
                && !HasComp<RideableComponent>(args.OtherEntity)
                && (!TryComp<BuckleComponent>(args.OtherEntity, out var buckle) || !buckle.Buckled))
                CollidePlayer(uid, comp, rideable, ref args);
            else if(args.OtherFixture.Hard && !HasComp<IgnoredByRideableSprintComponent>(args.OtherEntity) && !HasComp<BodyComponent>(args.OtherEntity))
                CollideObject(uid, comp, rideable, ref args);
        }

        private bool CheckSpear(EntityUid uid, [NotNullWhen(true)] out EntityUid? item)
        {
            item = null;
            if (!HasComp<HandsComponent>(uid))
                return false;

            item = _hands.GetActiveItem(uid);

            if (!item.HasValue)
                return false;

            if (!HasComp<SpearComponent>(item.Value))
                return false;

            if (!TryComp<WieldableComponent>(item.Value, out var weldable))
                return false;

            return weldable.Wielded;
        }

        private void CollidePlayer(EntityUid uid, RideableSprintComponent sprintComp, RideableComponent rideableComp, ref StartCollideEvent args)
        {
            var otherPlayer = args.OtherEntity;

            if (!rideableComp.Rider.HasValue)
                return;
            var rider = rideableComp.Rider.Value;

            if (CheckSpear(otherPlayer, out var spear))
            {
                if (!TryComp<MeleeWeaponComponent>(spear, out var melee))
                    return;

                var dmg = melee.Damage * 4;
                ThrowFromRideable(rider, 4, dmg, 0.6f);
                return;
            }

            if (rideableComp.StunList.TryGetValue(otherPlayer, out var time))
            {
                var diff = _gameTiming.CurTime - time;
                if (diff.Duration() < TimeSpan.FromSeconds(5))
                    return;
            }

            if (!TryComp<PhysicsComponent>(uid, out var rideablePhysics))
                return;
            var direction = rideablePhysics.LinearVelocity;

            _throwing.TryThrow(otherPlayer, direction * 0.6f);
            _stun.TryAddStunDuration(otherPlayer, TimeSpan.FromSeconds(2));
            _stun.TryKnockdown(otherPlayer, TimeSpan.FromSeconds(2), true);
            var ev = new GetHorseDamageModifier();
            RaiseLocalEvent(uid, ref ev);

            if (TryComp<InventoryComponent>(uid, out var inv))
                _inventory.RelayEvent((uid, inv), ref ev);

            var modifier = ev.Modifier;
            var damage = sprintComp.DamageOnCollide;

            _damageable.TryChangeDamage(otherPlayer, damage * modifier);

            _popup.PopupClient("Вы сшибаете пехотинца с дороги.", rider, rider);

            if (!rideableComp.StunList.TryAdd(otherPlayer, _gameTiming.CurTime))
                rideableComp.StunList[otherPlayer] = _gameTiming.CurTime;

            rideableComp.Dirty();
        }

        private void CollideObject(EntityUid uid, RideableSprintComponent sprintComp, RideableComponent rideableComp, ref StartCollideEvent args)
        {
            if (!rideableComp.Rider.HasValue)
                return;

            var rider = rideableComp.Rider.Value;
            ThrowFromRideable(rider, damage:sprintComp.RiderDamageOnCollide, throwingDistance: 0.6f);
        }
        #endregion

        #region Sprinting events

        private void OnSpeedRefresh(EntityUid ride, RideableSprintComponent comp, RefreshMovementSpeedModifiersEvent args)
        {
            args.ModifySpeed(comp.CurrentSpeedModifier, comp.CurrentSpeedModifier);
        }

        private void OnToggleSprint(ToggleRideSprintEvent ev, EntitySessionEventArgs args)
        {
            if (!_playerManager.TryGetSessionById(ev.UserId, out var session))
                return;

            var player = session.AttachedEntity;

            if (!TryComp<BuckleComponent>(player, out var buckle))
                return;

            if (!buckle.BuckledTo.HasValue)
                return;

            var rideable = buckle.BuckledTo.Value;

            if (!TryComp<RideableSprintComponent>(rideable, out var sprintComp))
                return;

            _popup.PopupClient(ev.Sprinting ? "Вы ускоряетесь!" : "Вы сбавляете ход.", player.Value, player.Value);

            sprintComp.Sprinting = ev.Sprinting;
        }

        private void OnUnbuckled(EntityUid uid, RideableSprintComponent comp, StopRideEvent args)
        {
            comp.Sprinting = false;
            comp.CurrentTime = 0;
            comp.CurrentSpeedModifier = comp.BaseSpeedModifier;

            comp.Dirty();
            _speedModifier.RefreshMovementSpeedModifiers(uid);
        }
        #endregion
    }
}
#region Events
[NetSerializable, Serializable]
public sealed class ToggleRideSprintEvent : EntityEventArgs
{
    public NetUserId UserId;
    public bool Sprinting;
    public ToggleRideSprintEvent(NetUserId userId, bool sprinting)
    {
        UserId = userId;
        Sprinting = sprinting;
    }
}

[ByRefEvent]
public sealed partial class GetHorseDamageModifier : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;
    public float Modifier;

    public GetHorseDamageModifier(float modifier = 1)
    {
        Modifier = modifier;
    }
}
#endregion
