using Content.Shared.Coordinates;
using Content.Shared.Imperial.SpawnOnAction.Events;
using Robust.Server.GameObjects;
using Content.Shared.Imperial.SpawnOnAction.Components;
using Content.Server.Actions;
using Content.Shared.Imperial.Medieval.Trading;

namespace Content.Server.Imperial.SpawnOnAction.Systems;

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
            _transform.SetWorldPosition(
            comp.Object.Value,
            ev.Target.Position
        );
            return;
        }
        if (comp.Object == null || comp.Prototype == null) return;

        _transform.SetCoordinates(
            comp.Object.Value,
            ev.Target
        );

        // не лучшее место для этого, но компонент в любом случае используется
        // только для торговой дыры
        if (TryComp<TradingComponent>(comp.Object, out var trading))
            trading.AccountOwner = ev.Performer;

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
