using Content.Shared.Cargo;

namespace Content.Client.Cargo.UI;

public sealed partial class CargoProductRow
{
    // Imperial Weekly Mode Start
    public WeeklyCargoProductData? WeeklyProduct { get; set; }

    public string ProductId { get; set; } = string.Empty;
    // Imperial Weekly Mode End
}
