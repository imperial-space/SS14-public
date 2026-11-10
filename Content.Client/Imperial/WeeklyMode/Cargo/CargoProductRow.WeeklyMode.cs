using Content.Client.Cargo;
using Content.Client.Cargo.UI;
using Content.Shared.Cargo;

namespace Content.Client.Imperial.WeeklyMode.Cargo;

public sealed partial class WeeklyCargoProductRow
{
    public WeeklyCargoProductData? WeeklyProduct { get; set; }

    public string ProductId { get; set; } = string.Empty;
}
