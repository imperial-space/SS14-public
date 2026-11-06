namespace Content.Server.Imperial.Changeling;

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
