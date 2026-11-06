using Content.Shared.Imperial.Fishing;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>/datum/fish_movement/accelerando: рыба разгоняется первые 30 секунд.</summary>
public sealed class ImperialFishMovementAccelerando : ImperialFishMovement
{
    private float _shortJumpVelAdd;
    private float _longJumpVelAdd;
    private const float AccelTimeCap = 30;

    public ImperialFishMovementAccelerando(ImperialFishingChallenge master, IRobustRandom random) : base(master, random)
    {
    }

    public override void MoveFish(float dt)
    {
        var secondsElapsed = TimesFired * dt;
        if (secondsElapsed >= AccelTimeCap)
        {
            base.MoveFish(dt);
            return;
        }

        if (TimesFired == 0)
        {
            _shortJumpVelAdd = ShortJumpVelocityLimit / AccelTimeCap;
            _longJumpVelAdd = LongJumpVelocityLimit / AccelTimeCap;
            base.MoveFish(dt);
            return;
        }

        if (CurrentVelocityLimit != 0)
        {
            var velAdd = CurrentVelocityLimit == ShortJumpVelocityLimit ? _shortJumpVelAdd : _longJumpVelAdd;
            CurrentVelocityLimit += MathF.Round(velAdd * dt, 2);
        }

        ShortJumpVelocityLimit += MathF.Round(_shortJumpVelAdd * dt, 2);
        LongJumpVelocityLimit += MathF.Round(_longJumpVelAdd * dt, 2);
        base.MoveFish(dt);
    }

    protected override float GetAcceleration(float dt)
    {
        var acceleration = base.GetAcceleration(dt);
        return acceleration + MathF.Min(acceleration, acceleration * TimesFired * dt / AccelTimeCap);
    }

    protected override void SetFishPosition(float dt)
    {
        FishVelocity = MathF.Round(FishVelocity);
        base.SetFishPosition(dt);
    }
}
