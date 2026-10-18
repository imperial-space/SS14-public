namespace Content.Server.Imperial.Changeling;

/// <summary>Правило генокрадов TG: кого сделали генокрадами.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingRuleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Minds = new();
}

/// <summary>Роль разума генокрада (для RoleRequirement целей).</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingRoleComponent : Component;

/// <summary>/datum/antagonist/fallen_changeling: генокрад, которого поглотил другой генокрад.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingFallenComponent : Component;

/// <summary>Генокрада поглотили: разум остался без способностей.</summary>
public readonly record struct ImperialChangelingFellEvent(EntityUid Mind);

/// <summary>/datum/objective/absorb: извлечь N совместимых генов.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingAbsorbConditionComponent : Component
{
    /// <summary>gen_amount_goal(6, 8).</summary>
    [DataField]
    public int Min = 6;

    [DataField]
    public int Max = 8;

    [ViewVariables]
    public int Target;
}

/// <summary>/datum/objective/absorb_most.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingAbsorbMostConditionComponent : Component;

/// <summary>/datum/objective/absorb_changeling.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingAbsorbLingConditionComponent : Component;

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

/// <summary>maroon_objective.target = identity_theft.target: та же жертва, что у побега под личиной.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingPairedTargetComponent : Component;

/// <summary>Данные генокрада на разуме: в TG они живут в датуме антагониста и переживают тело.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingMindComponent : Component
{
    /// <summary>absorbed_count.</summary>
    [ViewVariables]
    public int AbsorbedCount;

    /// <summary>genetic_points + dna_cost купленного (absorb_changeling).</summary>
    [ViewVariables]
    public int EffectivePoints;

    /// <summary>initial(genetic_points).</summary>
    [ViewVariables]
    public int InitialPoints = 10;
}
