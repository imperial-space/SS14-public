using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

public sealed class SharedGulagTeleporterSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GulagTeleporterComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GulagTeleporterComponent, CanDropTargetEvent>(OnCanDrop);
    }

    private void OnInit(Entity<GulagTeleporterComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Occupant = _container.EnsureContainer<ContainerSlot>(ent, GulagTeleporterComponent.ContainerId);
    }

    private void OnCanDrop(Entity<GulagTeleporterComponent> ent, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= ent.Comp.Open && ent.Comp.Occupant.ContainedEntity == null && HasComp<MobStateComponent>(args.Dragged);
    }
}
