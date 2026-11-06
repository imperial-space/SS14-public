using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fugitives;

/// <summary>Можно перетащить человека на машину захвата.</summary>
public sealed class ImperialFugitiveCaptureSharedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFugitiveCaptureComponent, Content.Shared.DragDrop.CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(Entity<ImperialFugitiveCaptureComponent> ent, ref Content.Shared.DragDrop.CanDropTargetEvent args)
    {
        if (!HasComp<Content.Shared.Humanoid.HumanoidProfileComponent>(args.Dragged))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }
}
