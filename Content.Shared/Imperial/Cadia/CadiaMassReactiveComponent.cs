using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Масс-реактивный снаряд: сам по себе бьёт слабо, главный урон — подрыв в цели. Сила падает к краю радиуса,
/// стрелка не задевает, сквозь стену не проходит. Без движка взрывов: сотня болтеров не положит сервер.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaMassReactiveComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Blast = new();

    [DataField]
    public float Radius = 1f;

    /// <summary>Какая доля урона остаётся на краю радиуса.</summary>
    [DataField]
    public float EdgeFactor = 0.5f;

    [DataField]
    public EntProtoId Effect = "CadiaBoltBlast";

    [DataField]
    public SoundSpecifier? Sound;
}
