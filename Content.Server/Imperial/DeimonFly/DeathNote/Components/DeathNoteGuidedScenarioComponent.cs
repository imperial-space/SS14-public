using Content.Shared.Damage;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

[RegisterComponent]
public sealed partial class DeathNoteGuidedScenarioComponent : Component
{
    [ViewVariables]
    public uint EntryId;

    [ViewVariables]
    public DeathNoteGuidedScenarioType Scenario;

    [ViewVariables]
    public TimeSpan ExpiresAt;

    /// <summary>
    /// Keeps a scenario armed until its one allowed world interaction succeeds.
    /// Used by the silent airlock accident after the assigned execution time.
    /// </summary>
    [ViewVariables]
    public bool PersistentUntilTriggered;

    [ViewVariables]
    public bool Triggered;

    [ViewVariables]
    public DamageSpecifier MinimumDamage = new();

    [ViewVariables]
    public DamageSpecifier MaximumDamage = new();

    [ViewVariables]
    public int ImpactCount;

    [ViewVariables]
    public TimeSpan ImpactInterval;

    [ViewVariables]
    public TimeSpan AirlockForceCloseDelay;

    [ViewVariables]
    public TimeSpan DoorCloseStageDuration;

    [ViewVariables]
    public TimeSpan VendingFallDuration;

    [ViewVariables]
    public TimeSpan CleanupGracePeriod;

    [ViewVariables]
    public TimeSpan EffectTrackingDuration = TimeSpan.FromMinutes(5);

    [ViewVariables]
    public float AirlockAutoCloseDelayModifier;

    [ViewVariables]
    public float DoorwayImpactRadius;

    [ViewVariables]
    public SoundSpecifier? VendingImpactSound;

    [ViewVariables]
    public List<DeathNoteConsumablePoisonOption> ConsumablePoisons = new();

    [ViewVariables]
    public int PoisonedConsumableLimit = 2;

    [ViewVariables]
    public HashSet<EntityUid> PoisonedConsumables = new();
}
