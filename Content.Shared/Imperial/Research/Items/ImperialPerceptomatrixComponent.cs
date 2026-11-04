using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Шлем перцептоматрицы: защита разума и навязывание галлюцинаций.</summary>
[RegisterComponent]
public sealed partial class ImperialPerceptomatrixComponent : Component
{
    [DataField]
    public EntProtoId Action = "ImperialActionPerceptomatrix";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [DataField]
    public float Duration = 30f;
}
