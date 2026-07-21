using Content.Shared._CP14.Workbench.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CP14.Workbench;

/// <summary>
/// Raised directed on the entity who crafted an item at a workbench.
/// </summary>
public sealed class CP14WorkbenchCraftedEvent : EntityEventArgs
{
    public ProtoId<CP14WorkbenchRecipePrototype> Recipe;
    public EntProtoId Result;
    public EntityUid ResultEntity;
    public EntityUid Workbench;

    public CP14WorkbenchCraftedEvent(
        ProtoId<CP14WorkbenchRecipePrototype> recipe,
        EntProtoId result,
        EntityUid resultEntity,
        EntityUid workbench)
    {
        Recipe = recipe;
        Result = result;
        ResultEntity = resultEntity;
        Workbench = workbench;
    }
}
