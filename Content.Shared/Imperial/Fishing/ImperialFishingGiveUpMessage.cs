using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Игрок закрыл окно мини-игры.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingGiveUpMessage : BoundUserInterfaceMessage;
