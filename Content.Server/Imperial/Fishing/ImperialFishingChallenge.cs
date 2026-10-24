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

public enum ImperialReelingState : byte
{
    Idle,
    Up,
    Down,
}

/// <summary>
/// Одна рыбалка (/datum/fishing_challenge): фазы, награда и физика мини-игры.
/// Логикой наград и событиями занимается <see cref="ImperialFishingSystem"/>.
/// </summary>
public sealed class ImperialFishingChallenge
{
    private const float BaitMinVelocityBounce = 150;
    private const float MaxFishCompletionMalus = 15;
    public const float BaitHeightDifficultyMalus = 1.3f;
    public static readonly TimeSpan BitingTimeWindow = TimeSpan.FromSeconds(4);

    public EntityUid User;
    public EntityUid Rod;
    public EntityUid Location;
    public EntityUid Float;
    public ProtoId<ImperialFishSourcePrototype> Source;

    public bool Completed;
    public ImperialFishingRules SpecialEffects;
    public readonly List<ImperialFishingRules> ActiveEffects = new();
    public TimeSpan NextActiveEffect;
    public ImperialFishingRules? CurrentActiveEffect;

    /// <summary>reward_path: прототип, "dud" или сущность (например, рыба из аквариума).</summary>
    public string? RewardProto;
    public EntityUid? RewardEntity;

    public float Difficulty = ImperialFishing.DefaultDifficulty;
    public ImperialFishingPhase Phase = ImperialFishingPhase.Wait;
    public TimeSpan NextPhaseAt;
    public Vector2 WaitTimeRange = new(3, 25);
    public TimeSpan LastBaitingClick;
    public string Background = "background_default";
    public ImperialFishIcon FishIcon = ImperialFishIcon.Fish;

    public float ExperienceMultiplier = 1;

    // Блесна.
    public Vector2? SpinFrequency;
    public bool SpinReady;
    public TimeSpan? SpinReadyAt;
    public TimeSpan? SpinMissAt;
    public TimeSpan? AutoSpinAt;
    public TimeSpan? AutoStartAt;

    // Мини-игра.
    public TimeSpan StartTime;
    public float FishHeight = 50;
    public float BaitHeight = 360;
    public float InitialBaitHeight = 360;
    public int BaitPixelHeight = ImperialFishing.BaitPixelHeight;
    public float FishPosition;
    public float BaitPosition;
    public float BaitVelocity;
    public float Completion = 30;
    public float CompletionLoss = 6;
    public float CompletionGain = 5;
    public ImperialFishMovement? Mover;
    public ImperialReelingState Reeling = ImperialReelingState.Idle;
    public float GravityVelocity = -800;
    public float ReelingVelocity = 1200;
    public float BaitBounceMult = 0.6f;
    public float DecelerationMult = 1.8f;
    public float OverlapVelocityMult = 0.6f;
    public float Accumulator;

    /// <summary>Победа по таймеру крючка-капкана.</summary>
    public TimeSpan? KillWinAt;

    /// <summary>Время, когда во время вываживания нужно перестать держать удочку и отменить (3 с).</summary>
    public TimeSpan? StopAt;

    public bool Bidirectional => (SpecialEffects & ImperialFishingRules.Bidirectional) != 0;

    /// <summary>
    /// Применить леску, крючок, удочку и навык (конструктор /datum/fishing_challenge).
    /// </summary>
    public void Setup(ImperialFishingRodComponent rod, ImperialFishingLineComponent? line, ImperialFishingHookComponent? hook, float skillModifier)
    {
        if (line != null)
        {
            CompletionGain += 1;
            if ((line.Traits & ImperialFishingLineTraits.Bouncy) != 0)
                CompletionLoss -= 2;
            if ((line.Traits & ImperialFishingLineTraits.Stiff) != 0)
            {
                CompletionLoss += 1;
                CompletionGain -= 1;
            }

            if ((line.Traits & ImperialFishingLineTraits.Autoreel) != 0)
                SpecialEffects |= ImperialFishingRules.Autoreel;
        }

        if (hook != null)
        {
            if ((hook.Traits & ImperialFishingHookTraits.Weighted) != 0)
                BaitBounceMult = 0.1f;
            if ((hook.Traits & ImperialFishingHookTraits.Bidirectional) != 0)
                SpecialEffects |= ImperialFishingRules.Bidirectional;
            if ((hook.Traits & ImperialFishingHookTraits.NoEscape) != 0)
                SpecialEffects |= ImperialFishingRules.NoEscape;
            if ((hook.Traits & ImperialFishingHookTraits.Ensnare) != 0)
                CompletionLoss -= 2;
            if ((hook.Traits & ImperialFishingHookTraits.Kill) != 0)
                SpecialEffects |= ImperialFishingRules.Kill;
        }

        // Навык ускоряет вываживание (модификаторы рыбалки отрицательные).
        CompletionLoss += skillModifier / 5;
        CompletionGain -= skillModifier / 7.5f;

        ReelingVelocity *= rod.BaitSpeedMult;
        CompletionGain *= rod.CompletionSpeedMult;
        BaitBounceMult *= rod.BouncinessMult;
        DecelerationMult *= rod.DecelerationMult;
        GravityVelocity *= rod.GravityMult;
        OverlapVelocityMult = MathF.Pow(OverlapVelocityMult, rod.BaitSpeedMult);
    }

