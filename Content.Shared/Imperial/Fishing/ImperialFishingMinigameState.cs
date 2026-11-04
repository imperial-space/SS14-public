using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Состояние мини-игры для окна.</summary>
[Serializable, NetSerializable]
public sealed class ImperialFishingMinigameState : BoundUserInterfaceState
{
    public float BaitPosition;
    public float BaitHeight;
    public int BaitPixelHeight;
    public float FishPosition;
    public float Completion;
    public string Background = "background_default";
    public string Frame = "frame_wood";
    public ImperialFishIcon Icon;
    public bool Flip;
    public bool Camo;
    public bool Antigrav;
    public bool Bidirectional;
}
