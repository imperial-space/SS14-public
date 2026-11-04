using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaHoloMarkerKind : byte
{
    Ship,
    Friendly,
    Pylon,
    PylonFallen,
    Citadel,
    Strike,
    Target,
    Shuttle,

    /// <summary>Цель, отмеченная командиром на гололитическом столе.</summary>
    Designated,
}
