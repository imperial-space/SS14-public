using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Electrocution;
using Content.Shared.Eye;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticLockPassiveSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem   _eye     = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming        _timing  = default!;

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<HereticLockPassiveComponent>(uid);
        EnsureComp<InsulatedComponent>(uid);
    }

    public void ApplyPassiveLevel2(EntityUid uid)
    {
        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetDrawFov(uid, false, eye);
    }

    public void TryResetGraspCooldown(EntityUid uid)
    {
        var now = _timing.CurTime;
        foreach (var action in _actions.GetActions(uid))
        {
            if (!TryComp<InstantActionComponent>(action.Owner, out var ia)) continue;
            if (ia.Event is not HereticMansusGraspActionEvent) continue;
            if (action.Comp.Cooldown is not {} cd) break;
            if (cd.End > now)
                _actions.SetCooldown((action.Owner, (ActionComponent?) action.Comp), now, now);
            break;
        }
    }
}
