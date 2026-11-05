using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;

namespace Content.Server.Imperial.Quirks;

/// <summary>TRAIT_AGEUSIA: вкусы не ощущаются.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkAgeusiaComponent : Component;
