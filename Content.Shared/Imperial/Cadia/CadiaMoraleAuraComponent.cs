using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Источник духа или страха: комиссар, священник, штандарт, демон. Действует на мораль вокруг себя.</summary>
[RegisterComponent]
public sealed partial class CadiaMoraleAuraComponent : Component
{
    [DataField]
    public float Radius = 7f;

    /// <summary>Своим, единиц в секунду.</summary>
    [DataField]
    public float FriendRate;

    /// <summary>Врагам, единиц в секунду.</summary>
    [DataField]
    public float EnemyRate;

    /// <summary>Сторона постройки. Без неё аура работает в руках бойца, и сторона берётся у него.</summary>
    [DataField]
    public CadiaFaction? Faction;
}
