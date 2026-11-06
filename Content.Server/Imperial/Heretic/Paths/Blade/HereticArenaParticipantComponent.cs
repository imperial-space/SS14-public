using Content.Shared.Damage;

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

    [DataField]
    public DamageSpecifier ParticipantStateChangedHeal = new() { DamageDict = { ["Blunt"] = -60, ["Heat"] = -60, ["Cellular"] = -60, ["Asphyxiation"] = -60, ["Poison"] = -60 } };

    [DataField]
    public TimeSpan ParticipantStateChangedDelay = TimeSpan.FromMilliseconds(60_000);
}
