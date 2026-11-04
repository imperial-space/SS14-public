using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

[Serializable, NetSerializable]
public sealed class ImperialAiBorgEntry(NetEntity entity, string name, string location, int health, int charge,
    bool shell, bool deployed, bool linked)
{
    public NetEntity Entity = entity;
    public string Name = name;
    public string Location = location;
    public int Health = health;
    public int Charge = charge;
    public bool Shell = shell;
    public bool Deployed = deployed;
    public bool Linked = linked;
}
