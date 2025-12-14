/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CP14.Workbench.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._CP14.Workbench;

/// <summary>
/// This entity can be used to craft other objects through the interface
/// </summary>
[RegisterComponent]
[Access(typeof(CP14WorkbenchSystem))]
public sealed partial class CP14WorkbenchComponent : Component
{
    /// <summary>
    /// Crafting speed modifier on this workbench.
    /// </summary>
    [DataField]
    public float CraftSpeed = 1f;

    /// <summary>
    /// List of recipes available for crafting on this type of workbench
    /// </summary>
    [DataField]
    public List<ProtoId<CP14WorkbenchRecipePrototype>> Recipes = new();

    /// <summary>
    /// Played during crafting. Can be overwritten by the crafting sound of a specific recipe.
    /// </summary>
    ///

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string CraftSound = "/Audio/Imperial/Medieval/craft_wood.ogg";

}
