using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Vents;

/// <summary>
/// Гейзер лаваленда (structure/geyser из SS13): копит реагент до 500 единиц, восполняя по 2 за 2 с.
/// Обнаружение шахтёрским сканером даёт очки и настоящее имя.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandGeyserComponent : Component
{
    /// <summary>reagent_id; null — случайный (geyser/random).</summary>
    [DataField] public ProtoId<ReagentPrototype>? Reagent;

    [DataField] public float Potency = 2f;
    [DataField] public float MaxVolume = 500f;
    [DataField] public float Interval = 2f;
    [DataField] public int PointValue = 100;
    [DataField] public LocId? TrueName;
    [DataField] public LocId? DiscoveryMessage;
    [DataField] public string Solution = "geyser";

    [DataField] public SoundSpecifier DiscoverSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/twobeep_high.ogg");
    [DataField] public SoundSpecifier ScanSound = new SoundCollectionSpecifier("ImperialIndustrialScan");

    [ViewVariables] public bool Discovered;
    [ViewVariables] public TimeSpan NextRefill;
}
