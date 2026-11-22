using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.SyndieBattle.Events;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SyndieBattleRespawnUiStateEvent : EntityEventArgs
{
    [DataField]
    public bool Visible;

    [DataField]
    public TimeSpan CooldownRemaining;
}
