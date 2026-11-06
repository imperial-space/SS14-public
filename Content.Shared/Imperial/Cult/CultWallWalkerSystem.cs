using Content.Shared.Imperial.Cult.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Imperial.Cult;

/// <summary>datum/element/wall_walker для стен культа.</summary>
public sealed class CultWallWalkerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultWallWalkerComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<CultWallWalkerComponent> ent, ref PreventCollideEvent args)
    {
        if (MetaData(args.OtherEntity).EntityPrototype?.ID is { } id && ent.Comp.Walls.Contains(id))
            args.Cancelled = true;
    }
}
