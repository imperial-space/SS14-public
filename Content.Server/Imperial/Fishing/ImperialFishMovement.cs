using Content.Shared.Imperial.Fishing;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Движение рыбы в мини-игре (/datum/fish_movement и наследники).
/// </summary>
[Virtual]
public class ImperialFishMovement
{
    private const float TargetMinDistance = 6;
    private const float FrictionMult = 0.9f;
    private const float ShortJumpMinDistance = 100;
    private const float ShortJumpMaxDistance = 200;

    protected readonly ImperialFishingChallenge Master;
    protected readonly IRobustRandom Random;

    public int TimesFired;
    public float ShortJumpChance = 2.25f;
    public float LongJumpChance = 0.0625f;
    public float ShortJumpVelocityLimit = 400;
    public float LongJumpVelocityLimit = 200;
    public float CurrentVelocityLimit;
    public float FishIdleVelocity;
    public float? TargetPosition;
    public bool CanInterruptMove = true;
    public float FishVelocity;

    protected virtual float InitialShortJumpChance => 2.25f;
    protected virtual float InitialLongJumpChance => 0.0625f;

    public ImperialFishMovement(ImperialFishingChallenge master, IRobustRandom random)
    {
        Master = master;
        Random = random;
        ShortJumpChance = InitialShortJumpChance;
        LongJumpChance = InitialLongJumpChance;
    }

    public static ImperialFishMovement Create(ImperialFishMovementType type, ImperialFishingChallenge master, IRobustRandom random)
    {
        return type switch
        {
            ImperialFishMovementType.Slow => new ImperialFishMovementSlow(master, random),
            ImperialFishMovementType.Zippy => new ImperialFishMovementZippy(master, random),
            ImperialFishMovementType.Accelerando => new ImperialFishMovementAccelerando(master, random),
            ImperialFishMovementType.Choppy => new ImperialFishMovementChoppy(master, random),
            ImperialFishMovementType.Plunger => new ImperialFishMovementPlunger(master, random),
            _ => new ImperialFishMovement(master, random),
        };
    }

    private bool Prob(float chance) => Random.Prob(Math.Clamp(chance / 100f, 0f, 1f));

    /// <summary>
    /// adjust_to_difficulty(): шансы прыжков выше 1% плавно растут к 100% на максимальной сложности.
    /// </summary>
    public virtual void AdjustToDifficulty()
    {
        var squareAngleRad = MathF.PI / 2;
        var zeroOneDifficulty = Master.Difficulty / 100f;
        ShortJumpChance = Adjust(ShortJumpChance, squareAngleRad, zeroOneDifficulty);
        LongJumpChance = Adjust(LongJumpChance, squareAngleRad, zeroOneDifficulty);
    }

    private float Adjust(float chance, float squareAngleRad, float zeroOneDifficulty)
    {
        if (chance > 1)
            return MathF.Pow(zeroOneDifficulty, squareAngleRad - MathF.Atan(chance * 1 / squareAngleRad)) * 100;

        return chance * Master.Difficulty;
    }

    public virtual void ResetDifficultyValues()
    {
        ShortJumpChance = InitialShortJumpChance;
        LongJumpChance = InitialLongJumpChance;
    }

    public virtual void MoveFish(float dt)
    {
        TimesFired++;
        // Прыжки бросаются каждый второй тик (раз в децисекунду), как в TG.
        var canRoll = TimesFired % 2 == 1;
        var longChance = LongJumpChance;
        var shortChance = ShortJumpChance;

        if (TargetPosition != null && MathF.Abs(TargetPosition.Value - Master.FishPosition) < TargetMinDistance)
            TargetPosition = null;

        if (canRoll && (CanInterruptMove || TargetPosition == null) && Prob(longChance))
        {
            // Прыжок на 75–100% свободного места, чаще туда, где места больше.
            var distanceFromTop = ImperialFishing.MinigameArea - Master.FishPosition - Master.FishHeight;
            var distanceFromBottom = Master.FishPosition;
            var topChance = distanceFromTop < ShortJumpMinDistance ? 0 : distanceFromTop / MathF.Max(distanceFromBottom, 1) * 100;
            var newTarget = Master.FishPosition;
            if (Prob(topChance))
                newTarget += distanceFromTop * Random.Next(75, 101) / 100f;
            else
                newTarget -= distanceFromBottom * Random.Next(75, 101) / 100f;

            TargetPosition = MathF.Round(newTarget);
            CurrentVelocityLimit = LongJumpVelocityLimit;
        }

        if (TargetPosition != null)
        {
            var distance = TargetPosition.Value - Master.FishPosition;
            var accelerationMult = GetAcceleration(dt);
            var targetAcceleration = distance * accelerationMult * dt;
            if (FishIdleVelocity != 0)
            {
                var idleVelocity = FishIdleVelocity;
                var absIdle = MathF.Abs(idleVelocity);
                if (MathF.Sign(idleVelocity) != MathF.Sign(targetAcceleration))
                {
                    var halvedRatio = accelerationMult * 0.5f / absIdle;
                    if (halvedRatio < 1)
                    {
                        var power = MathF.Min(halvedRatio + 0.5f, 1);
                        targetAcceleration *= 1 - MathF.Pow(halvedRatio, power);
                    }
                    else
                    {
                        targetAcceleration += idleVelocity;
                    }
                }
            }

            FishVelocity = FishVelocity * FrictionMult + targetAcceleration;
        }
        else if (canRoll && Prob(shortChance))
        {
            var distanceFromTop = ImperialFishing.MinigameArea - Master.FishPosition - Master.FishHeight;
            var distanceFromBottom = Master.FishPosition;
            float? jumpLength = null;
            if (distanceFromTop >= ShortJumpMinDistance)
                jumpLength = Random.Next((int) ShortJumpMinDistance, (int) ShortJumpMaxDistance + 1);
            if (distanceFromBottom >= ShortJumpMinDistance && (jumpLength == null || Random.Prob(0.5f)))
                jumpLength = -Random.Next((int) ShortJumpMinDistance, (int) ShortJumpMaxDistance + 1);

            TargetPosition = Math.Clamp(Master.FishPosition + (jumpLength ?? 0), 0, ImperialFishing.MinigameArea - Master.FishHeight);
            CurrentVelocityLimit = ShortJumpVelocityLimit;
        }

        FishVelocity = Math.Clamp(FishVelocity, -CurrentVelocityLimit, CurrentVelocityLimit);
        SetFishPosition(dt);
    }

    protected virtual float GetAcceleration(float dt)
    {
        return 0.3f * Master.Difficulty + 0.5f;
    }

    protected virtual void SetFishPosition(float dt)
    {
        Master.FishPosition = Math.Clamp(Master.FishPosition + FishVelocity * dt, 0, ImperialFishing.MinigameArea - Master.FishHeight);
    }
}

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

/// <summary>/datum/fish_movement/zippy.</summary>
public sealed class ImperialFishMovementZippy : ImperialFishMovement
{
    protected override float InitialShortJumpChance => 2.25f * 3;

    public ImperialFishMovementZippy(ImperialFishingChallenge master, IRobustRandom random) : base(master, random)
    {
    }
}

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
