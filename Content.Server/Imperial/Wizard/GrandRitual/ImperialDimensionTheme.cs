using System.Linq;
using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Imperial.Wizard.Spells;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Wizard.GrandRitual;

/// <summary>Тема измерения (/datum/dimension_theme): пол и стены.</summary>
public readonly record struct ImperialDimensionTheme(string Floor, EntProtoId? Wall);
