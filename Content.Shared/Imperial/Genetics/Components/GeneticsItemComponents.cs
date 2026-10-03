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

[DataDefinition, Serializable, NetSerializable]
public sealed partial class StoredMutation
{
    [DataField]
    public ProtoId<GeneticMutationPrototype> Mutation;

    [DataField]
    public ChromosomeKind Chromosome = ChromosomeKind.None;
}

[DataDefinition]
public sealed partial class AdvancedInjector
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public List<StoredMutation> Mutations = new();
}

/// <summary>
/// Генетический облик: имя, ДНК (уникальные ферменты), внешность (уникальная идентичность) и особенности.
/// </summary>
[DataDefinition]
public sealed partial class GeneticMakeupData
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Dna = string.Empty;

    [DataField]
    public string BloodType = string.Empty;

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData> Profiles = new();

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> Markings = new();

    public GeneticMakeupData Clone()
    {
        return new GeneticMakeupData
        {
            Name = Name,
            Dna = Dna,
            BloodType = BloodType,
            Profiles = new(Profiles),
            Markings = Markings.ToDictionary(
                c => c.Key,
                c => c.Value.ToDictionary(l => l.Key, l => new List<Marking>(l.Value))),
        };
    }
}

/// <summary>ДНК-сканер: замок не даёт извлечь пациента.</summary>
[RegisterComponent]
public sealed partial class DnaScannerComponent : Component
{
    [ViewVariables]
    public bool Locked;
}

/// <summary>Генетическая дискета (disk/data из SS13): мутации и один генетический облик.</summary>
[RegisterComponent]
public sealed partial class GeneticDataDiskComponent : Component
{
    [DataField]
    public int MaxMutations = 6;

    [DataField]
    public bool ReadOnly;

    [ViewVariables]
    public List<StoredMutation> Mutations = new();

    [ViewVariables]
    public GeneticMakeupData? Makeup;
}

/// <summary>
/// ДНК-инжектор (dnainjector из SS13). Активатор включает спящий ген цели, мутатор добавляет мутацию сверху,
/// инжектор облика переносит имя, внешность или особенности.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DnaInjectorComponent : Component
{
    [DataField, AutoNetworkedField]
    public DnaInjectorMode Mode = DnaInjectorMode.Mutator;

    [DataField, AutoNetworkedField]
    public List<StoredMutation> Mutations = new();

    [DataField, AutoNetworkedField]
    public bool Used;

    /// <summary>Активатор, впрыснутый в носителя гена: в консоли даёт хромосому.</summary>
    [DataField]
    public bool Research;

    [DataField]
    public TimeSpan InjectTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public GeneticMakeupData? Makeup;

    [ViewVariables]
    public DnaMakeupType MakeupType;
}

[Serializable, NetSerializable]
public enum DnaInjectorMode : byte
{
    Activator,
    Mutator,
    Makeup,
}

[Serializable, NetSerializable]
public enum DnaInjectorVisuals : byte
{
    Used,
}

[Serializable, NetSerializable]
public enum SequenceScannerVisuals : byte
{
    Recharging,
}

/// <summary>Хромосома (chromosome из SS13): вставляется в консоль и затем в мутацию.</summary>
[RegisterComponent]
public sealed partial class ChromosomeComponent : Component
{
    [DataField(required: true)]
    public ChromosomeKind Kind;
}
