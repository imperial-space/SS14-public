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
