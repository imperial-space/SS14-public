using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>N-spect сканер: проверка цели на контрабанду с распечаткой отчёта.</summary>
[RegisterComponent]
public sealed partial class ImperialInspectorComponent : Component
{
    [DataField]
    public float Range = 5f;

    [DataField]
    public EntProtoId Report = "Paper";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);
}
