using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

public enum ImperialFishingPhase : byte
{
    /// <summary>WAIT_PHASE: поплавок покачивается.</summary>
    Wait,

    /// <summary>BITING_PHASE: клюёт, нужно подсечь.</summary>
    Biting,

    /// <summary>MINIGAME_PHASE: вываживание.</summary>
    Minigame,
}
