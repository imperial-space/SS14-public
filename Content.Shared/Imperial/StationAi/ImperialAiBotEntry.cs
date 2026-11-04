using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

[Serializable, NetSerializable]
public sealed class ImperialAiBotEntry(NetEntity entity, string name, string location, bool summoned)
{
    public NetEntity Entity = entity;
    public string Name = name;
    public string Location = location;
    public bool Summoned = summoned;
}
