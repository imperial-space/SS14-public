using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.Mood;
using Content.Server.Imperial.Quirks;
using Content.Shared.Medical;
using Content.Shared.Alert;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Kitchen;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Kitchen;
using Content.Server.Kitchen.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Kitchen;

/// <summary>TRAIT_HANDMADE: блюдо приготовил игрок, его качество — сложность рецепта.</summary>
[RegisterComponent]
public sealed partial class ImperialHandmadeComponent : Component
{
    [DataField]
    public int Complexity;

    /// <summary>bitecount == 0: бафф даёт только первый укус.</summary>
    [ViewVariables]
    public bool Bitten;
}
