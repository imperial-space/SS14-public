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
