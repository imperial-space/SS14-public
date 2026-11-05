using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

[Serializable, NetSerializable]
public enum ImperialMalfModuleCategory : byte
{
    Destructive,
    Utility,
    Upgrade,
}
