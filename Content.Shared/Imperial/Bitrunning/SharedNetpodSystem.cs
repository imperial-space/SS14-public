using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>Затаскивание себя в открытый нетпод (mouse_drop_receive).</summary>
public sealed class SharedNetpodSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NetpodComponent, Content.Shared.DragDrop.CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(Entity<NetpodComponent> ent, ref Content.Shared.DragDrop.CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = ent.Comp.Open && args.Dragged == args.User && HasComp<Content.Shared.Mobs.Components.MobStateComponent>(args.Dragged);
        args.Handled = true;
    }
}
