namespace Content.Client.Imperial.Heretic;

/// <summary>
/// Клиентская метка: на эту сущность уже наложены слои галлюцинации Плачущих.
/// </summary>
[RegisterComponent]
public sealed partial class HereticWeepingHallucinationVisualsComponent : Component;

public enum HereticWeepingHallucinationLayers : byte
{
    Armor,
    Hood,
    Blade,
}
