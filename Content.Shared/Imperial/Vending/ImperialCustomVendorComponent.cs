using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>vending/custom TG: продаёт вложенные предметы по ценам владельца.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialCustomVendorComponent : Component
{
    [DataField]
    public string Container = "custom_vendor";

    [DataField]
    public int MaxItems = 20;

    /// <summary>Счёт владельца в банке Imperial.</summary>
    [DataField, AutoNetworkedField]
    public int? Account;

    [DataField, AutoNetworkedField]
    public string Owner = string.Empty;

    /// <summary>Цены вложенных предметов.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<NetEntity, int> Prices = new();

    [DataField]
    public float DeconstructedTotalIntensity = 30f;

    [DataField]
    public float DeconstructedSlope = 3f;

    [DataField]
    public float DeconstructedMaxTileIntensity = 10f;
}
