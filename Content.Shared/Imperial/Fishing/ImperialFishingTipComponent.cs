using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Записка «совет рыбаку» (/obj/item/paper/paperslip/fishing_tip): случайный совет при создании.</summary>
[RegisterComponent]
public sealed partial class ImperialFishingTipComponent : Component;
