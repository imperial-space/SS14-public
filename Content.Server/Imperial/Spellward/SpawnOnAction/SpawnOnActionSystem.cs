using Content.Shared.Coordinates;
using Content.Shared.Imperial.SpawnOnAction.Events;
using Robust.Server.GameObjects;
using Content.Shared.Imperial.SpawnOnAction;
using Content.Server.Actions;

namespace Content.Server.Imperial.Spellward;

public sealed partial class SpawnOnActionSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnActionComponent, SpawnOnActionEvent>(OnUse);
        SubscribeLocalEvent<SpawnOnActionComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SpawnOnActionComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnUse(EntityUid uid, SpawnOnActionComponent comp, SpawnOnActionEvent ev)
    {
        if (ev.Handled) return;
        if (comp.IsFirst)
        {
            comp.Object = Spawn(comp.Prototype, uid.ToCoordinates());
            comp.IsFirst = false;
            return;
        }
        if (comp.Object == null || comp.Prototype == null) return;
        var mapPosition = _transform.GetWorldPosition(uid);

        _transform.SetWorldPosition(
            comp.Object.Value,
            mapPosition
        );
        ev.Handled = true;
    }
    private void OnComponentInit(EntityUid uid, SpawnOnActionComponent comp, ComponentInit ev)
    {
        _actions.AddAction(uid, ref comp.Action, comp.ActionId);
    }
    private void OnComponentShutdown(EntityUid uid, SpawnOnActionComponent comp, ComponentShutdown ev)
    {
        _actions.RemoveAction(uid, comp.Action);
        if (comp.Object != null)
            QueueDel(comp.Object.Value);
    }
}
