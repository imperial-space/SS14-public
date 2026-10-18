using Content.Shared.DragDrop;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>Машина принимает перетаскиваемых обезьян (переработчик) или слаймов (процессор).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenobioDropTargetComponent : Component
{
    [DataField]
    public bool Monkeys;

    [DataField]
    public bool Slimes;
}

public sealed class XenobioDropTargetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobioDropTargetComponent, CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(Entity<XenobioDropTargetComponent> ent, ref CanDropTargetEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Dragged))
            return;

        var ok = ent.Comp.Slimes && HasComp<SlimeComponent>(args.Dragged)
                 || ent.Comp.Monkeys && IsMonkey(args.Dragged);
        if (!ok)
            return;

        args.CanDrop = true;
        args.Handled = true;
    }

    /// <summary>ismonkey().</summary>
    public bool IsMonkey(EntityUid uid)
    {
        return MetaData(uid).EntityPrototype?.ID.Contains("Monkey") == true;
    }
}
