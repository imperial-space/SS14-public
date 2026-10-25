using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Mood;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Drunk;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Mood;

/// <summary>Активный мудлет.</summary>
public sealed class ImperialMoodActive
{
    public ProtoId<ImperialMoodEventPrototype> Event;
    public int Change;
    public string Description = string.Empty;
    public bool Hidden;
    public float Timeout;
    public TimeSpan? Expires;
    public int Priority;
}

/// <summary>/datum/mood: мудлеты, настроение и рассудок существа.</summary>
[RegisterComponent]
public sealed partial class ImperialMoodComponent : Component
{
    public Dictionary<string, ImperialMoodActive> Events = new();

    /// <summary>mood: сумма мудлетов.</summary>
    public int Mood;

    /// <summary>shown_mood: сумма видимых мудлетов.</summary>
    public int ShownMood;

    /// <summary>mood_level 1–9.</summary>
    public int MoodLevel = ImperialMood.LevelNeutral;

    /// <summary>sanity 0–150.</summary>
    [DataField]
    public float Sanity = ImperialMood.SanityNeutral;

    /// <summary>sanity_level 1–6.</summary>
    public int SanityLevel = 2;

    /// <summary>insanity_effect: на сколько раньше наступает критическое состояние.</summary>
    public int InsanityEffect;

    [DataField]
    public float MoodModifier = 1f;

    [DataField]
    public float PositiveModifier = 1f;

    [DataField]
    public float NegativeModifier = 1f;

    [DataField]
    public float PositiveLengthModifier = 1f;

    [DataField]
    public float NegativeLengthModifier = 1f;

    public bool Hallucinating;
    public TimeSpan NextTick;

    /// <summary>Каждые 5 секунд: окружение, личности, роли.</summary>
    public TimeSpan NextSlowTick;

    /// <summary>personalities: savefile_key личностей.</summary>
    public HashSet<string> Personalities = new();

    /// <summary>Стадия опьянения, по которой выставлен мудлет drunk.</summary>
    public int DrunkStage;
}

public static class ImperialMood
{
    public const int Happy4 = 15, Happy3 = 10, Happy2 = 6, Happy1 = 2, Sad1 = -3, Sad2 = -7, Sad3 = -15, Sad4 = -20;
    public const int LevelNeutral = 5;
    public const float SanityMaximum = 150, SanityGreat = 125, SanityNeutral = 100, SanityDisturbed = 75, SanityUnstable = 50, SanityCrazy = 25, SanityInsane = 0;

    /// <summary>RUN_DELAY TG: замедления рассудка прибавляются к нему.</summary>
    public const float RunDelay = 2.2f;
}

