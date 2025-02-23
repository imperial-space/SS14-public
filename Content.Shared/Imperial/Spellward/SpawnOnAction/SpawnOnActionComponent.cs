namespace Content.Shared.Imperial.SpawnOnAction.Components;

[RegisterComponent]
public sealed partial class SpawnOnActionComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Object;
    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsFirst = true;
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Action;
    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public string ActionId = "ActionSpellwardSpawn";
    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public string Prototype;
}

