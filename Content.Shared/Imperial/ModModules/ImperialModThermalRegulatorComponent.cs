using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>thermal_regulator: держит температуру тела носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModThermalRegulatorComponent : Component
{
    /// <summary>temperature_setting (BODYTEMP_NORMAL).</summary>
    [DataField]
    public float Target = 310.15f;

    /// <summary>Доля разницы температур, выравниваемая за секунду.</summary>
    [DataField]
    public float Rate = 0.08f;
}
