namespace Content.Shared.Imperial.Medieval.DoOnUse.Action;

[RegisterComponent]
public sealed partial class AddActionOnUseComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public string ActionId = "idkreally";
}
