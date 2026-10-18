using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Подставка для трофейной рыбы (/obj/structure/fish_mount).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishMountComponent : Component
{
    public const string ContainerId = "imperial-fish-mount";

    /// <summary>persistence_id: трофей сохраняется между раундами.</summary>
    [DataField]
    public string? PersistenceId;

    /// <summary>persistence_loaded_fish: трофей из прошлых раундов может рассыпаться при снятии.</summary>
    [ViewVariables]
    public bool PersistenceLoaded;

    /// <summary>fillet_type рыбы до установки (на подставке рыбу не разделать).</summary>
    [ViewVariables]
    public EntProtoId? StoredFillet;

    [ViewVariables]
    public bool Loaded;
}

[Serializable, NetSerializable]
public enum ImperialFishMountVisuals : byte
{
    /// <summary>Прототип рыбы на подставке (пустая строка — нет рыбы).</summary>
    Fish,
}
