using Content.Shared.Imperial.Fishing;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>/datum/fish_movement/zippy.</summary>
public sealed class ImperialFishMovementZippy : ImperialFishMovement
{
    protected override float InitialShortJumpChance => 2.25f * 3;

    public ImperialFishMovementZippy(ImperialFishingChallenge master, IRobustRandom random) : base(master, random)
    {
    }
}
