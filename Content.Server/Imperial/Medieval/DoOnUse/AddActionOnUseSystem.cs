using Content.Shared.Actions;
using Content.Shared.Imperial.Medieval.DoOnUse.Action;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Medieval.DoOnUse.Action;

public sealed partial class AddActionOnUseSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddActionOnUseComponent, UseInHandEvent>(OnDo);
    }
    private void OnDo(EntityUid uid, AddActionOnUseComponent component, UseInHandEvent ev)
    {
        _action.AddAction(ev.User, component.ActionId);
        QueueDel(uid);
    }
}
