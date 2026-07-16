using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.BloodOfGodFountain;

[RegisterComponent, NetworkedComponent]
public sealed partial class BloodOfGodFountainComponent : Component;

[Serializable, NetSerializable]
public enum BloodFountainVisuals : byte
{
    State,
}
