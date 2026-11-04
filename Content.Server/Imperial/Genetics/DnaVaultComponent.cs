using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// ДНК-хранилище (dna_vault из SS13): цель станции — собрать образцы животных, растений и разумных гуманоидов.
/// После заполнения каждый член экипажа может один раз получить одно из двух случайных улучшений.
/// </summary>
[RegisterComponent]
public sealed partial class DnaVaultComponent : Component
{
    [ViewVariables]
    public int AnimalsMax = 100;

    [ViewVariables]
    public int PlantsMax = 100;

    [ViewVariables]
    public int DnaMax = 100;

    [ViewVariables]
    public HashSet<string> Animals = new();

    [ViewVariables]
    public HashSet<string> Plants = new();

    [ViewVariables]
    public HashSet<string> Dna = new();

    [ViewVariables]
    public bool Completed;

    /// <summary>Пары улучшений, выпавшие каждому, кто открыл хранилище.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, List<ProtoId<GeneticMutationPrototype>>> PowerLottery = new();

    /// <summary>Улучшения, из которых выбирается пара (vault_mutation.dm).</summary>
    [DataField]
    public List<ProtoId<GeneticMutationPrototype>> Powers = new()
    {
        "MutationBreathless",
        "MutationDextrous",
        "MutationQuick",
        "MutationFireImmunity",
        "MutationPlasmocile",
        "MutationQuickRecovery",
        "MutationTough",
    };

    [DataField]
    public SoundSpecifier UploadSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
}
