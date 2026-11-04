using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Т-лучевые очки: сканер работает только надетым на глаза.</summary>
[RegisterComponent]
public sealed partial class ImperialWornTrayScannerComponent : Component
{
    [DataField]
    public float Range = 4f;
}
