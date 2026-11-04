using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Vents;

/// <summary>
/// Валун (item/boulder): разбивается киркой за durability шагов в руду; на лаве становится плотом.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBoulderComponent : Component
{
    [DataField] public OreVentSize Size = OreVentSize.Small;
    [DataField] public int Durability = 5;

    /// <summary>Руда и число единиц материала (100 = лист).</summary>
    [DataField] public Dictionary<EntProtoId, int> Materials = new();

    /// <summary>Артефакт внутри (boulder/artifact).</summary>
    [DataField] public EntProtoId? Artifact;

    [DataField] public float PlatformLifespan = 20f;
    [DataField] public string Style = "boulder";
    [DataField] public EntProtoId Platform = "ImperialBoulderPlatform";

    [DataField] public SoundSpecifier HitSound = new SoundCollectionSpecifier("ImperialPickaxe");
    [DataField] public SoundSpecifier BreakSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/rock_break.ogg");
}
