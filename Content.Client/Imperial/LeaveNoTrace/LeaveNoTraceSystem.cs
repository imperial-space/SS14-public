using Content.Shared.Coordinates;
using Content.Shared.Imperial.LeaveNoTrace;

namespace Content.Client.Imperial.LeaveNoTrace;

public sealed class LeaveNoTraceSystem : SharedLeaveNoTraceSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeaveNoTraceComponent, ComponentRemove>(OnRemove);
        SubscribeNetworkEvent<LeaveNoTraceVisualEvent>(OnSetVisual);
    }

    private void OnRemove(Entity<LeaveNoTraceComponent> ent, ref ComponentRemove args)
    {
        Del(GetEntity(ent.Comp.EffectEntity));
        ent.Comp.EffectEntity = null;
    }

    private void OnSetVisual(LeaveNoTraceVisualEvent args)
    {
        var user = GetEntity(args.Owner);

        if (!TryComp<LeaveNoTraceComponent>(user, out var leaveNoTrace))
            return;

        if (args.IsSeen)
        {
            if (leaveNoTrace.EffectEntity != null)
                return;

            leaveNoTrace.EffectEntity =
                GetNetEntity(SpawnAttachedTo(leaveNoTrace.Effect, user.ToCoordinates()));

            return;
        }

        Del(GetEntity(leaveNoTrace.EffectEntity));
        leaveNoTrace.EffectEntity = null;
    }

}
