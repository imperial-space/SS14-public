using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

[Serializable, NetSerializable]
public sealed partial class ImperialCraftDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Recipe = string.Empty;

    private ImperialCraftDoAfterEvent()
    {
    }

    public ImperialCraftDoAfterEvent(string recipe)
    {
        Recipe = recipe;
    }

    public override DoAfterEvent Clone() => this;
}
