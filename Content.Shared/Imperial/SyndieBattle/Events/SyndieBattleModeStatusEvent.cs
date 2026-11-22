using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.SyndieBattle.Events;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SyndieBattleModeStatusEvent : EntityEventArgs
{
    [DataField]
    public bool Active;

    [DataField]
    public SoundSpecifier? Music;
}
