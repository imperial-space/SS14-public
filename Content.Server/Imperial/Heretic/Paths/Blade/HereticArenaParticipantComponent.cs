namespace Content.Server.Imperial.Heretic.Paths.Blade;

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