/// <summary>/datum/mood TG: мудлеты складываются в настроение, настроение двигает рассудок, низкий рассудок замедляет и пугает.</summary>
public sealed partial class ImperialMoodSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private static readonly ProtoId<AlertPrototype> MoodAlert = "ImperialMood";
    private static readonly EntProtoId Hallucinations = "ImperialMoodHallucinations";

    private const int MinorInsanityPen = 5;
    private const int MajorInsanityPen = 10;

    /// <summary>Порядок значков в ImperialMood: mood1–9 и особые.</summary>
    private static readonly string[] SpecialIcons = { "mood_happiness_good", "mood_happiness_bad", "mood_despair" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialMoodComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ImperialMoodComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ImperialMoodComponent, ImperialMoodAddEvent>(OnAddEvent);
        SubscribeLocalEvent<ImperialMoodComponent, ImperialMoodClearEvent>(OnClearEvent);
        SubscribeLocalEvent<ImperialMoodComponent, ImperialMoodAlertEvent>(OnAlertClick);
        SubscribeLocalEvent<ImperialMoodComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ImperialMoodComponent, MobStateChangedEvent>(OnOwnState);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        InitializeTriggers();
        InitializePersonality();
        InitializeQuirks();
    }

    /// <summary>mob_mood есть у гуманоидов с игроком.</summary>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (HasComp<HumanoidProfileComponent>(args.Entity))
            EnsureComp<ImperialMoodComponent>(args.Entity);
    }

    private void OnStartup(Entity<ImperialMoodComponent> ent, ref ComponentStartup args)
    {
        UpdateMood(ent);
        SetSanity(ent, ent.Comp.Sanity, force: true);
    }

    private void OnShutdown(Entity<ImperialMoodComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, MoodAlert);
        SetInsanityEffect(ent, 0);
        SetHallucinations(ent, false);
    }

    private void OnAddEvent(Entity<ImperialMoodComponent> ent, ref ImperialMoodAddEvent args)
    {
        AddMood(ent.AsNullable(), args.Category, args.Event, args.Source);
    }

    private void OnClearEvent(Entity<ImperialMoodComponent> ent, ref ImperialMoodClearEvent args)
    {
        ClearMood(ent.AsNullable(), args.Category);
    }

    /// <summary>on_revive / смерть: мёртвые не грустят, а оживлённые теряют временные мудлеты.</summary>
    private void OnOwnState(Entity<ImperialMoodComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
        {
            foreach (var (category, active) in ent.Comp.Events.ToArray())
            {
                if (active.Timeout > 0)
                    ent.Comp.Events.Remove(category);
            }

            UpdateMood(ent);
        }
    }

    #region Мудлеты

    /// <summary>add_mood_event: тот же мудлет обновляет таймер, другой мудлет той же категории заменяет старый.</summary>
    public void AddMood(Entity<ImperialMoodComponent?> ent, string category, ProtoId<ImperialMoodEventPrototype> id,
        EntityUid? source = null, int? change = null, string? description = null, int priority = 0, float? timeout = null)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !_proto.TryIndex(id, out var proto))
            return;

        var comp = ent.Comp;
        var now = _timing.CurTime;
        var moodChange = change ?? proto.MoodChange;

        // can_effect_mob и on_add: личность меняет силу и текст мудлета или отменяет его.
        if (!ApplyPersonality((ent.Owner, comp), proto, source, ref moodChange, ref description))
            return;

        if (comp.Events.TryGetValue(category, out var existing))
        {
            // be_refreshed: тот же мудлет — только продлеваем.
            if (existing.Event == id)
            {
                if (existing.Timeout > 0)
                    existing.Expires = now + TimeSpan.FromSeconds(existing.Timeout);
                return;
            }

            // conditional be_replaced: более важный мудлет не уступает менее важному.
            if (existing.Priority > priority)
                return;
        }

        var length = (timeout ?? proto.Timeout) * Math.Max(moodChange > 0 ? comp.PositiveLengthModifier : comp.NegativeLengthModifier, 0.1f);
        var text = description ?? (Loc.TryGetString(proto.DescriptionLoc, out var loc, ("target", source is { } s ? Name(s) : string.Empty)) ? loc : string.Empty);

        comp.Events[category] = new ImperialMoodActive
        {
            Event = id,
            Change = moodChange,
            Description = text,
            Hidden = proto.Hidden,
            Timeout = length,
            Expires = length > 0 ? now + TimeSpan.FromSeconds(length) : null,
            Priority = priority,
        };

        UpdateMood((ent.Owner, comp));

        if (moodChange != 0 && !proto.Hidden)
            SpreadToViewers(ent.Owner, proto.ID, moodChange > 0);
    }

    /// <summary>clear_mood_event.</summary>
    public void ClearMood(Entity<ImperialMoodComponent?> ent, string category)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !ent.Comp.Events.Remove(category))
            return;

        UpdateMood((ent.Owner, ent.Comp));
    }

    public bool HasMood(Entity<ImperialMoodComponent?> ent, string category)
    {
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Events.ContainsKey(category);
    }

    public ImperialMoodActive? GetMood(Entity<ImperialMoodComponent?> ent, string category)
    {
        return Resolve(ent, ref ent.Comp, false) ? ent.Comp.Events.GetValueOrDefault(category) : null;
    }

    /// <summary>update_mood.</summary>
    public void UpdateMood(Entity<ImperialMoodComponent> ent)
    {
        var comp = ent.Comp;
        float mood = 0, shown = 0;
        foreach (var active in comp.Events.Values)
        {
            var value = active.Change * Math.Max(active.Change > 0 ? comp.PositiveModifier : comp.NegativeModifier, 0);
            mood += value;
            if (!active.Hidden)
                shown += value;
        }

        comp.Mood = (int) (mood * Math.Max(comp.MoodModifier, 0));
        comp.ShownMood = (int) (shown * Math.Max(comp.MoodModifier, 0));
        comp.MoodLevel = comp.Mood switch
        {
            <= ImperialMood.Sad4 => 1,
            <= ImperialMood.Sad3 => 2,
            <= ImperialMood.Sad2 => 3,
            <= ImperialMood.Sad1 => 4,
            < ImperialMood.Happy1 => 5,
            < ImperialMood.Happy2 => 6,
            < ImperialMood.Happy3 => 7,
            < ImperialMood.Happy4 => 8,
            _ => 9,
        };

        UpdateIcon(ent);
    }

    /// <summary>update_mood_icon: рожица по настроению или особый значок, цвет по рассудку.</summary>
    private void UpdateIcon(Entity<ImperialMoodComponent> ent)
    {
        var icon = ent.Comp.MoodLevel;
        var strongest = -1;
        foreach (var active in ent.Comp.Events.Values)
        {
            if (!_proto.TryIndex(active.Event, out var proto) || proto.Icon == null)
                continue;

            var index = Array.IndexOf(SpecialIcons, proto.Icon);
            if (index < 0 || Math.Abs(active.Change) <= strongest)
                continue;

            strongest = Math.Abs(active.Change);
            icon = 10 + index;
        }

        _alerts.ShowAlert(ent.Owner, MoodAlert, (short) ((ent.Comp.SanityLevel - 1) * 12 + icon));
    }

    #endregion

    #region Рассудок

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialMoodComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextTick)
                continue;

            comp.NextTick = now + TimeSpan.FromSeconds(1);
            var ent = (uid, comp);

            var expired = false;
            foreach (var (category, active) in comp.Events.ToArray())
            {
                if (active.Expires is { } end && now >= end)
                {
                    comp.Events.Remove(category);
                    expired = true;
                }
            }

            if (expired)
                UpdateMood(ent);

            if (_mobState.IsDead(uid))
                continue;

            PollTriggers(ent);
            PollQuirks(ent);
            if (now >= comp.NextSlowTick)
            {
                comp.NextSlowTick = now + TimeSpan.FromSeconds(5);
                PollPersonalities(ent);
                PollAntag(ent);
            }

            ProcessSanity(ent, 1f);
        }
    }

    /// <summary>process: настроение медленно тянет рассудок к своему уровню.</summary>
    private void ProcessSanity(Entity<ImperialMoodComponent> ent, float seconds)
    {
        switch (ent.Comp.MoodLevel)
        {
            case 1: AdjustSanity(ent, -0.3f * seconds, ImperialMood.SanityInsane); break;
            case 2: AdjustSanity(ent, -0.15f * seconds, ImperialMood.SanityCrazy); break;
            case 3: AdjustSanity(ent, -0.1f * seconds, ImperialMood.SanityUnstable); break;
            case 4: AdjustSanity(ent, -0.05f * seconds, ImperialMood.SanityUnstable); break;
            case 5: AdjustSanity(ent, 0, ImperialMood.SanityUnstable); break;
            case 6: AdjustSanity(ent, 0.2f * seconds, ImperialMood.SanityUnstable); break;
            case 7: AdjustSanity(ent, 0.3f * seconds, ImperialMood.SanityUnstable); break;
            case 8: AdjustSanity(ent, 0.4f * seconds, ImperialMood.SanityNeutral, ImperialMood.SanityMaximum); break;
            default: AdjustSanity(ent, 0.6f * seconds, ImperialMood.SanityNeutral, ImperialMood.SanityMaximum); break;
        }
    }

    public void AdjustSanity(Entity<ImperialMoodComponent> ent, float amount, float minimum = ImperialMood.SanityInsane,
        float maximum = ImperialMood.SanityGreat)
    {
        SetSanity(ent, ent.Comp.Sanity + amount, minimum, maximum);
    }

    /// <summary>set_sanity: вне допустимых границ рассудок возвращается шагами по 0.7.</summary>
    public void SetSanity(Entity<ImperialMoodComponent> ent, float amount, float minimum = ImperialMood.SanityInsane,
        float maximum = ImperialMood.SanityGreat, bool force = false)
    {
        var comp = ent.Comp;
        if (amount < minimum && comp.Sanity < minimum)
            amount = comp.Sanity + 0.7f;

        // TRAIT_UNSTABLE: рассудок только падает.
        if (!force && HasComp<ImperialQuirkUnstableComponent>(ent))
            amount = Math.Min(comp.Sanity, amount);

        if (amount > maximum)
            amount = maximum;

        if (!force && Math.Abs(amount - comp.Sanity) < 0.0001f)
            return;

        comp.Sanity = amount;
        var level = amount switch
        {
            < ImperialMood.SanityCrazy => 6,
            < ImperialMood.SanityUnstable => 5,
            < ImperialMood.SanityDisturbed => 4,
            <= ImperialMood.SanityNeutral + 1 => 3,
            <= ImperialMood.SanityGreat + 1 => 2,
            _ => 1,
        };

        SetInsanityEffect(ent, level switch
        {
            6 => MajorInsanityPen,
            5 => MinorInsanityPen,
            _ => 0,
        });

        if (level != comp.SanityLevel || force)
        {
            comp.SanityLevel = level;
            _speed.RefreshMovementSpeedModifiers(ent);
            SetHallucinations(ent, level >= 5);
        }

        UpdateIcon(ent);
    }

    /// <summary>movespeed_modifier/sanity: insane +1, crazy +0.5, disturbed +0.25 к задержке шага TG.</summary>
    private void OnRefreshSpeed(Entity<ImperialMoodComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var slowdown = ent.Comp.SanityLevel switch
        {
            6 => 1f,
            5 => 0.5f,
            4 => 0.25f,
            _ => 0f,
        };

        if (slowdown <= 0)
            return;

        var modifier = ImperialMood.RunDelay / (ImperialMood.RunDelay + slowdown);
        args.ModifySpeed(modifier, modifier);
    }

    /// <summary>set_insanity_effect: crit_threshold сдвигается на величину штрафа.</summary>
    private void SetInsanityEffect(Entity<ImperialMoodComponent> ent, int value)
    {
        var old = ent.Comp.InsanityEffect;
        if (old == value)
            return;

        ent.Comp.InsanityEffect = value;
        if (!_thresholds.TryGetThresholdForState(ent, MobState.Critical, out var crit))
            return;

        _thresholds.SetMobStateThreshold(ent, crit.Value + old - value, MobState.Critical);
    }

    /// <summary>hallucination/sanity при сумасшествии: слуховые галлюцинации.</summary>
    private void SetHallucinations(Entity<ImperialMoodComponent> ent, bool enabled)
    {
        if (ent.Comp.Hallucinating == enabled)
            return;

        // Чужую паракузию (черта) не трогаем.
        if (enabled && HasComp<ParacusiaComponent>(ent))
            return;

        ent.Comp.Hallucinating = enabled;
        var proto = _proto.Index(Hallucinations);
        if (enabled)
            EntityManager.AddComponents(ent, proto, false);
        else if (!TerminatingOrDeleted(ent))
            EntityManager.RemoveComponents(ent, proto);
    }

    #endregion

    #region Окно настроения

    /// <summary>print_mood: голод, опьянение, рассудок, настроение и все мудлеты.</summary>
    private void OnAlertClick(Entity<ImperialMoodComponent> ent, ref ImperialMoodAlertEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor) || _mobState.IsIncapacitated(ent))
            return;

        args.Handled = true;
        var comp = ent.Comp;
        var msg = new StringBuilder();
        msg.AppendLine(Loc.GetString("imperial-mood-print-header"));

        if (TryComp<HungerComponent>(ent, out var hunger))
        {
            var key = _hunger.GetHungerThreshold(hunger) switch
            {
                HungerThreshold.Overfed => "overfed",
                HungerThreshold.Okay => "okay",
                HungerThreshold.Peckish => "peckish",
                HungerThreshold.Starving => "starving",
                _ => "dead",
            };
            msg.AppendLine(Loc.GetString("imperial-mood-print-hunger", ("state", Loc.GetString($"imperial-mood-print-hunger-{key}"))));
        }

        if (_status.TryGetMaxTime<DrunkStatusEffectComponent>(ent, out var drunk) && drunk.EndEffectTime is { } drunkEnd)
        {
            var drunkness = (drunkEnd - _timing.CurTime).TotalSeconds;
            var key = drunkness switch
            {
                < 11 => 1,
                < 21 => 2,
                < 41 => 3,
                < 61 => 4,
                < 81 => 5,
                _ => 6,
            };
            msg.AppendLine(Loc.GetString("imperial-mood-print-drunk", ("state", Loc.GetString($"imperial-mood-print-drunk-{key}"))));
        }

        var sanity = comp.Sanity switch
        {
            >= ImperialMood.SanityGreat => 1,
            >= ImperialMood.SanityNeutral => 2,
            >= ImperialMood.SanityDisturbed => 3,
            >= ImperialMood.SanityUnstable => 4,
            >= ImperialMood.SanityCrazy => 5,
            _ => 6,
        };
        msg.AppendLine(Loc.GetString("imperial-mood-print-sanity", ("state", Loc.GetString($"imperial-mood-print-sanity-{sanity}"))));
        msg.AppendLine(Loc.GetString("imperial-mood-print-mood", ("state", Loc.GetString($"imperial-mood-print-mood-{comp.MoodLevel}"))));

        msg.AppendLine(Loc.GetString("imperial-mood-print-modifiers"));
        var any = false;
        foreach (var active in comp.Events.Values)
        {
            if (active.Hidden || active.Description.Length == 0)
                continue;

            any = true;
            var color = active.Change switch
            {
                <= ImperialMood.Sad2 => "#ff3d3d",
                <= ImperialMood.Sad1 => "#ff8c3d",
                < 0 => "#a0a0a0",
                0 => "#a0a0a0",
                < ImperialMood.Happy2 => "#6ec36e",
                _ => "#3dd13d",
            };
            msg.AppendLine($"• [color={color}]{FormattedMessage.EscapeText(active.Description)}[/color]");
        }

        if (!any)
            msg.AppendLine(Loc.GetString("imperial-mood-print-none"));

        if (comp.Personalities.Count > 0)
            msg.AppendLine(Loc.GetString("imperial-mood-print-personality", ("personalities", PersonalityNames(comp))));

        if (TryComp<Shared.Imperial.Quirks.ImperialQuirksComponent>(ent, out var quirks) && quirks.Quirks.Count > 0)
        {
            var names = quirks.Quirks.Select(q => _proto.TryIndex(q, out var trait) ? Loc.GetString(trait.Name) : q.Id);
            msg.AppendLine(Loc.GetString("imperial-mood-print-quirks", ("quirks", string.Join(", ", names))));
        }

        var text = msg.ToString().TrimEnd();
        _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Emotes, text, text, EntityUid.Invalid, false, actor.PlayerSession.Channel);
    }

    #endregion
}
