using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;

namespace Content.Server.Imperial.Quirks;

/// <summary>TRAIT_VORACIOUS: ест быстрее (большими кусками).</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkVoraciousComponent : Component;
