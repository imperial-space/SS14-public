using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaHoloTableState : BoundUserInterfaceState
{
    public CadiaFaction Faction;
    public bool Commander;
    public bool Planner;
    public bool ReadOnly;
    public Vector2 TablePosition;
    public List<CadiaFlightLevel> Levels = new();
    public List<CadiaHoloElement> Elements = new();
    public List<CadiaHoloLive> Live = new();
    public List<CadiaHoloSquad> Squads = new();
}
