using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Варп-штормы ═══════════════════════

[Serializable, NetSerializable]
public enum CadiaWarpStorm : byte
{
    None,
    BloodRain,
    Whispers,
    DaemonSurge,
}
