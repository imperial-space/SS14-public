namespace Content.Shared.Imperial.Medieval.DoOnUse.Spawn;

[RegisterComponent]
public sealed partial class SpawnOnUseComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public string EntityPrototype = "idkreally";
}
