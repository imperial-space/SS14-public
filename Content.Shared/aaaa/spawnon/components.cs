namespace Content.Shared.Imperial.SpawnOnAction;

[RegisterComponent]
public sealed partial class SpawnOnActionComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Object;
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsFirst = true;
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Action;
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string ActionId = "ActionSpellwardSpawn";
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Prototype = "MobHuman";
}
