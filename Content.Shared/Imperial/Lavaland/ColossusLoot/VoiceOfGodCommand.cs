using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.ColossusLoot;

[Serializable, NetSerializable]
public enum VoiceOfGodCommand
{
    Stop,
    Weaken,
    Sleep,
    Vomit,
    Silence,
    Wake,
    Heal,
    Pain,
    Burn,
    Stand,
    Rest,
}
