

using Content.Shared.Actions;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Imperial.HardsuitInjection.Components;

namespace Content.Shared.Imperial.HardsuitInjection.EntitySystems;

public sealed partial class InjectSystem
{
    private void InitializeActionEvents()
    {
        SubscribeLocalEvent<InjectComponent, ToggleECEvent>(OnToggleEC);
        SubscribeLocalEvent<InjectComponent, InjectionEvent>(OnInjection);
        SubscribeLocalEvent<InjectComponent, GetItemActionsEvent>(OnGetItemAction);
    }

    private void OnGetItemAction(EntityUid uid, InjectComponent component, GetItemActionsEvent args)
    {
        if (!_timing.IsFirstTimePredicted) return;
        if ((args.SlotFlags | component.RequiredFlags) != component.RequiredFlags) return;

        args.AddAction(ref component.ToggleInjectionActionEntity, component.ToggleInjectionAction);
        args.AddAction(ref component.InjectionActionEntity, component.InjectionAction);
    }

    private void OnToggleEC(EntityUid uid, InjectComponent component, ToggleECEvent args)
    {
        if (args.Handled) return;
        if (_netManager.IsClient) return;

        args.Handled = true;
        component.ToggleInjectionActionEntity = args.Action;

        ToggleEC(uid, args.Performer);
    }

    private void OnInjection(EntityUid uid, InjectComponent component, InjectionEvent args)
    {
        if (args.Handled) return;
        if (_netManager.IsClient) return;

        args.Handled = true;
        component.InjectionActionEntity = args.Action;

        Inject(uid, uid);
    }
}
