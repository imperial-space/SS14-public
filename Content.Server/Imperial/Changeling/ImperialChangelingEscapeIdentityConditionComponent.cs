namespace Content.Server.Imperial.Changeling;

/// <summary>/datum/objective/escape/escape_with_identity.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingEscapeIdentityConditionComponent : Component
{
    /// <summary>target_real_name: имя может смениться за раунд, поэтому запоминается.</summary>
    [ViewVariables]
    public string? TargetName;

    /// <summary>target_missing_id: у цели не было своей ID-карты.</summary>
    [ViewVariables]
    public bool TargetMissingId;
}
