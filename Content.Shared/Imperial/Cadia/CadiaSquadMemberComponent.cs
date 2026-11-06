using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Боец отряда: номер, цвет и название отряда, командир ли он. Свои видят над его головой знак отряда.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaSquadMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Number;

    [DataField, AutoNetworkedField]
    public string Name = string.Empty;

    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField, AutoNetworkedField]
    public bool Leader;

    [DataField, AutoNetworkedField]
    public CadiaFaction Faction;

    /// <summary>Действующий приказ отряда — показывается над головой и в окне.</summary>
    [DataField, AutoNetworkedField]
    public CadiaSquadOrder Order;

    /// <summary>Куда отдан приказ: стрелка цели ведёт сюда.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 OrderPoint;

    [DataField, AutoNetworkedField]
    public CadiaFlightLevel OrderLevel;
}
