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

/// <summary>Мультитул запомнил генератор порталов (buffer).</summary>
[RegisterComponent]
public sealed partial class ImperialFishingPortalBufferComponent : Component
{
    [ViewVariables]
    public EntityUid Portal;
}

[Serializable, NetSerializable]
public enum ImperialFishingPortalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialFishingPortalOption
{
    public string Key = string.Empty;
    public string Name = string.Empty;
    public string RadialState = "default";

    /// <summary>Привязанное место ловли (звёздочка в углу).</summary>
    public bool Linked;
}

[Serializable, NetSerializable]
public sealed class ImperialFishingPortalBoundUserInterfaceState(List<ImperialFishingPortalOption> options, bool unlink) : BoundUserInterfaceState
{
    public readonly List<ImperialFishingPortalOption> Options = options;

    /// <summary>Меню отвязки мест ловли.</summary>
    public readonly bool Unlink = unlink;
}

[Serializable, NetSerializable]
public sealed class ImperialFishingPortalSelectMessage(string key) : BoundUserInterfaceMessage
{
    public readonly string Key = key;
}

[Serializable, NetSerializable]
public enum ImperialFishingPortalVisuals : byte
{
    /// <summary>Состояние наложения активного портала (пустая строка — выключен).</summary>
    Overlay,
}
