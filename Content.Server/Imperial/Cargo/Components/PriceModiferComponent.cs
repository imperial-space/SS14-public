namespace Content.Server.Imperial.Cargo.Components;

[RegisterComponent]
public sealed partial class PriceModifierComponent : Component
{
    [DataField]
    public float Modifier;
}
