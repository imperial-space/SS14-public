using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Дальнобойный газоанализатор (analyzer/ranged): анализ тайла на расстоянии.</summary>
[RegisterComponent]
public sealed partial class ImperialRangedGasAnalyzerComponent : Component
{
    [DataField]
    public float Range = 15f;
}
