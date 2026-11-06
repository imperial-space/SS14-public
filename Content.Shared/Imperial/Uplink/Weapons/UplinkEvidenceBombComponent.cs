using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Uplink.Weapons;

/// <summary>
/// Кейс оружия агента (toolbox/guncase/traitor): Alt-ПКМ взводит бомбу уничтожения улик,
/// через 5 секунд кейс взрывается вместе с содержимым.
/// </summary>
[RegisterComponent]
public sealed partial class UplinkEvidenceBombComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan? DetonateAt;

    [DataField]
    public float UpdateEvidenceBombsTotalIntensity = 40f;

    [DataField]
    public float UpdateEvidenceBombsSlope = 6f;

    [DataField]
    public float UpdateEvidenceBombsMaxTileIntensity = 6f;
}
