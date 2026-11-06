using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Компьютер или дисплей, показывающий состояние пациента рядом (operating, vitals display).</summary>
[RegisterComponent]
public sealed partial class ImperialVitalsDisplayComponent : Component
{
    [DataField]
    public float Range = 2f;

    /// <summary>Подробный отчёт (продвинутый дисплей, операционный компьютер).</summary>
    [DataField]
    public bool Advanced;
}
