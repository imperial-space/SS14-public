using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaSquadInfo
{
    public int Number;
    public string Name = string.Empty;
    public Color Color;
    public NetEntity? Leader;
    public CadiaSquadOrder Order;
    public List<NetEntity> Members = new();
}
