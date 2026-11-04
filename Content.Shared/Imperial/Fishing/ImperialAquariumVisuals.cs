using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public enum ImperialAquariumVisuals : byte
{
    PanelOpen,
    Air,
    Broken,
}
