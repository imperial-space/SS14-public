using Content.Shared.Sprite;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>
/// Меняет размер носителя, пока мутация активна (карликовость, гигантизм).
/// </summary>
[RegisterComponent]
public sealed partial class GeneticScaleComponent : Component
{
    [DataField]
    public float Scale = 1f;

    /// <summary>Дополнительное растяжение по вертикали (акромегалия: выше, но не шире).</summary>
    [DataField]
    public float Height = 1f;
}
