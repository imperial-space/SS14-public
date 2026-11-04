using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>Инструмент кухни (TOOL_KNIFE, TOOL_ROLLINGPIN…).</summary>
[RegisterComponent]
public sealed partial class ImperialKitchenToolComponent : Component
{
    [DataField(required: true)]
    public List<ImperialKitchenTool> Tools = new();

    /// <summary>toolspeed: множитель времени обработки.</summary>
    [DataField]
    public float Speed = 1f;
}
