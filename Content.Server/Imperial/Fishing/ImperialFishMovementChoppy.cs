using Content.Shared.Imperial.Fishing;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>/datum/fish_movement/choppy: положение обновляется рывками раз в полсекунды.</summary>
public sealed class ImperialFishMovementChoppy : ImperialFishMovement
{
    private float _fauxPosition;

    public ImperialFishMovementChoppy(ImperialFishingChallenge master, IRobustRandom random) : base(master, random)
    {
    }

    protected override void SetFishPosition(float dt)
    {
        _fauxPosition = Math.Clamp(_fauxPosition + FishVelocity * dt, 0, ImperialFishing.MinigameArea - Master.FishHeight);
        if (TimesFired % 10 == 0)
            Master.FishPosition = _fauxPosition;
    }
}
