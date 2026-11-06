using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

[Serializable, NetSerializable]
public sealed class ImperialAiAlarmEntry(ImperialAiAlarmKind kind, NetEntity source, string location)
{
    public ImperialAiAlarmKind Kind = kind;
    public NetEntity Source = source;
    public string Location = location;
}
