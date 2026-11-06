using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Значки улова в мини-игре для мастеров рыбалки (GLOB.specific_fish_icons).</summary>
[Prototype]
public sealed partial class ImperialFishingIconsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Прототип (с наследниками) → значок.</summary>
    [DataField]
    public Dictionary<string, ImperialFishIcon> Entities = new();

    /// <summary>Особые награды ("randomSeed", "randomOrgan", ...) → значок.</summary>
    [DataField]
    public Dictionary<string, ImperialFishIcon> Specials = new();
}
