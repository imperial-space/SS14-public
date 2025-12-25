using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmithingFurnaceComponent : Component
{
    [DataField]
    public float MeltingTime { get; set; } = 10f;

    [DataField]
    public ItemSlot MeltingSlot { get; set; } = new();

    public TimeSpan? UnlockTime { get; set; }
}
