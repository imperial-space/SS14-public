using System.Linq;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Imperial.Medieval.Igniter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Light.Components;
using Content.Shared.Temperature;
using Content.Shared.Verbs;

namespace Content.Server.Imperial.Medieval.FlammableExpendableLight;


public sealed partial class FlammableExpendableLightSystem : EntitySystem
{
    [Dependency] private readonly ExpendableLightSystem _expendableLightSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlammableExpendableLightComponent, IgniteEvent>(OnIgnite);
        SubscribeLocalEvent<FlammableExpendableLightComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FlammableExpendableLightComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);

        SubscribeLocalEvent<FlammableExpendableLightComponent, UseInHandEvent>(OnActivate, before: [typeof(ExpendableLightSystem)]);
        SubscribeLocalEvent<FlammableExpendableLightComponent, GetVerbsEvent<ActivationVerb>>(RemoveIgniteVerb, after: [typeof(ExpendableLightSystem)]);
    }

    private void OnIgnite(EntityUid uid, FlammableExpendableLightComponent component, IgniteEvent args)
    {
        if (!TryComp<ExpendableLightComponent>(uid, out var expendableLightComponent)) return;

        _expendableLightSystem.TryActivate((uid, expendableLightComponent));
    }

    private void OnAfterInteract(EntityUid uid, FlammableExpendableLightComponent component, AfterInteractEvent args)
    {
        if (args.Target == null) return;
        if (!TryComp<ExpendableLightComponent>(uid, out var expendableLightComponent)) return;

        var isHotEvent = new IsHotEvent();
        RaiseLocalEvent(args.Target.Value, isHotEvent, false);

        if (!isHotEvent.IsHot) return;

        _expendableLightSystem.TryActivate((uid, expendableLightComponent));
    }

    private void OnAfterInteractUsing(EntityUid uid, FlammableExpendableLightComponent component, AfterInteractUsingEvent args)
    {
        if (!TryComp<ExpendableLightComponent>(uid, out var expendableLightComponent)) return;

        var isHotEvent = new IsHotEvent();
        RaiseLocalEvent(args.Used, isHotEvent, false);

        if (!isHotEvent.IsHot) return;

        _expendableLightSystem.TryActivate((uid, expendableLightComponent));
    }

    private void OnActivate(EntityUid uid, FlammableExpendableLightComponent component, UseInHandEvent args)
    {
        args.Handled = true;
    }

    // This remove last verb. We subs to the event after ExpendableLightSystem
    private void RemoveIgniteVerb(EntityUid uid, FlammableExpendableLightComponent component, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!TryComp<ExpendableLightComponent>(uid, out var expendableLightComponent)) return;

        if (!args.CanAccess || !args.CanInteract) return;
        if (expendableLightComponent.CurrentState != ExpendableLightState.BrandNew) return;

        args.Verbs.Remove(args.Verbs.Last());
    }
}
