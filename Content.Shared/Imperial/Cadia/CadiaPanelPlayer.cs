using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public struct CadiaPanelPlayer
{
    public Guid User;
    public string Name;
    public string Character;
    public CadiaFaction Faction;
    public string Role;
    public bool Alive;
    public bool Banned;
    public int TeamKills;
    public int TeamDamage;
}
