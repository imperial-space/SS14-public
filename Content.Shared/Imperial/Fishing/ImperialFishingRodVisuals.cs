using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Наложения удочки (update_overlays): цвет лески, крючок, наживка.</summary>
[Serializable, NetSerializable]
public enum ImperialFishingRodVisuals : byte
{
    LineColor,
    Hook,
    Bait,
    Collapsed,
}
