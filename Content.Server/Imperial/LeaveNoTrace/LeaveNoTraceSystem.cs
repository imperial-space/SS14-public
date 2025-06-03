using Content.Server.Examine;
using Content.Server.Objectives.Systems;
using Content.Shared.Ghost;
using Content.Shared.Imperial.LeaveNoTrace;
using Content.Shared.Objectives.Components;
using Content.Shared.Stealth.Components;
using Robust.Shared.Player;

namespace Content.Server.Imperial.LeaveNoTrace;

public sealed partial class LeaveNoTraceSystem : SharedLeaveNoTraceSystem
{
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeaveNoTraceConditionComponent, ObjectiveGetProgressEvent>(OnLeaveNoTraceAfterAssign);
    }

    private void OnLeaveNoTraceAfterAssign(Entity<LeaveNoTraceConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var player = args.Mind.OwnedEntity;
        args.Progress = HasComp<LeaveNoTraceComponent>(player) ? 1f : 0f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LeaveNoTraceComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.CurTime is <= 0)
            {
                RemCompDeferred<LeaveNoTraceComponent>(uid);
                continue;
            }

            if (TryComp<StealthComponent>(uid, out var stealth) && stealth.Enabled)
            {
                comp.CurTime = comp.TimeForReveal;
                comp.IsSeen = false;
                continue;
            }

            if (comp.IsSeen)
            {
                comp.CurTime -= frameTime;
            }

            var seen = false;

            foreach (var player in _lookup.GetEntitiesInRange<ActorComponent>(Transform(uid).Coordinates, comp.Range, LookupFlags.Dynamic))
            {
                if (HasComp<GhostComponent>(player))
                    continue;

                if (player.Owner == uid)
                    continue;

                if (!_examine.InRangeUnOccluded(uid, player, comp.Range))
                    continue;

                seen = true;
                break;
            }

            if (seen)
            {
                comp.IsSeen = true;
            }
            else
            {
                comp.IsSeen = false;
                comp.CurTime = comp.TimeForReveal;
            }
        }
    }
}
