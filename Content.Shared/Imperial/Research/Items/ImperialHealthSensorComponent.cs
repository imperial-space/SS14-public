using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Датчик здоровья (assembly/health): срабатывает, когда носитель впадает в крит или умирает.</summary>
[RegisterComponent]
public sealed partial class ImperialHealthSensorComponent : Component
{
    /// <summary>true — ждать смерти, false — критического состояния.</summary>
    [DataField]
    public bool WaitForDeath;

    [DataField]
    public bool Scanning = true;

    [ViewVariables]
    public bool Triggered;
}
