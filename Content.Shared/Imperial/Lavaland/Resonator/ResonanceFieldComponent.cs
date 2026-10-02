using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Lavaland.Resonator;

[RegisterComponent]
public sealed partial class ResonanceFieldComponent : Component
{
    [DataField]
    public EntityUid? Creator;

    [DataField]
    public EntityUid? ParentResonator;

    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public float FailureProb = 0f;

    [DataField]
    public float AddingFailure = 50f;

    [DataField]
    public ResonatorMode Mode = ResonatorMode.Auto;

    public bool Rupturing = false;

    public float Timer = 0f;

    public float Duration = 2f;
}