    /// <summary>Множитель стартового прогресса по скорости подсечки (start_minigame_phase).</summary>
    public void ApplyReactionBonus(TimeSpan timeLeft, bool autoReel)
    {
        if (Difficulty > ImperialFishing.DefaultDifficulty)
            Completion -= MaxFishCompletionMalus * (Difficulty * 0.01f);

        if (autoReel)
        {
            Completion *= 1.3f;
            return;
        }

        var window = BitingTimeWindow;
        if (timeLeft <= window - TimeSpan.FromSeconds(3))
            Completion *= 0.65f;
        else if (timeLeft <= window - TimeSpan.FromSeconds(2))
            Completion *= 0.82f;
        else if (timeLeft >= window - TimeSpan.FromSeconds(0.5))
            Completion *= 1.4f;
        else if (timeLeft >= window - TimeSpan.FromSeconds(1))
            Completion *= 1.2f;
    }

    /// <summary>adjust_to_difficulty().</summary>
    public void AdjustToDifficulty()
    {
        Mover?.AdjustToDifficulty();
        BaitHeight -= MathF.Round(Difficulty * BaitHeightDifficultyMalus);
        BaitPixelHeight = (int) MathF.Round(ImperialFishing.BaitPixelHeight * (BaitHeight / 360f));
        ExperienceMultiplier += Difficulty * ImperialFishing.SkillDifficultyExpMult;
    }

    /// <summary>
    /// move_bait(): возвращает true, если рыба поймана (completion ≥ 100), false — если ушла, null — игра продолжается.
    /// </summary>
    public bool? MoveBait(float dt)
    {
        var shouldBounce = MathF.Abs(BaitVelocity) > BaitMinVelocityBounce;
        BaitPosition += BaitVelocity * dt;

        if (BaitPosition > ImperialFishing.MinigameArea - BaitHeight)
        {
            BaitPosition = ImperialFishing.MinigameArea - BaitHeight;
            BaitVelocity = Reeling == ImperialReelingState.Up || !shouldBounce ? 0 : -BaitVelocity * BaitBounceMult;
        }
        else if (BaitPosition < 0)
        {
            BaitPosition = 0;
            BaitVelocity = Reeling == ImperialReelingState.Down || !shouldBounce ? 0 : -BaitVelocity * BaitBounceMult;
        }

        var fishOnBait = FishPosition + FishHeight >= BaitPosition && BaitPosition + BaitHeight >= FishPosition;

        float velocityChange;
        switch (Reeling)
        {
            case ImperialReelingState.Up:
                velocityChange = ReelingVelocity;
                break;
            case ImperialReelingState.Down:
                velocityChange = -ReelingVelocity;
                break;
            default:
                velocityChange = !Bidirectional || BaitVelocity > 0 ? GravityVelocity : -GravityVelocity;
                break;
        }

        velocityChange *= (fishOnBait ? OverlapVelocityMult : 1) * dt;
        velocityChange = MathF.Round(velocityChange);

        if (CurrentActiveEffect == ImperialFishingRules.Antigrav)
            velocityChange = -velocityChange;

        if (Reeling == ImperialReelingState.Idle && (SpecialEffects & ImperialFishingRules.Autoreel) != 0)
        {
            var baitCenter = BaitPosition + BaitHeight / 2;
            var autoAdjustment = ReelingVelocity * 0.2f;
            velocityChange += FishPosition > baitCenter ? autoAdjustment : -autoAdjustment;
        }

        // Торможение, если ускорение направлено против скорости.
        if (BaitVelocity > 0 && velocityChange < 0)
            BaitVelocity += MathF.Max(-BaitVelocity, velocityChange * DecelerationMult);
        else if (BaitVelocity < 0 && velocityChange > 0)
            BaitVelocity += MathF.Min(-BaitVelocity, velocityChange * DecelerationMult);

        if (Bidirectional && Reeling == ImperialReelingState.Idle)
        {
            if (velocityChange < 0)
                BaitVelocity = MathF.Max(BaitVelocity + velocityChange, 0);
            else if (velocityChange > 0)
                BaitVelocity = MathF.Min(BaitVelocity + velocityChange, 0);
        }
        else
        {
            BaitVelocity += velocityChange;
        }

        bool? result = null;
        if (fishOnBait)
        {
            Completion += CompletionGain * dt;
            if (Completion >= 100)
                result = true;
        }
        else
        {
            Completion -= CompletionLoss * dt;
            if (Completion <= 0 && (SpecialEffects & ImperialFishingRules.NoEscape) == 0)
                result = false;
        }

        Completion = Math.Clamp(Completion, 0, 100);
        return result;
    }
}
