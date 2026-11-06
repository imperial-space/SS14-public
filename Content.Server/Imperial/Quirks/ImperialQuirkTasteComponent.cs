using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;

namespace Content.Server.Imperial.Quirks;

/// <summary>Вкусы языка (liked_foodtypes / disliked_foodtypes), которые меняют причуды.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkTasteComponent : Component
{
    /// <summary>Язык человека: любит фастфуд и жареное, не любит гадость, сырое и жуков.</summary>
    [DataField]
    public ImperialFoodType Liked = ImperialFoodType.Junkfood;

    [DataField]
    public ImperialFoodType Disliked = ImperialFoodType.Gross | ImperialFoodType.Raw | ImperialFoodType.Bugs;

    [ViewVariables]
    public TimeSpan NextReaction;
}
