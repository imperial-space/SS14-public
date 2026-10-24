using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DragDrop;
using Content.Shared.Imperial.ImperialVehicle;
using Content.Shared.Imperial.ImperialVehicle.Components;
using Content.Shared.Interaction;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>
/// Машина клоуна не пристёгивает водителя кликом: посадка — перетаскиванием себя на машину,
/// клик снаружи открывает багажник. Перехват общий, чтобы клиент не предсказывал пристёгивание.
/// </summary>
public abstract class SharedImperialClownCarSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialClownCarComponent, CanDropTargetEvent>(OnCanDropTarget, before: [typeof(SharedBuckleSystem)]);
        SubscribeLocalEvent<ImperialClownCarComponent, DragDropTargetEvent>(OnDragDropTarget, before: [typeof(SharedBuckleSystem)]);
        SubscribeLocalEvent<ImperialClownCarComponent, InteractHandEvent>(OnInteractHand, before: [typeof(SharedBuckleSystem)]);
        SubscribeLocalEvent<ImperialClownCarComponent, StrapAttemptEvent>(OnStrapAttempt, after: [typeof(SharedImperialVehicleSystem)]);
        SubscribeLocalEvent<ImperialVehiclePilotComponent, UnbuckleAttemptEvent>(OnPilotUnbuckleAttempt);
    }

    private void OnCanDropTarget(Entity<ImperialClownCarComponent> ent, ref CanDropTargetEvent args)
    {
        args.CanDrop = HasComp<BuckleComponent>(args.Dragged);
        args.Handled = true;
    }

    private void OnDragDropTarget(Entity<ImperialClownCarComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Dragged == args.User)
            TryStartEnter(ent, args.User);
        else
            TryStartForcedEnter(ent, args.User, args.Dragged);
    }

    private void OnInteractHand(EntityUid uid, ImperialClownCarComponent comp, InteractHandEvent args)
    {
        var ent = new Entity<ImperialClownCarComponent>(uid, comp);
        if (args.Handled)
            return;

        // Водитель кликом по машине вылезает (climb_out), остальные открывают багажник.
        if (TryComp<ImperialVehicleComponent>(ent, out var vehicle) && vehicle.Rider == args.User)
            return;

        args.Handled = true;
        TryOpenTrunk(ent, args.User);
    }

    private void OnStrapAttempt(Entity<ImperialClownCarComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Buckle.Owner == ent.Comp.AllowedDriver)
            return;

        args.Cancelled = true;
    }

    private void OnPilotUnbuckleAttempt(Entity<ImperialVehiclePilotComponent> ent, ref UnbuckleAttemptEvent args)
    {
        // Водителя машины клоуна нельзя вытащить снаружи.
        if (args.User is { } user && user != ent.Owner && HasComp<ImperialClownCarComponent>(args.Strap))
            args.Cancelled = true;
    }

    protected virtual void TryStartEnter(Entity<ImperialClownCarComponent> ent, EntityUid user)
    {
    }

    protected virtual void TryStartForcedEnter(Entity<ImperialClownCarComponent> ent, EntityUid user, EntityUid target)
    {
    }

    protected virtual void TryOpenTrunk(Entity<ImperialClownCarComponent> ent, EntityUid user)
    {
    }
}
