using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Медицинский диагностический прибор TG: результат выводится всплывающим сообщением или бумагой.</summary>
[RegisterComponent]
public sealed partial class ImperialMedScannerComponent : Component
{
    [DataField(required: true)]
    public ImperialMedScanMode Mode;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1f);
}
