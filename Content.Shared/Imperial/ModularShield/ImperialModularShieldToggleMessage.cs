using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public sealed class ImperialModularShieldToggleMessage(int id = -1) : BoundUserInterfaceMessage
{
    /// <summary>Номер генератора в консоли; −1 — сам генератор.</summary>
    public readonly int Id = id;
}
