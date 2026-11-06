using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaLandmarkKind : byte
{
    Pylon,
    Citadel,
    Beacon,
}
