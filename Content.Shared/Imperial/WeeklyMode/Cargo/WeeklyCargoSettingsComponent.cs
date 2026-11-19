namespace Content.Shared.Cargo;

/// <summary>Delivery settings on a campaign map. Absent components use these defaults.</summary>
[RegisterComponent]
public sealed partial class WeeklyCargoSettingsComponent : Component
{
    [DataField]
    public string CratePrototype = "CrateGenericSteel";

    [DataField]
    public string CrateContainerId = "entity_storage";

    [DataField]
    public int BoxedCapacity = 30;
}
