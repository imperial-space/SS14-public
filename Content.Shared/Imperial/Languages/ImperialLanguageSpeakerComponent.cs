using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Languages;

/// <summary>/datum/language_holder: какие языки знает существо и на каком говорит.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialLanguageSpeakerComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialLanguagePrototype>> Spoken = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialLanguagePrototype>> Understood = new();

    /// <summary>grant_partial_language: язык понимается на столько процентов.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ImperialLanguagePrototype>, int> Partial = new();

    /// <summary>add_blocked_language: язык нельзя ни понять, ни выучить, пока блок не снят.</summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialLanguagePrototype>> Blocked = new();

    /// <summary>selected_language.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ImperialLanguagePrototype>? Current;

    [ViewVariables]
    public EntityUid? MenuAction;
}
