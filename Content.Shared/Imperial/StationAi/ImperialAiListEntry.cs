using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

[Serializable, NetSerializable]
public sealed class ImperialAiListEntry(NetEntity entity, string name, string detail)
{
    public NetEntity Entity = entity;
    public string Name = name;
    public string Detail = detail;
}
