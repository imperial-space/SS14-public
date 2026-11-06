using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Генератор рыболовных порталов (/obj/machinery/fishing_portal_generator).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingPortalComponent : Component
{
    /// <summary>Режим Синдиката (EMAGGED).</summary>
    [DataField]
    public bool Emagged;

    /// <summary>all_destinations: доступны все источники (fish-porter 4000 deluxe).</summary>
    [DataField]
    public bool AllDestinations;

    /// <summary>max_fishing_spots: сколько мест ловли можно привязать мультитулом.</summary>
    [DataField]
    public int MaxSpots = 1;

    /// <summary>long_range_link: привязанное место может быть на другой карте.</summary>
    [DataField]
    public bool LongRange;

    /// <summary>Привязанные места ловли и их источники.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, string> Linked = new();

    /// <summary>Активный источник (null — портал выключен).</summary>
    [ViewVariables]
    public string? Active;

    /// <summary>current_linked_atom: привязанное место, откуда сейчас ловят.</summary>
    [ViewVariables]
    public EntityUid? LinkedAtom;
}
