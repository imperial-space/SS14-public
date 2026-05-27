using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.EnvyKnife;

/// <summary>
/// Нож Зависти: при попадании по гуманоиду копирует его внешность (расу, цвет кожи, маркинги)
/// на владельца ножа.
/// </summary>
[RegisterComponent]
public sealed partial class EnvyKnifeComponent : Component
{
    /// <summary>Звук при копировании внешности.</summary>
    [DataField]
    public SoundSpecifier TransformSound = new SoundPathSpecifier("/Audio/Magic/polymorph.ogg");
}
