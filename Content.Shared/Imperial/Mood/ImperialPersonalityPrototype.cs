using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>
/// /datum/personality TG. Выбирается чертой с тем же id в категории ImperialPersonalities;
/// личности из одной группы несовместимы.
/// </summary>
[Prototype]
public sealed partial class ImperialPersonalityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>savefile_key TG: по нему системы узнают личность.</summary>
    [DataField(required: true)]
    public string Key = string.Empty;

    [DataField]
    public List<string> Groups = new();
}
