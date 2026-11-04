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

/// <summary>Когда существо в последний раз оценивало блюдо (last_check_time, 5 секунд).</summary>
[RegisterComponent]
public sealed partial class ImperialFoodTasterComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> LastChecks = new();
}
