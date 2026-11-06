using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

public enum ImperialVendingProductType : byte
{
    /// <summary>products.</summary>
    Regular,

    /// <summary>contraband: видна после взлома проводов.</summary>
    Contraband,

    /// <summary>premium: всегда видна, стоит дороже.</summary>
    Premium,
}
