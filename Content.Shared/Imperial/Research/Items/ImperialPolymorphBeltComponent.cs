using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Пояс инверсии полиморфного поля: сканирует существо и превращает в него носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialPolymorphBeltComponent : Component
{
    [ViewVariables]
    public EntProtoId? Stored;

    [DataField]
    public float Duration = 60f;
}
