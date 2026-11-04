using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaHoloLiveKind : byte
{
    Friendly,
    Commander,
    Pylon,
    PylonFallen,
    Citadel,
    Shuttle,
    AntiAir,
    Strike,
    EnemyContact,
    SectorImperium,
    SectorChaos,
    SectorNeutral,
    Beacon,
}
