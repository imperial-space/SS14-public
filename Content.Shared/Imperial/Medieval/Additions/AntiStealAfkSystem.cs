using Content.Shared.Interaction.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Additions;

public partial class AntiStealAfkSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _tick = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<AntiStealAfkComponent, PlayerDetachedEvent>(Leave);
    }
    public void Leave(EntityUid uid, AntiStealAfkComponent component, PlayerDetachedEvent args)
    {
        component.Leaved = _tick.CurTime;
    }

    public bool TryStrip(EntityUid user, EntityUid target)
    {
        if (TryComp<AntiStealAfkComponent>(target, out var comp))
            if (TryComp<SSDIndicatorComponent>(target, out var ssd) && ssd.IsSSD)
            {
                if (TryComp<MobStateComponent>(target, out var mobstate) && mobstate.CurrentState != MobState.Alive)
                    return true;
                if (HasComp<BypassInteractionChecksComponent>(user))
                    return true;
                var difference = ((comp.Leaved + TimeSpan.FromMinutes(5)) - _tick.CurTime).TotalSeconds;
                if (difference > 0)
                {
                    if (_net.IsServer)
                        _popup.PopupEntity(Loc.GetString("imperial-medieval-afkrob", ("time", Math.Floor(difference).ToString())), target, user);
                    return false;
                }
            }

        return true;
    }
}
