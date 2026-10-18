using Robust.Shared.Random;
using System.Linq;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Pointing;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeSystem
{
    [Dependency] private readonly StandingStateSystem _standing = default!;

    /// <summary>find_slime_food action_cooldown.</summary>
    private static readonly TimeSpan HuntCooldown = TimeSpan.FromSeconds(7.5);

    /// <summary>Тик ИИ (planning) раз в секунду.</summary>
    private static readonly TimeSpan AiInterval = TimeSpan.FromSeconds(1);

    private const float HuntRange = 7f;

    private static readonly string[] StayCommands = { "sit", "stay", "stop", "сидеть", "лежать", "место", "фу", "стоп", "стой", "стоять" };
    private static readonly string[] FreeCommands = { "free", "loose", "гулять", "вон", "свобод", "брысь" };
    private static readonly string[] FollowCommands = { "heel", "follow", "за мной", "след", "охран", "к ноге", "ко мне" };
    private static readonly string[] AttackCommands = { "attack", "sic", "kill", "eat", "feed", "атакуй", "фас", "убей", "ешь", "съешь", "корм" };
    private static readonly string[] RandomSpeech = { "slime-speech-1", "slime-speech-2" };
    private static readonly string[] RandomEmotes = { "slime-emote-blorbles", "slime-emote-light", "slime-emote-bounce", "slime-emote-jiggle", "slime-emote-vibrate" };

    private void InitializeAi()
    {
        SubscribeLocalEvent<SlimeComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<SlimeCommanderComponent, AfterPointedAtEvent>(OnCommanderPointed);
    }

    #region Друзья

    /// <summary>befriend(): друг может командовать слаймом, слайм его не ест.</summary>
    public void Befriend(Entity<SlimeComponent> ent, EntityUid friend)
    {
        var brain = EnsureComp<SlimeBrainComponent>(ent);
        if (!brain.Friends.Add(friend))
            return;
        if (brain.Target == friend)
            brain.Target = null;
        if (brain.HuntTarget == friend)
            brain.HuntTarget = null;
        EnsureComp<SlimeCommanderComponent>(friend).Slimes.Add(ent);
    }

    /// <summary>unfriend().</summary>
    public void Unfriend(Entity<SlimeComponent> ent, SlimeBrainComponent brain, EntityUid friend)
    {
        brain.Friends.Remove(friend);
        if (brain.FollowTarget == friend)
        {
            brain.FollowTarget = null;
            brain.PetMode = SlimePetMode.Free;
        }

        if (TryComp<SlimeCommanderComponent>(friend, out var commander))
            commander.Slimes.Remove(ent);
    }

    /// <summary>ai_retaliate: напавший становится целью.</summary>
    private void Retaliate(Entity<SlimeComponent> ent, SlimeBrainComponent brain, EntityUid attacker)
    {
        if (attacker == ent.Owner || brain.Friends.Contains(attacker) || HasComp<SlimeComponent>(attacker))
            return;
        brain.Target = attacker;
    }

    #endregion

    #region Охота

    private void UpdateAi(Entity<SlimeComponent> ent, SlimeBrainComponent brain, TimeSpan now)
    {
        if (now < brain.NextAiTick)
            return;
        brain.NextAiTick = now + AiInterval;

        if (_mobState.IsIncapacitated(ent) || brain.BzStasis || brain.ConsoleStasis)
            return;

        var player = HasComp<ActorComponent>(ent);

        // change_slime_face: 5% в секунду.
        if (!player && _random.Prob(0.05f))
            ChangeFace(ent, brain);

        if (player)
            return;

        // use_mob_ability/evolve и reproduce.
        if (ent.Comp.Stage == SlimeLifeStage.Baby)
            TryEvolve(ent, feedback: false);
        else
            TryReproduce(ent, feedback: false);

        if (TerminatingOrDeleted(ent))
            return;

        // random_speech: 1% в секунду.
        if (_random.Prob(0.01f))
            RandomChatter(ent);

        if (brain.FeedingOn != null)
            return;

        // Устаревшие цели.
        if (brain.Target is { } target && (TerminatingOrDeleted(target) || _mobState.IsDead(target) || brain.Friends.Contains(target)))
            brain.Target = null;
        if (brain.HuntTarget is { } hunt && !CanFeedOn(ent, hunt))
            brain.HuntTarget = null;

        // find_and_hunt_target/find_slime_food
        if (brain.HuntTarget == null && now >= brain.NextHuntSearch &&
            (brain.Hunger != SlimeHunger.None || ent.Comp.Rabid || brain.Target != null))
        {
            brain.NextHuntSearch = now + HuntCooldown;
            brain.HuntTarget = FindFood(ent, brain);
        }

        ApplyBlackboard(ent, brain);
    }

    /// <summary>valid_dinner(): кого слайм готов съесть.</summary>
    private EntityUid? FindFood(Entity<SlimeComponent> ent, SlimeBrainComponent brain)
    {
        var coords = _transform.GetMoverCoordinates(ent);
        foreach (var (mob, _) in _lookup.GetEntitiesInRange<MobStateComponent>(coords, HuntRange))
        {
            if (brain.Friends.Contains(mob) || HasComp<SlimeComponent>(mob) || !CanFeedOn(ent, mob))
                continue;

            // Ешь того, на кого злишься.
            if (mob == brain.Target)
                return mob;

            // Голодный и бешеный ест кого угодно.
            if (brain.Hunger == SlimeHunger.Starving && ent.Comp.Rabid)
                return mob;

            // Обезьяны и люди.
            if (HasComp<HumanoidProfileComponent>(mob) || HasComp<SlimeFoodComponent>(mob) || IsMonkey(mob))
                return mob;
        }

        return null;
    }

    private bool IsMonkey(EntityUid uid)
    {
        var proto = MetaData(uid).EntityPrototype?.ID;
        return proto != null && proto.Contains("Monkey");
    }

    /// <summary>Передаёт цель или режим в HTN.</summary>
    private void ApplyBlackboard(Entity<SlimeComponent> ent, SlimeBrainComponent brain)
    {
        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        var blackboard = htn.Blackboard;
        var target = brain.HuntTarget ?? brain.Target;
        if (brain.PetMode == SlimePetMode.Stay && brain.Target == null)
            target = null;

        if (target is { } t && !TerminatingOrDeleted(t))
        {
            blackboard.SetValue("Target", t);
            blackboard.SetValue("SlimeTargetCoordinates", _transform.GetMoverCoordinates(t));
            blackboard.SetValue("SlimeHunting", true);
        }
        else
        {
            // Ключи целей не удаляются: HTN читает их при запуске уже спланированных задач.
            blackboard.Remove<bool>("SlimeHunting");
        }

        if (brain.PetMode == SlimePetMode.Follow && brain.FollowTarget is { } follow && !TerminatingOrDeleted(follow))
        {
            blackboard.SetValue("FollowTarget", _transform.GetMoverCoordinates(follow));
            blackboard.SetValue("SlimeFollowing", true);
        }
        else
        {
            blackboard.Remove<bool>("SlimeFollowing");
        }

        if (brain.PetMode == SlimePetMode.Stay)
            blackboard.SetValue("SlimeStay", true);
        else
            blackboard.Remove<bool>("SlimeStay");
    }

    /// <summary>target_caught(): удар или присасывание при касании цели.</summary>
    private void OnTargetCaught(Entity<SlimeComponent> ent, SlimeBrainComponent brain, EntityUid hunted)
    {
        if (HasComp<ActorComponent>(ent))
            return;
        if (hunted != brain.HuntTarget && hunted != brain.Target)
            return;
        if (!CanFeedOn(ent, hunted))
            return;

        // Лежит или повезло (20%) — кормимся.
        if (_standing.IsDown(hunted) || _random.Prob(0.2f))
        {
            StartFeeding(ent, hunted);
            return;
        }

        // Живой здоровый игрок — сперва немного побить.
        if (HasComp<ActorComponent>(hunted) && OrganicHealth(hunted) >= 20f)
            return;

        StartFeeding(ent, hunted);
    }

    #endregion

    #region Лицо и речь

    /// <summary>perform_change_slime_face.</summary>
    private void ChangeFace(Entity<SlimeComponent> ent, SlimeBrainComponent brain)
    {
        SlimeMood mood;
        if (ent.Comp.Rabid || brain.Target != null)
            mood = SlimeMood.Angry;
        else if (ent.Comp.HungerDisabled)
            mood = SlimeMood.Smile;
        else if (brain.HuntTarget != null)
            mood = SlimeMood.Mischievous;
        else
            mood = _random.Pick(new[] { SlimeMood.Sad, SlimeMood.Smile, SlimeMood.Pout });

        if (ent.Comp.Mood == mood)
            return;
        ent.Comp.Mood = mood;
        Dirty(ent);
    }

    /// <summary>random_speech/slime.</summary>
    private void RandomChatter(Entity<SlimeComponent> ent)
    {
        if (_random.Prob(0.5f))
            _chat.TrySendInGameICMessage(ent, Loc.GetString(_random.Pick(RandomSpeech)), InGameICChatType.Speak, hideChat: false);
        else
            _chat.TrySendInGameICMessage(ent, Loc.GetString(_random.Pick(RandomEmotes)), InGameICChatType.Emote, hideChat: false);
    }

    #endregion

    #region Команды питомца

    /// <summary>obeys_commands: друг командует голосом.</summary>
    private void OnListen(Entity<SlimeComponent> ent, ref ListenEvent args)
    {
        if (!TryComp<SlimeBrainComponent>(ent, out var brain) || !brain.Friends.Contains(args.Source) ||
            _mobState.IsIncapacitated(ent) || HasComp<ActorComponent>(ent))
            return;

        var text = args.Message.ToLowerInvariant();
        if (StayCommands.Any(text.Contains))
        {
            brain.PetMode = SlimePetMode.Stay;
            brain.HuntTarget = null;
            brain.Target = null;
            Respond(ent, "slime-command-stay");
        }
        else if (FreeCommands.Any(text.Contains))
        {
            brain.PetMode = SlimePetMode.Free;
            brain.FollowTarget = null;
            Respond(ent, "slime-command-free");
        }
        else if (FollowCommands.Any(text.Contains))
        {
            brain.PetMode = SlimePetMode.Follow;
            brain.FollowTarget = args.Source;
            Respond(ent, "slime-command-follow");
        }
        else if (AttackCommands.Any(text.Contains))
        {
            brain.AttackCommander = args.Source;
            brain.AttackCommandUntil = _timing.CurTime + TimeSpan.FromSeconds(10);
            Respond(ent, "slime-command-attack");
        }
        else
        {
            return;
        }

        ApplyBlackboard(ent, brain);
    }

    /// <summary>pet_command/attack/slime: друг указывает цель, слайм её ест.</summary>
    private void OnCommanderPointed(Entity<SlimeCommanderComponent> ent, ref AfterPointedAtEvent args)
    {
        foreach (var slimeUid in ent.Comp.Slimes.ToList())
        {
            if (!TryComp<SlimeComponent>(slimeUid, out var slime) || !TryComp<SlimeBrainComponent>(slimeUid, out var brain))
            {
                ent.Comp.Slimes.Remove(slimeUid);
                continue;
            }

            if (brain.AttackCommander != ent.Owner || _timing.CurTime > brain.AttackCommandUntil)
                continue;

            brain.AttackCommander = null;
            if (!CanFeedOn((slimeUid, slime), args.Pointed, checkFriendship: true))
            {
                Respond((slimeUid, slime), "slime-command-refuse");
                continue;
            }

            brain.PetMode = SlimePetMode.Attack;
            brain.HuntTarget = args.Pointed;
            ApplyBlackboard((slimeUid, slime), brain);
        }
    }

    private void Respond(Entity<SlimeComponent> ent, string key)
    {
        _chat.TrySendInGameICMessage(ent, Loc.GetString(key), InGameICChatType.Emote, hideChat: false);
    }

    #endregion
}
