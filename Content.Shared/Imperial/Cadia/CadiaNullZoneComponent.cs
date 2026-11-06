using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Гасит варп рядом: пилон делает силы Хаоса дороже и опаснее, а неприкасаемый полностью глушит чужие силы.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaNullZoneComponent : Component
{
    [DataField]
    public float Radius = 8f;

    /// <summary>Полное подавление (неприкасаемый); иначе только штраф псайкерам Хаоса (пилон).</summary>
    [DataField]
    public bool Total;
}
