using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public sealed class ImperialModularShieldRenameMessage(int id, string name) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly string Name = name;
}
