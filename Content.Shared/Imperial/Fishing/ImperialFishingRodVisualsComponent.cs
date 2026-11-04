using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Состояние катушки удочки (reel_overlay и т. п.).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFishingRodVisualsComponent : Component
{
    [DataField]
    public string ReelState = "reel_overlay";

    [DataField]
    public Color DefaultLineColor = Color.Gray;

    /// <summary>Состояние разложенной удочки (у телескопических).</summary>
    [DataField]
    public string? ExtendedState;

    /// <summary>Состояние сложенной удочки (у телескопических).</summary>
    [DataField]
    public string? CollapsedState;
}
