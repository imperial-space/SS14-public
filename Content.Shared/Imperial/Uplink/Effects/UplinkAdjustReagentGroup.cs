using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Uplink.Effects;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class UplinkAdjustReagentGroup : EntityEffectBase<UplinkAdjustReagentGroup>
{
    [DataField(required: true)]
    public string Group = string.Empty;

    /// <summary>Отрицательное — сколько вывести каждого реагента группы.</summary>
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField]
    public string? Exclude;
}
