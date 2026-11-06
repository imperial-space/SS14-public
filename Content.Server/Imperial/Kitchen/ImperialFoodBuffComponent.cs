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

/// <summary>/datum/status_effect/food: бафф от блюда, сделанного руками.</summary>
[RegisterComponent]
public sealed partial class ImperialFoodBuffComponent : Component
{
    [ViewVariables]
    public int Strength;
    [ViewVariables]
    public TimeSpan End;
}
