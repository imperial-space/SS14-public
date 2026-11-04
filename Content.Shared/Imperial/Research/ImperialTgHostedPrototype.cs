using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

/// <summary>Технологии SS14, чьи рецепты перенесены в узлы TG (генерируется вместе с деревом).</summary>
[Prototype]
public sealed partial class ImperialTgHostedPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Технология SS14 → узел TG, с изучением которого она считается изученной.</summary>
    [DataField]
    public Dictionary<string, string> Technologies = new();
}
