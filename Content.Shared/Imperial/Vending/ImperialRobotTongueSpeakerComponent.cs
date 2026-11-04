using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>Прежняя речь тела, пока в нём механический язык.</summary>
[RegisterComponent]
public sealed partial class ImperialRobotTongueSpeakerComponent : Component
{
    [ViewVariables]
    public ProtoId<SpeechVerbPrototype> OldVerb = "Default";

    [ViewVariables]
    public ProtoId<SpeechSoundsPrototype>? OldSounds;
}
