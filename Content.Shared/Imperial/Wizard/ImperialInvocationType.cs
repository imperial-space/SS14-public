using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>INVOCATION_NONE / WHISPER / SHOUT / EMOTE.</summary>
[Serializable, NetSerializable]
public enum ImperialInvocationType : byte
{
    None,
    Whisper,
    Shout,
    Emote,
}
