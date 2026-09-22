using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Heretic;

[RegisterComponent]
public sealed partial class HereticArenaParticipantComponent : Component
{
    [DataField]
    public EntityUid Arena;

    [DataField]
    public EntityUid LastAttacker;

    [DataField]
    public EntityUid TrainingBlade;

    [DataField]
    public bool IsVictor = false;
}
