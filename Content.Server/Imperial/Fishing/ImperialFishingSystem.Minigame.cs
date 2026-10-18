using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Fishing;

public sealed partial class ImperialFishingSystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <summary>FISH_DAMAGE_PER_SECOND при крючке-капкане.</summary>
    private const float KillHookDamagePerSecond = 2;

    /// <summary>EXPERIENCE_MALUS_MULT.</summary>
    private const float ExperienceMalusMult = 0.08f;

    /// <summary>get_difficulty(): false — сложность обнулилась, рыба поймана сразу.</summary>
    private bool GetDifficulty(ImperialFishingChallenge challenge)
    {
        var source = _proto.Index(challenge.Source);
        var rod = GetRodInfo((challenge.Rod, Comp<ImperialFishingRodComponent>(challenge.Rod)));
        var instance = CompOrNull<ImperialFishComponent>(challenge.RewardEntity);
        var difficulty = _sources.CalculateDifficulty(source, challenge.RewardProto, instance, rod, challenge.User);

        if ((challenge.SpecialEffects & ImperialFishingRules.NoExp) == 0)
            difficulty += _skill.GetValueModifier(challenge.User);

        difficulty += GetEquipmentDifficultyMods(challenge.User);

        if (difficulty <= 0)
        {
            Complete(challenge, true);
            return false;
        }

        challenge.Difficulty = Math.Clamp(MathF.Round(difficulty), ImperialFishing.MinimumDifficulty, 100);
        return true;
    }

    /// <summary>fishing_difficulty_mods_by_source: сумки, шляпы и т. п.</summary>
    private float GetEquipmentDifficultyMods(EntityUid user)
    {
        var total = 0f;
        if (_inventory.TryGetContainerSlotEnumerator(user, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out var slot))
            {
                if (TryComp<ImperialFishingDifficultyModifierComponent>(item, out var mod) && (mod.Slots & slot.SlotFlags) != 0)
                    total += mod.Modifier;
            }
        }

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<ImperialFishingDifficultyModifierComponent>(held, out var mod) && mod.Hands)
                total += mod.Modifier;
        }

        // Модуль МОДа «рыболовная перчатка».
        if (TryComp<ImperialProfoundFisherUserComponent>(user, out var profound))
            total += profound.DifficultyModifier;

        return total;
    }

    /// <summary>start_minigame_phase().</summary>
    private void StartMinigame(ImperialFishingChallenge challenge, bool autoReel)
    {
        if (!GetDifficulty(challenge))
            return;

        var timeLeft = challenge.NextPhaseAt - _timing.CurTime;
        challenge.ApplyReactionBonus(timeLeft, autoReel);

        var fish = CompOrNull<ImperialFishComponent>(challenge.RewardEntity)
                   ?? (challenge.RewardProto != null ? _data.GetFish(challenge.RewardProto) : null);

        challenge.Mover = ImperialFishMovement.Create(fish?.Movement ?? ImperialFishMovementType.Default, challenge, _random);
        if (fish != null)
        {
            var traits = challenge.RewardEntity != null ? fish.Traits.Select(t => t.Id) : fish.InitialTraits.Select(t => t.Id);
            foreach (var trait in traits)
                _traits.MinigameMod(trait, challenge);
        }

        // /datum/fish_source/portal/random/pre_challenge_started(): всё немного случайно.
        if (_proto.Index(challenge.Source).Special == "portalRandom")
        {
            challenge.BaitBounceMult = MathF.Max(challenge.BaitBounceMult + _random.Next(-3, 4) * 0.1f, 0.1f);
            challenge.CompletionLoss = MathF.Max(challenge.CompletionLoss + _random.Next(-2, 3), 0);
            challenge.CompletionGain = MathF.Max(challenge.CompletionGain + _random.Next(-1, 2), 2);
            challenge.Mover.ShortJumpVelocityLimit += _random.Next(-100, 101) + _random.Next(-100, 101);
            challenge.Mover.LongJumpVelocityLimit += _random.Next(-100, 101) + _random.Next(-100, 101);
            foreach (var effect in new[] { ImperialFishingRules.Antigrav, ImperialFishingRules.Flip, ImperialFishingRules.Camo })
            {
                if (_random.Prob(0.3f))
                    challenge.SpecialEffects |= effect;
            }
        }

        challenge.ActiveEffects.Clear();
        foreach (var effect in new[] { ImperialFishingRules.Antigrav, ImperialFishingRules.Flip, ImperialFishingRules.Camo })
        {
            if ((challenge.SpecialEffects & effect) != 0)
                challenge.ActiveEffects.Add(effect);
        }

        var rod = Comp<ImperialFishingRodComponent>(challenge.Rod);
        challenge.BaitHeight = challenge.InitialBaitHeight * rod.BaitHeightMult;
        challenge.FishPosition = _random.Next(0, (int) ((ImperialFishing.MinigameArea - challenge.FishHeight) * 0.8f) + 1);
        var diffDist = 100 + (int) challenge.Difficulty;
        challenge.BaitPosition = Math.Clamp(MathF.Round(challenge.FishPosition + _random.Next(-diffDist, diffDist + 1) - challenge.BaitHeight * 0.5f),
            0, ImperialFishing.MinigameArea - challenge.BaitHeight);

        if (!_ui.TryOpenUi(challenge.Rod, ImperialFishingUiKey.Key, challenge.User))
        {
            Complete(challenge, false);
            return;
        }

        challenge.AdjustToDifficulty();
        challenge.Phase = ImperialFishingPhase.Minigame;
        challenge.StartTime = _timing.CurTime;
        challenge.AutoStartAt = null;
        if (challenge.ActiveEffects.Count > 0)
            challenge.NextActiveEffect = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(5, 10));

        if ((challenge.SpecialEffects & ImperialFishingRules.Kill) != 0 && fish != null && challenge.RewardEntity == null)
        {
            var wait = fish.MaxIntegrity * (1 - fish.IntegrityFailure) / KillHookDamagePerSecond;
            challenge.KillWinAt = _timing.CurTime + TimeSpan.FromSeconds(wait);
        }

        SetFloat(challenge, true, true);
        SendState(challenge);
    }

    private void OnReelMessage(Entity<ImperialFishingRodComponent> ent, ref ImperialFishingReelMessage args)
    {
        if (!_challenges.TryGetValue(args.Actor, out var challenge) || challenge.Rod != ent.Owner || challenge.Phase != ImperialFishingPhase.Minigame)
            return;

        challenge.Reeling = args.State switch
        {
            1 => ImperialReelingState.Up,
            2 when challenge.Bidirectional => ImperialReelingState.Down,
            2 => ImperialReelingState.Up,
            _ => ImperialReelingState.Idle,
        };
    }

    private void OnGiveUpMessage(Entity<ImperialFishingRodComponent> ent, ref ImperialFishingGiveUpMessage args)
    {
        if (_challenges.TryGetValue(args.Actor, out var challenge) && challenge.Rod == ent.Owner)
            Interrupt(challenge);
    }

    private void SendState(ImperialFishingChallenge challenge)
    {
        var rod = Comp<ImperialFishingRodComponent>(challenge.Rod);
        var state = new ImperialFishingMinigameState
        {
            BaitPosition = challenge.BaitPosition,
            BaitHeight = challenge.BaitHeight,
            BaitPixelHeight = challenge.BaitPixelHeight,
            FishPosition = challenge.FishPosition,
            Completion = challenge.Completion,
            Background = challenge.CurrentActiveEffect switch
            {
                ImperialFishingRules.Antigrav => "background_antigrav",
                ImperialFishingRules.Flip => "background_flip",
                ImperialFishingRules.Camo => "background_camo",
                _ => challenge.Background,
            },
            Frame = rod.FrameState,
            Icon = challenge.FishIcon,
            Flip = challenge.CurrentActiveEffect == ImperialFishingRules.Flip,
            Camo = challenge.CurrentActiveEffect == ImperialFishingRules.Camo,
            Antigrav = challenge.CurrentActiveEffect == ImperialFishingRules.Antigrav,
            Bidirectional = challenge.Bidirectional,
        };
        _ui.SetUiState(challenge.Rod, ImperialFishingUiKey.Key, state);
    }

    /// <summary>select_active_effect(): антигравитация, переворот, маскировка.</summary>
    private void SelectActiveEffect(ImperialFishingChallenge challenge)
    {
        if (challenge.CurrentActiveEffect == null)
        {
            var effect = _random.Pick(challenge.ActiveEffects);
            challenge.CurrentActiveEffect = effect;
            switch (effect)
            {
                case ImperialFishingRules.Antigrav:
                    _audio.PlayEntity("/Audio/Imperial/Fishing/arcade_jump.ogg", Filter.Entities(challenge.User), challenge.User, false);
                    challenge.NextActiveEffect = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(6, 10));
                    break;
                case ImperialFishingRules.Flip:
                    _audio.PlayEntity("/Audio/Items/bikehorn.ogg", Filter.Entities(challenge.User), challenge.User, false);
                    challenge.NextActiveEffect = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(5, 7));
                    break;
                case ImperialFishingRules.Camo:
                    _audio.PlayEntity("/Audio/Effects/teleport_departure.ogg", Filter.Entities(challenge.User), challenge.User, false, AudioParams.Default.WithVolume(-8));
                    challenge.NextActiveEffect = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(6, 9));
                    break;
            }

            return;
        }

        challenge.NextActiveEffect = challenge.CurrentActiveEffect switch
        {
            ImperialFishingRules.Antigrav => _timing.CurTime + TimeSpan.FromSeconds(_random.Next(10, 14)),
            ImperialFishingRules.Flip => _timing.CurTime + TimeSpan.FromSeconds(_random.Next(8, 13)),
            _ => _timing.CurTime + TimeSpan.FromSeconds(_random.Next(9, 17)),
        };
        challenge.CurrentActiveEffect = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var challenge in _challenges.Values.ToList())
        {
            if (challenge.Completed)
                continue;

            if (!ValidateChallenge(challenge))
                continue;

            switch (challenge.Phase)
            {
                case ImperialFishingPhase.Wait:
                    UpdateWait(challenge);
                    break;
                case ImperialFishingPhase.Biting:
                    if (challenge.AutoStartAt is { } autoStart && _timing.CurTime >= autoStart)
                    {
                        challenge.AutoStartAt = null;
                        StartMinigame(challenge, true);
                    }
                    else if (_timing.CurTime >= challenge.NextPhaseAt)
                    {
                        StartBaitingPhase(challenge, false);
                    }

                    break;
                case ImperialFishingPhase.Minigame:
                    UpdateMinigame(challenge, frameTime);
                    break;
            }
        }

        UpdateHooked();
    }

    /// <summary>Рыбак, удочка и место ловли на месте, леска не порвана.</summary>
    private bool ValidateChallenge(ImperialFishingChallenge challenge)
    {
        if (TerminatingOrDeleted(challenge.User) || TerminatingOrDeleted(challenge.Rod) || TerminatingOrDeleted(challenge.Location)
            || _mobState.IsIncapacitated(challenge.User))
        {
            Interrupt(challenge);
            return false;
        }

        if (!_hands.IsHolding(challenge.User, challenge.Rod) && CompOrNull<ImperialProfoundFisherUserComponent>(challenge.User)?.Rod != challenge.Rod)
        {
            Interrupt(challenge);
            return false;
        }

        var rod = GetRodInfo((challenge.Rod, Comp<ImperialFishingRodComponent>(challenge.Rod)));
        var range = GetCastRange(rod, challenge.User) + 1.5f;
        if (!_transform.InRange(Transform(challenge.Location).Coordinates, Transform(challenge.User).Coordinates, range))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-line-snapped"), challenge.User, challenge.User);
            Interrupt(challenge);
            return false;
        }

        if (challenge.RewardEntity is { } reward && (TerminatingOrDeleted(reward) || _container.TryGetContainingContainer(reward, out var container) && container.Owner != challenge.Location))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-reward-gone"), challenge.User, challenge.User);
            Interrupt(challenge);
            return false;
        }

        return true;
    }

    private void UpdateWait(ImperialFishingChallenge challenge)
    {
        if (challenge.SpinFrequency != null)
        {
            if (!challenge.SpinReady && challenge.SpinReadyAt is { } readyAt && _timing.CurTime >= readyAt)
            {
                challenge.SpinReadyAt = null;
                challenge.SpinReady = true;
                SetFloat(challenge, false, true);
                _audio.PlayPvs(PingSound, challenge.Float, AudioParams.Default.WithVolume(-6));
                if ((challenge.SpecialEffects & ImperialFishingRules.Autoreel) != 0)
                    challenge.AutoSpinAt = _timing.CurTime + TimeSpan.FromSeconds(0.2);
            }

            if (challenge.AutoSpinAt is { } autoSpin && _timing.CurTime >= autoSpin)
            {
                challenge.AutoSpinAt = null;
                if (challenge.SpinReady)
                {
                    Alert(challenge, "imperial-fishing-spun");
                    SetLureTimers(challenge);
                }
            }

            if (challenge.SpinMissAt is { } missAt && _timing.CurTime >= missAt)
            {
                challenge.SpinMissAt = null;
                Alert(challenge, "imperial-fishing-miss");
                StartBaitingPhase(challenge, true);
                return;
            }
        }

        if (_timing.CurTime >= challenge.NextPhaseAt)
            StartBitingPhase(challenge);
    }

    private void UpdateMinigame(ImperialFishingChallenge challenge, float frameTime)
    {
        if (challenge.KillWinAt is { } killAt && _timing.CurTime >= killAt)
        {
            challenge.ExperienceMultiplier *= 0.5f;
            Complete(challenge, true);
            return;
        }

        challenge.Accumulator += frameTime;
        var changed = false;
        while (challenge.Accumulator >= ImperialFishing.MinigameTick && !challenge.Completed)
        {
            challenge.Accumulator -= ImperialFishing.MinigameTick;
            changed = true;

            if (challenge.ActiveEffects.Count > 0 && _timing.CurTime >= challenge.NextActiveEffect)
                SelectActiveEffect(challenge);

            challenge.Mover?.MoveFish(ImperialFishing.MinigameTick);
            var result = challenge.MoveBait(ImperialFishing.MinigameTick);
            if (result == true)
            {
                Complete(challenge, true);
                return;
            }

            if (result == false)
            {
                _popup.PopupEntity(Loc.GetString("imperial-fishing-got-away"), challenge.User, challenge.User);
                Complete(challenge, false);
                return;
            }
        }

        if (changed)
            SendState(challenge);
    }

    /// <summary>complete(): опыт, награда, очистка.</summary>
    private void Complete(ImperialFishingChallenge challenge, bool win)
    {
        if (challenge.Completed)
            return;

        challenge.Completed = true;
        _challenges.Remove(challenge.User);

        if (challenge.Phase == ImperialFishingPhase.Minigame)
            _ui.CloseUi(challenge.Rod, ImperialFishingUiKey.Key, challenge.User);

        if (!TerminatingOrDeleted(challenge.Float))
            QueueDel(challenge.Float);

        if (!TerminatingOrDeleted(challenge.User) && challenge.Phase == ImperialFishingPhase.Minigame
            && (challenge.SpecialEffects & ImperialFishingRules.NoExp) == 0)
        {
            var seconds = (float) (_timing.CurTime - challenge.StartTime).TotalSeconds;
            var malus = _skill.GetLevel(challenge.User) - challenge.Difficulty * 0.1f;
            if (malus > 0)
                challenge.ExperienceMultiplier /= 1 + malus * ExperienceMalusMult;

            if (TryComp<ImperialFishingRodComponent>(challenge.Rod, out var rodComp))
                challenge.ExperienceMultiplier *= rodComp.ExperienceMultiplier;

            _skill.AddExperience(challenge.User, MathF.Round(seconds * ImperialFishing.SkillExpPerSecond * challenge.ExperienceMultiplier));
        }

        var ev = new ImperialFishingCompletedEvent(challenge, win);
        RaiseLocalEvent(challenge.Location, ref ev);

        if (!win || ev.Handled || TerminatingOrDeleted(challenge.User))
            return;

        if (challenge.RewardProto != ImperialFishing.Dud || challenge.RewardEntity != null)
            _audio.PlayPvs(BigSplashSound, challenge.Location);

        var source = _proto.Index(challenge.Source);
        EntityUid? reward;
        if (challenge.RewardEntity is { } entity && !TerminatingOrDeleted(entity))
        {
            _container.TryRemoveFromContainer(entity, true);
            _transform.SetCoordinates(entity, Transform(challenge.User).Coordinates);
            _hands.PickupOrDrop(challenge.User, entity);
            _popup.PopupEntity(Loc.GetString("imperial-fishing-caught", ("reward", entity)), challenge.User, challenge.User);
            reward = entity;
        }
        else
        {
            reward = _sources.DispenseReward(source, challenge.RewardProto ?? ImperialFishing.Dud, challenge.User);
        }

        if (reward is { } caught && TryComp<ImperialFishComponent>(caught, out var fish))
        {
            if ((challenge.SpecialEffects & ImperialFishingRules.Kill) != 0)
            {
                var damage = MathF.Ceiling((float) (_timing.CurTime - challenge.StartTime).TotalSeconds * KillHookDamagePerSecond);
                _fish.DamageFish((caught, fish), damage);
            }

            if (fish.JustSpawned)
            {
                fish.CatcherName = Name(challenge.User);
                fish.CatchDate = DateTime.Now.ToString("dd.MM.yyyy");
                fish.JustSpawned = false;
                Dirty(caught, fish);
            }
        }

        if (TryComp<ImperialFishingRodComponent>(challenge.Rod, out var rodComponent))
            OnRewardCaught(GetRodInfo((challenge.Rod, rodComponent)), reward);

        var caughtEv = new ImperialFishingCaughtEvent(challenge.User, reward);
        RaiseLocalEvent(challenge.Rod, ref caughtEv);
    }

    /// <summary>on_reward_caught(): наживка съедена (если это рыба или живое существо).</summary>
    private void OnRewardCaught(ImperialFishingRodInfo rod, EntityUid? reward)
    {
        if (reward == null || rod.Bait is not { } bait || CompOrNull<ImperialFishingBaitComponent>(bait)?.Unconsumable == true)
            return;

        if (TryComp<MobStateComponent>(reward, out var mob))
        {
            if (_mobState.IsDead(reward.Value, mob))
                return;
        }
        else if (!TryComp<ImperialFishComponent>(reward, out var fish))
        {
            return;
        }
        else if (CompOrNull<ImperialFishingBaitComponent>(bait)?.Poisonous == true && !_traits.Has(fish, "ToxinImmunity")
                 && !fish.FavoriteBait.Any(id => _bait.Matches(bait, id)))
        {
            _fish.SetDead((reward.Value, fish), true, silent: true);
        }

        QueueDel(bait);
    }

    /// <summary>Леска к зацепленному предмету рвётся, если он ушёл слишком далеко.</summary>
    private void UpdateHooked()
    {
        var query = EntityQueryEnumerator<ImperialFishingRodComponent>();
        while (query.MoveNext(out var uid, out var rod))
        {
            if (rod.Hooked is not { } hooked)
                continue;

            if (TerminatingOrDeleted(hooked) || _container.IsEntityInContainer(hooked))
            {
                ClearHooked((uid, rod));
                continue;
            }

            var holder = Transform(uid).ParentUid;
            var info = GetRodInfo((uid, rod));
            if (!_transform.InRange(Transform(hooked).Coordinates, Transform(uid).Coordinates, GetCastRange(info, holder) + 1.5f))
                ClearHooked((uid, rod));
        }
    }

    public bool IsFishing(EntityUid user) => _challenges.ContainsKey(user);

    public ImperialFishingChallenge? GetChallenge(EntityUid user) => _challenges.GetValueOrDefault(user);
}

/// <summary>Рыбалка закончилась (COMSIG_MOB_COMPLETE_FISHING). Источник с особой логикой может выдать награду сам.</summary>
[ByRefEvent]
public record struct ImperialFishingCompletedEvent(ImperialFishingChallenge Challenge, bool Win, bool Handled = false);

/// <summary>Поймано (COMSIG_FISHING_ROD_CAUGHT_FISH), поднимается на удочке.</summary>
[ByRefEvent]
public readonly record struct ImperialFishingCaughtEvent(EntityUid User, EntityUid? Reward);
