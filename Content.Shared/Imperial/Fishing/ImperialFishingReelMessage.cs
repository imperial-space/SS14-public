using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Состояние кнопки мыши: 0 — отпущена, 1 — тянуть вверх, 2 — вниз.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingReelMessage : BoundUserInterfaceMessage
{
    public readonly byte State;

    public ImperialFishingReelMessage(byte state)
    {
        State = state;
    }
}
