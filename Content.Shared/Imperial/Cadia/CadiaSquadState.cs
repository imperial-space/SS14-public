using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaSquadState : BoundUserInterfaceState
{
    public CadiaFaction Faction;
    public List<CadiaSquadInfo> Squads = new();
    public List<CadiaSquadFighter> Fighters = new();
    public TimeSpan VoxCooldownUntil;
}
