using Content.Shared.Damage;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

[RegisterComponent]
public sealed partial class DeathNoteDisposalVictimComponent : Component
{
    [ViewVariables]
    public EntityUid DisposalUnit;

    [ViewVariables]
    public TimeSpan NextImpact;

    [ViewVariables]
    public int ImpactsRemaining;

    [ViewVariables]
    public DamageSpecifier DamagePerImpact = new();

    [ViewVariables]
    public TimeSpan ImpactInterval;

    [ViewVariables]
    public TimeSpan ExpiresAt;
}
