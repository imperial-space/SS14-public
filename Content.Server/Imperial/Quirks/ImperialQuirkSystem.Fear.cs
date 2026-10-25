using Robust.Shared.Random;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Bed.Sleep;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Quirks;

/// <summary>
/// /datum/component/fearful: страх копится от источников и пропадает сам, пороги — испуг, ужас, паника, сердечный приступ.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialQuirkFearComponent : Component
{
    [DataField]
    public bool Claustrophobia;

    [DataField]
    public bool Nyctophobia;

    [DataField]
    public bool Monophobia;

    /// <summary>Фобия (/datum/brain_trauma/mild/phobia): случайная при появлении.</summary>
    [DataField]
    public bool Phobia;

    public string? PhobiaType;
    public float Buildup;
    public float LastBuildup;
    public TimeSpan NextTick;
    public TimeSpan NextPhobiaCheck;
    public TimeSpan ScareCooldown;
    public TimeSpan NextMessage;
    public TimeSpan PanicEnd;
}

public sealed partial class ImperialQuirkSystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly Content.Shared.Mind.SharedMindSystem _mind = default!;

    // TERROR_BUILDUP_*
    private const float TerrorFear = 150f;
    private const float TerrorTerror = 300f;
    private const float TerrorPanic = 500f;
    private const float TerrorPassiveMax = 600f;
    private const float TerrorHeartAttack = 800f;
    private const float TerrorMax = 1000f;
    private const float TerrorPassiveDecrease = 15f;

    private static readonly ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> Psicodine = "Psicodine";

    /// <summary>GLOB.phobia_types, которые SS14 умеет распознать, и слова-триггеры.</summary>
    private static readonly Dictionary<string, string[]> PhobiaWords = new()
    {
        ["spiders"] = ["паук", "паучих", "арахнид"],
        ["clowns"] = ["клоун", "хонк"],
        ["security"] = ["охран", "офицер", "смотрител", "варден", "сбшник"],
        ["doctors"] = ["врач", "доктор", "медик", "хирург"],
        ["authority"] = ["капитан", "глава", "командован", "начальств"],
        ["robots"] = ["робот", "борг", "киборг", "андроид"],
        ["skeletons"] = ["скелет", "кост"],
        ["lizards"] = ["ящер", "унатх", "рептил"],
        ["insects"] = ["жук", "насеком", "моль", "мотыл", "пчел"],
        ["aliens"] = ["ксено", "чужой", "чужие", "пришелец"],
        ["guns"] = ["пушк", "ствол", "пистолет", "винтовк", "стреля"],
        ["blood"] = ["кровь", "крови", "кровав"],
        ["birds"] = ["птиц", "куриц", "утк", "ворон"],
        ["snakes"] = ["змея", "змеи", "змей"],
        ["fish"] = ["рыб", "карп"],
        ["greytide"] = ["ассистент", "грейтайд", "изолк"],
        ["space"] = ["космос", "вакуум"],
    };

    private void InitializeFear()
    {
        SubscribeLocalEvent<EntitySpokeEvent>(OnPhobiaSpeech);
    }

    /// <summary>Страхи складываются в один компонент: клаустро-, никто-, монофобия и фобия.</summary>
    private void ApplyFear(EntityUid mob, string quirk, Robust.Shared.Player.ICommonSession? player)
    {
        if (quirk is not ("ImperialQuirkClaustrophobia" or "ImperialQuirkNyctophobia" or "ImperialQuirkMonophobia" or "ImperialQuirkPhobia"))
            return;

        var fear = EnsureComp<ImperialQuirkFearComponent>(mob);
        fear.Claustrophobia |= quirk == "ImperialQuirkClaustrophobia";
        fear.Nyctophobia |= quirk == "ImperialQuirkNyctophobia";
        fear.Monophobia |= quirk == "ImperialQuirkMonophobia";
        if (quirk != "ImperialQuirkPhobia")
            return;

        fear.Phobia = true;
        fear.PhobiaType = _random.Pick(PhobiaWords.Keys.ToList());
        Message(player, Loc.GetString("imperial-quirk-phobia-gain", ("phobia", Loc.GetString($"imperial-quirk-phobia-{fear.PhobiaType}"))));
    }

    private bool Fearless(EntityUid mob) => HasReagent(mob, Psicodine);

    private void AddTerror(Entity<ImperialQuirkFearComponent> ent, float amount)
    {
        ent.Comp.Buildup = Math.Clamp(ent.Comp.Buildup + amount, 0f, TerrorMax);
    }

    /// <summary>Пассивные источники держат страх не выше 600.</summary>
    private float PassiveSource(Entity<ImperialQuirkFearComponent> ent, float perSecond)
    {
        return Math.Max(0f, Math.Min(perSecond, TerrorPassiveMax - ent.Comp.Buildup));
    }

    private void UpdateFear(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialQuirkFearComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextTick)
                continue;

            comp.NextTick = now + Second;
            var ent = (uid, comp);
            if (!Alive(uid) || HasStatus(uid, SleepingSystem.StatusEffectForcedSleeping))
                continue;

            if (Fearless(uid))
            {
                comp.Buildup = Math.Max(0f, comp.Buildup - TerrorPassiveDecrease * 2);
                continue;
            }

            var gain = 0f;
            if (comp.Claustrophobia && InCloset(uid))
                gain += PassiveSource(ent, 15f);
            if (comp.Nyctophobia && InDarkness(uid))
                gain += PassiveSource(ent, 5f);
            if (comp.Monophobia && Alone(uid))
                gain += PassiveSource(ent, 2.5f);
            if (comp.Phobia)
                gain += CheckPhobia(ent, now);

            if (gain > 0)
            {
                if (comp.Buildup < TerrorFear && comp.Buildup + gain >= TerrorFear)
                    FearMessage(ent, "imperial-quirk-fear-start");
                AddTerror(ent, gain);
            }
            else if (comp.Buildup <= comp.LastBuildup)
            {
                comp.Buildup = Math.Max(0f, comp.Buildup - TerrorPassiveDecrease);
            }

            comp.LastBuildup = comp.Buildup;
            FearEffects(ent, now);
        }
    }

    private bool InCloset(EntityUid mob)
    {
        return _container.TryGetContainingContainer(mob, out var container)
               && HasComp<EntityStorageComponent>(container.Owner);
    }

    /// <summary>get_lumcount: тайл тёмный, если рядом нет включённого света в его радиусе.</summary>
    private bool InDarkness(EntityUid mob)
    {
        var coords = Transform(mob).Coordinates;
        foreach (var light in _lookup.GetEntitiesInRange<Robust.Server.GameObjects.PointLightComponent>(coords, 10f, LookupFlags.Uncontained | LookupFlags.Contained))
        {
            if (!light.Comp.Enabled || light.Comp.Energy <= 0.1f)
                continue;

            var lightCoords = Transform(light).Coordinates;
            if (!lightCoords.TryDistance(EntityManager, coords, out var distance) || distance > light.Comp.Radius * 0.6f)
                continue;

            // Свет в руках или на поясе освещает самого носителя.
            if (_container.IsEntityOrParentInContainer(light) || _examine.InRangeUnOccluded(light, mob, light.Comp.Radius))
                return false;
        }

        return true;
    }

    private bool Alone(EntityUid mob)
    {
        foreach (var other in _lookup.GetEntitiesInRange<MindContainerComponent>(Transform(mob).Coordinates, 7f))
        {
            if (other.Owner != mob && other.Comp.HasMind && Alive(other) && _examine.InRangeUnOccluded(mob, other.Owner, 7f))
                return false;
        }

        return true;
    }

    /// <summary>phobia_source tick: раз в 5 секунд ищет страшное в поле зрения, испуг даёт +120.</summary>
    private float CheckPhobia(Entity<ImperialQuirkFearComponent> ent, TimeSpan now)
    {
        if (ent.Comp.PhobiaType is not { } phobia || now < ent.Comp.ScareCooldown || now < ent.Comp.NextPhobiaCheck)
            return 0f;

        ent.Comp.NextPhobiaCheck = now + TimeSpan.FromSeconds(5);
        if (phobia == "space" && Transform(ent).GridUid == null)
            return FreakOut(ent, Loc.GetString("imperial-quirk-phobia-reason-space"));

        foreach (var other in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 7f))
        {
            if (other == ent.Owner || !IsScary(phobia, other) || !_examine.InRangeUnOccluded(ent.Owner, other, 7f))
                continue;

            return FreakOut(ent, Name(other));
        }

        return 0f;
    }

    private bool IsScary(string phobia, EntityUid uid)
    {
        var id = MetaData(uid).EntityPrototype?.ID ?? string.Empty;
        var species = TryComp<HumanoidProfileComponent>(uid, out var profile) ? profile.Species.Id : null;
        return phobia switch
        {
            "spiders" => species == "Arachnid" || id.Contains("Spider") && HasComp<Content.Shared.Mobs.Components.MobStateComponent>(uid),
            "clowns" => HasJob(uid, "Clown") || id is "ClothingMaskClown" or "BikeHorn",
            "security" => HasDepartment(uid, "Security"),
            "doctors" => HasDepartment(uid, "Medical"),
            "authority" => HasDepartment(uid, "Command"),
            "robots" => HasComp<BorgChassisComponent>(uid),
            "skeletons" => species == "Skeleton" || id.Contains("Skeleton") && HasComp<Content.Shared.Mobs.Components.MobStateComponent>(uid),
            "lizards" => species == "Reptilian" || id.Contains("Lizard"),
            "insects" => species == "Moth" || id.Contains("Bee") && HasComp<Content.Shared.Mobs.Components.MobStateComponent>(uid),
            "aliens" => id.Contains("Xeno") && HasComp<Content.Shared.Mobs.Components.MobStateComponent>(uid),
            "guns" => HasComp<GunComponent>(uid),
            "blood" => TryComp<PuddleComponent>(uid, out _) && PuddleHasBlood(uid),
            "birds" => id.Contains("Chicken") || id.Contains("Duck") || id.Contains("Crow"),
            "snakes" => id.Contains("Snake"),
            "fish" => id.Contains("Carp") || id.Contains("Fish"),
            "greytide" => HasJob(uid, "Passenger") || id == "ClothingHandsGlovesColorYellow",
            _ => false,
        };
    }

    private bool PuddleHasBlood(EntityUid puddle)
    {
        return _solutions.TryGetSolution(puddle, "puddle", out _, out var solution)
               && solution.GetTotalPrototypeQuantity("Blood").Float() > 0;
    }

    private bool HasJob(EntityUid uid, string job)
    {
        return _mind.TryGetMind(uid, out var mindId, out _) && _jobs.MindTryGetJobId(mindId, out var id) && id == job;
    }

    private bool HasDepartment(EntityUid uid, string department)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _) || !_jobs.MindTryGetJobId(mindId, out var job) || job == null)
            return false;

        return _proto.TryIndex<Content.Shared.Roles.DepartmentPrototype>(department, out var dept) && dept.Roles.Contains(job.Value);
    }

    private float FreakOut(Entity<ImperialQuirkFearComponent> ent, string reason)
    {
        ent.Comp.ScareCooldown = _timing.CurTime + TimeSpan.FromSeconds(12);
        _popup.PopupEntity(Loc.GetString("imperial-quirk-phobia-see", ("thing", reason), ("message", Loc.GetString($"imperial-quirk-phobia-scare-{_random.Next(1, 6)}"))),
            ent, ent, PopupType.LargeCaution);
        Startle(ent);
        return 120f;
    }

    /// <summary>handle_hearing / handle_speech: слово-триггер пугает слышащего и сбивает речь самого фобика.</summary>
    private void OnPhobiaSpeech(EntitySpokeEvent args)
    {
        var message = args.Message.ToLowerInvariant();
        var query = EntityQueryEnumerator<ImperialQuirkFearComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PhobiaType is not { } phobia || !PhobiaWords[phobia].Any(message.Contains) || Fearless(uid))
                continue;

            if (uid == args.Source)
            {
                if (_random.Prob(0.5f))
                {
                    _stutter.DoStutter(uid, TimeSpan.FromSeconds(4), true);
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-phobia-say"), uid, uid, PopupType.SmallCaution);
                }

                continue;
            }

            if (HasComp<ImperialQuirkDeafComponent>(uid) || _timing.CurTime < comp.ScareCooldown
                || !_examine.InRangeUnOccluded(args.Source, uid, ChatSystem.VoiceRange))
            {
                continue;
            }

            comp.ScareCooldown = _timing.CurTime + TimeSpan.FromSeconds(12);
            _popup.PopupEntity(Loc.GetString("imperial-quirk-phobia-hear", ("message", Loc.GetString($"imperial-quirk-phobia-scare-{_random.Next(1, 6)}"))),
                uid, uid, PopupType.MediumCaution);
            if (comp.Buildup < TerrorPassiveMax)
                comp.Buildup = Math.Min(comp.Buildup + 40f, TerrorPassiveMax);
        }
    }

    /// <summary>terror_handler/startle.</summary>
    private void Startle(Entity<ImperialQuirkFearComponent> ent)
    {
        switch (_random.Next(1, 4))
        {
            case 1:
                _popup.PopupEntity(Loc.GetString("imperial-quirk-fear-startled"), ent, ent, PopupType.MediumCaution);
                _stun.TryUpdateStunDuration(ent, TimeSpan.FromSeconds(0.5));
                break;
            case 2:
                _chat.TryEmoteWithChat(ent, "Scream");
                if (_random.Prob(0.15f) && _hands.GetActiveItem(ent.Owner) is { } held)
                    _hands.TryDrop(ent.Owner, held);
                break;
            default:
                _jitter.DoJitter(ent, TimeSpan.FromSeconds(10), true);
                break;
        }
    }

    private void FearMessage(Entity<ImperialQuirkFearComponent> ent, string key)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextMessage)
            return;

        ent.Comp.NextMessage = now + TimeSpan.FromSeconds(15);
        _popup.PopupEntity(Loc.GetString(key), ent, ent, PopupType.MediumCaution);
    }

    /// <summary>Обработчики по умолчанию: дрожь, заикание, сердце, паника.</summary>
    private void FearEffects(Entity<ImperialQuirkFearComponent> ent, TimeSpan now)
    {
        var terror = ent.Comp.Buildup;
        if (terror < TerrorFear)
            return;

        var scale = Math.Clamp((terror - TerrorFear) / (TerrorTerror - TerrorFear), 0f, 1f);

        // jittering
        if (terror > TerrorTerror || _random.Prob((1f + 4f * scale) / 100f))
        {
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(10), true);
            FearMessage(ent, "imperial-quirk-fear-shaking");
        }

        // stuttering
        if (terror > TerrorTerror || _random.Prob((1f + 4f * scale) / 100f))
            _stutter.DoStutter(ent, TimeSpan.FromSeconds(10), true);

        // heart_problems
        var heartScale = Math.Clamp((terror - TerrorFear) / (TerrorPanic - TerrorFear), 0f, 1f);
        if (_random.Prob((1f + 4f * heartScale) / 100f))
        {
            if (terror < TerrorHeartAttack || !_random.Prob(0.15f))
            {
                Damage(ent, "Asphyxiation", 8f);
                FearMessage(ent, "imperial-quirk-fear-heart");
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("imperial-quirk-fear-heart-attack", ("user", ent.Owner)), ent, PopupType.LargeCaution);
                Damage(ent, "Asphyxiation", 40f);
                _status.TryUpdateStatusEffectDuration(ent, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(20));
            }
        }

        // panic
        if (terror < TerrorPanic || now < ent.Comp.PanicEnd)
            return;

        var panicScale = Math.Clamp((terror - TerrorPanic) / (TerrorMax - TerrorPanic), 0f, 1f);
        if (!_random.Prob((2f + 3f * panicScale) / 100f))
            return;

        ent.Comp.PanicEnd = now + TimeSpan.FromSeconds(_random.Next(3, 6));
        _chat.TryEmoteWithChat(ent, "Gasp");
        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(0.5), true);
        _popup.PopupEntity(Loc.GetString("imperial-quirk-fear-panic", ("user", ent.Owner)), ent, PopupType.MediumCaution);
        AddTerror(ent, 25f);
    }
}
