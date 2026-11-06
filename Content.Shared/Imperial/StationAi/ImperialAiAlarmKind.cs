using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

[Serializable, NetSerializable]
public enum ImperialAiAlarmKind : byte
{
    Fire,
    Atmosphere,
    Power,
    Camera,
}
