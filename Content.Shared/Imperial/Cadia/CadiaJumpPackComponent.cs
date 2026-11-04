using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Прыжковый ранец: удар с воздуха и подъём на соседний этаж.</summary>
[RegisterComponent]
public sealed partial class CadiaJumpPackComponent : Component
{
    [DataField]
    public float Range = 8f;

    [DataField]
    public float ImpactRadius = 1.8f;

    [DataField]
    public float ImpactDamage = 30f;

    /// <summary>Когда приземлиться (идёт прыжок).</summary>
    [ViewVariables]
    public TimeSpan? LandAt;

    [ViewVariables]
    public EntityUid? Jumper;

    /// <summary>Кому ранец дал полёт (как у Хелдрейка: не падает в проёмы, сам ходит между этажами).</summary>
    [ViewVariables]
    public EntityUid? Flyer;
}
