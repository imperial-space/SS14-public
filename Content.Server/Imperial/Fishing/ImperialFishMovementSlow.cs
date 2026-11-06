using Content.Shared.Imperial.Fishing;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>/datum/fish_movement/slow.</summary>
public sealed class ImperialFishMovementSlow : ImperialFishMovement
{
    protected override float InitialShortJumpChance => 0;
    protected override float InitialLongJumpChance => 1.5f;

    public ImperialFishMovementSlow(ImperialFishingChallenge master, IRobustRandom random) : base(master, random)
    {
        LongJumpVelocityLimit = 150;
        CanInterruptMove = false;
    }
}
