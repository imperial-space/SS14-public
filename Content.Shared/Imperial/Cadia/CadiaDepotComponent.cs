using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Депо снабжения: копит очки и выдаёт за них ящики, которые потом тащат к позициям.</summary>
[RegisterComponent]
public sealed partial class CadiaDepotComponent : Component
{
    [DataField]
    public float Points = 40f;

    [DataField]
    public float MaxPoints = 120f;

    /// <summary>Очков в минуту.</summary>
    [DataField]
    public float PerMinute = 6f;

    [DataField]
    public List<CadiaDepotEntry> Entries = new();

    /// <summary>Чьи секторы пополняют это депо.</summary>
    [DataField]
    public CadiaFaction Faction = CadiaFaction.Imperium;
}
