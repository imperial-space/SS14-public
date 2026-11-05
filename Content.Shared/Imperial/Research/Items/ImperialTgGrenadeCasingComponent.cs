using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Корпус химгранаты TG: нагревает содержимое при срабатывании, а «улучшенный» выпускает по частям.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialTgGrenadeCasingComponent : Component
{
    /// <summary>ignition_temp: на сколько кельвинов меняется температура смеси.</summary>
    [DataField]
    public float TemperatureDelta;

    /// <summary>unit_spread у adv_release: объём одного выброса. 0 — всё сразу.</summary>
    [DataField]
    public float ReleaseUnits;

    [DataField]
    public float ReleaseDelay = 5f;

    [ViewVariables]
    public Dictionary<EntityUid, Content.Shared.Chemistry.Components.Solution> Stash = new();
}
