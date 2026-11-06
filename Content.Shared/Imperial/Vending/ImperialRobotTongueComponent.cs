using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>organ/tongue/robot TG: речь становится механической, пока язык во рту.</summary>
[RegisterComponent]
public sealed partial class ImperialRobotTongueComponent : Component
{
    [DataField]
    public ProtoId<SpeechVerbPrototype> SpeechVerb = "Robotic";

    [DataField]
    public ProtoId<SpeechSoundsPrototype> SpeechSounds = "Borg";
}
