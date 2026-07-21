using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Ships.Sea;

/// <summary>
/// Компонент моря
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeaComponent : Component
{
    [DataField("Disabled"), AutoNetworkedField]
    public bool Disabled;

    [DataField, AutoNetworkedField]
    public bool WindEnabledLocal = true;

    [DataField, AutoNetworkedField]
    public string CalmParallax = "OceanMedieval";

    [DataField, AutoNetworkedField]
    public string StormParallax1 = "OceanMedievalStorm1";

    [DataField, AutoNetworkedField]
    public string StormParallax2 = "OceanMedievalStorm2";

    [DataField, AutoNetworkedField]
    public string StormParallax3 = "OceanMedievalStorm3";

    [DataField]
    public int InitStormLevel = 1;
}
