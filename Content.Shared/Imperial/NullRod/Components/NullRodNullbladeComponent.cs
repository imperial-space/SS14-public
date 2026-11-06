using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Нулевой клинок: урон — бросок 1d6 + сила, а по беззащитной цели добавляется скрытая атака 3d6.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodNullbladeComponent : Component
{
    /// <summary>Базовый урон из прототипа, относительно которого считается бросок.</summary>
    [DataField]
    public FixedPoint2 BaseDamage = 12;

    /// <summary>«Сила» руки владельца (в SS13 зависит от руки, по умолчанию 4).</summary>
    [DataField]
    public int Strength = 4;

    /// <summary>Тип урона скрытой атаки.</summary>
    [DataField]
    public string SneakDamageType = "Slash";

    [DataField]
    public SoundSpecifier SneakSound = new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg");
}
