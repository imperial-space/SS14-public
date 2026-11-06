using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

public sealed class SharedSecwaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecwayComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<SecwayComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Blocked)
            args.ModifySpeed(0f, 0f);
    }
}
