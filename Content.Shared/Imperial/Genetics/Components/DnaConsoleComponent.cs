using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>
/// Консоль генетики (dna_console из SS13): работает с пациентом в ДНК-сканере рядом.
/// </summary>
[RegisterComponent]
public sealed partial class DnaConsoleComponent : Component
{
    /// <summary>Радиус поиска сканера.</summary>
    [DataField]
    public float ScannerRange = 1.6f;

    [DataField]
    public EntProtoId InjectorProto = "ImperialDnaInjector";

    /// <summary>Слот дискеты.</summary>
    [DataField]
    public string DiskSlot = "disk";

    [ViewVariables]
    public List<StoredMutation> Storage = new();

    [ViewVariables]
    public Dictionary<ChromosomeKind, int> Chromosomes = new();

    /// <summary>Продвинутые инжекторы (injector_selection): до трёх именованных наборов мутаций.</summary>
    [ViewVariables]
    public List<AdvancedInjector> AdvancedInjectors = new();

    [DataField]
    public int MaxAdvInjectors = 3;

    [DataField]
    public int MaxInjectorMutations = 10;

    [DataField]
    public int MaxInjectorInstability = 50;

    /// <summary>Буферы генетического облика (genetic_makeup_buffer).</summary>
    [ViewVariables]
    public GeneticMakeupData?[] Makeups = new GeneticMakeupData?[3];

    [ViewVariables]
    public int CrisprCharges;

    [ViewVariables]
    public int PulseStrength = 1;

    [ViewVariables]
    public int PulseDuration = 2;

    /// <summary>Идущий импульс по ферментам: 0 — нет.</summary>
    [ViewVariables]
    public int PulseIndex;

    [ViewVariables]
    public bool PulseFeatures;

    [ViewVariables]
    public TimeSpan PulseAt;

    /// <summary>Отложенный перенос облика: применится, когда в сканер ляжет пациент.</summary>
    [ViewVariables]
    public (int Index, DnaMakeupType Type)? DelayedAction;

    [DataField]
    public TimeSpan JokerCooldown = TimeSpan.FromMinutes(20);

    [DataField]
    public TimeSpan ScrambleCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan MakeupCooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextInjector;

    [ViewVariables]
    public TimeSpan NextJoker;

    [ViewVariables]
    public TimeSpan NextScramble;

    [ViewVariables]
    public TimeSpan NextMakeup;
}
