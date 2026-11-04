using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

[DataDefinition]
public sealed partial class ImperialProcessEntry
{
    [DataField(required: true)]
    public ImperialKitchenTool Tool;

    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public int Amount = 3;

    /// <summary>time_to_process в секундах.</summary>
    [DataField]
    public float Time = 2f;

    [DataField]
    public bool TableRequired;
}
