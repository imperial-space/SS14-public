using Content.Shared.Imperial.Medieval.WarningOnAttach;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.WarningOnAttach;

public sealed class WarningOnAttachSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarningOnAttachComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(EntityUid uid, WarningOnAttachComponent component, PlayerAttachedEvent args)
    {
        if (component.Players.Contains(args.Player.Name))
            return;

        component.Players.Add(args.Player.Name);
        var ev = new ShowWarningOnAttachMessage(component.Message);
        RaiseNetworkEvent(ev, args.Player);
    }
}
