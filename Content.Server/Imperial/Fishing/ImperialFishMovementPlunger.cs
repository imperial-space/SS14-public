using Content.Shared.Imperial.Fishing;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>/datum/fish_movement/plunger: рыба ныряет на дно и всплывает.</summary>
public sealed class ImperialFishMovementPlunger : ImperialFishMovement
{
    private bool _isPlunging = true;
    private float _plungingSpeed = -22;

    public ImperialFishMovementPlunger(ImperialFishingChallenge master, IRobustRandom random) : base(master, random)
    {
    }

    public override void AdjustToDifficulty()
    {
        base.AdjustToDifficulty();
        _plungingSpeed += MathF.Round(_plungingSpeed * Master.Difficulty * 0.03f);
        FishIdleVelocity += _plungingSpeed;
    }

    public override void ResetDifficultyValues()
    {
        base.ResetDifficultyValues();
        if (_isPlunging)
            FishIdleVelocity -= _plungingSpeed;
        _plungingSpeed = -22;
    }

    public override void MoveFish(float dt)
    {
        var fishArea = ImperialFishing.MinigameArea - Master.FishHeight;
        if (_isPlunging)
        {
            if (TargetPosition > Master.FishPosition)
                TargetPosition = null;

            if (Master.FishPosition / fishArea <= 0.04f)
            {
                FishIdleVelocity -= _plungingSpeed;
                _isPlunging = false;
            }
        }
        else if ((fishArea - Master.FishPosition) / fishArea <= 0.04f)
        {
            FishIdleVelocity += _plungingSpeed;
            _isPlunging = true;
        }

        base.MoveFish(dt);
    }
}
