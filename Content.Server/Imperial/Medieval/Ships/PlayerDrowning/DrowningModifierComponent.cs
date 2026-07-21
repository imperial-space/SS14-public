namespace Content.Shared.Drowning;

[RegisterComponent]
public sealed partial class DrowningModifierComponent : Component
{
    [DataField]
    public float ResistanceModifier = 1.0f;
}
