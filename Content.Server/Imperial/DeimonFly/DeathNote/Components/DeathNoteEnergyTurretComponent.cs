namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Temporarily forces a station energy turret into its damaging fire mode and
/// records the Death Note targets for which that override is active.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteEnergyTurretComponent : Component
{
    [ViewVariables]
    public int OriginalFireMode;

    [ViewVariables]
    public int LethalFireMode;

    [ViewVariables]
    public bool OriginalEnabled;

    [ViewVariables]
    public bool OriginalHtnEnabled;

    [ViewVariables]
    public bool HasHtn;

    [ViewVariables]
    public bool CreatedRangedCombat;

    [ViewVariables]
    public EntityUid? OriginalRangedTarget;

    [ViewVariables]
    public float TargetRange;

    [ViewVariables]
    public HashSet<EntityUid> Targets = new();
}

/// <summary>
/// Marks a projectile fired specifically at a Death Note turret target.
/// Its ordinary projectile damage is replaced only if it reaches that target.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteEnergyTurretProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid Target;
}
