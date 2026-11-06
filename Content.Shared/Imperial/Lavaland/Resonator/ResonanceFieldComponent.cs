using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Lavaland.Resonator;

[RegisterComponent, AutoGenerateComponentPause]
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

    [ViewVariables]
    public bool Rupturing = false;

    /// <summary>Сколько поле живёт до взрыва.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(2);

    /// <summary>Когда поле взорвётся.</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan BurstAt;

    [DataField]
    public float BurstFieldRange = 0.6f;

    [DataField]
    public float BurstFieldRange2 = 1.6f;

    [DataField]
    public float UpdateRange = 0.4f;
}
