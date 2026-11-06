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
                jumpLength = Random.Next((int)ShortJumpMinDistance, (int)ShortJumpMaxDistance + 1);
            if (distanceFromBottom >= ShortJumpMinDistance && (jumpLength == null || Random.Prob(0.5f)))
                jumpLength = -Random.Next((int)ShortJumpMinDistance, (int)ShortJumpMaxDistance + 1);

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
