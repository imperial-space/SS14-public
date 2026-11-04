using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

public sealed class CadiaParachutingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaParachutingComponent, ShotAttemptedEvent>(OnShot);
        SubscribeLocalEvent<CadiaParachutingComponent, AttackAttemptEvent>(OnAttack);
        SubscribeLocalEvent<CadiaParachutingComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
    }

    private void OnShot(Entity<CadiaParachutingComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnAttack(EntityUid uid, CadiaParachutingComponent comp, AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnSpeed(Entity<CadiaParachutingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.DriftSpeed, ent.Comp.DriftSpeed);
    }
}
