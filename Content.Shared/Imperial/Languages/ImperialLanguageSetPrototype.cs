using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Languages;

/// <summary>Набор языков по виду или типу существа (language_holder TG). Читается и клиентом, поэтому в общей сборке.</summary>
[Prototype]
public sealed partial class ImperialLanguageSetPrototype : IPrototype
{
    /// <summary>Id вида (Human, Reptilian…) либо Borg / StationAi / Default.</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Spoken = new();

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Understood = new();

    [DataField]
    public Dictionary<ProtoId<ImperialLanguagePrototype>, int> Partial = new();
}
