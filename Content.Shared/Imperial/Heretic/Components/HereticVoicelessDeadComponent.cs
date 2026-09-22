namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marker for Voiceless Dead familiars spawned by the Heretic's Imperfect Ritual.
/// On death, drops a Living Heart.
/// </summary>
[RegisterComponent]
public sealed partial class HereticVoicelessDeadComponent : Component
{
    // The heretic that performed the Imperfect Ritual, used to enforce the two-active-creature limit.
    [DataField]
    public EntityUid Master = EntityUid.Invalid;
}
