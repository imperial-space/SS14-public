using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Медицинский инструмент TG, действующий на пациента после задержки.</summary>
[RegisterComponent]
public sealed partial class ImperialMedToolComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5f);

    /// <summary>blood_filter: сколько единиц чужих реагентов убрать из крови.</summary>
    [DataField]
    public FixedPoint2 FilterAmount;

    /// <summary>bonesetter: сколько тупого урона лечит одно вправление.</summary>
    [DataField]
    public float HealBlunt;
}
