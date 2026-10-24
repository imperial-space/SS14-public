using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Emp;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash;
using Content.Shared.Hands;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Jittering;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Quirks;

#region Компоненты

/// <summary>/datum/quirk/item_quirk/addict: тяга к веществу, ломка без дозы.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkAddictionComponent : Component
{
    /// <summary>Любое из этих веществ в крови снимает ломку; пусто — подходит опьянение (алкоголик).</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    /// <summary>Наркоман: одно случайное вещество из списка.</summary>
    [DataField]
    public bool PickOne;

    [DataField]
    public bool Drunk;

    [DataField]
    public EntProtoId? Container;

    [DataField]
    public int Pills;

    [DataField]
    public float PillVolume = 3f;

    public TimeSpan LastFix;
    public TimeSpan NextCheck;
}

/// <summary>/datum/quirk/item_quirk/allergic: шесть лекарств вызывают реакцию, адреналин её снимает.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkMedicineAllergyComponent : Component
{
    public List<ProtoId<ReagentPrototype>> Allergies = new();
    public TimeSpan NextCheck;
}

/// <summary>/datum/quirk/item_quirk/asthma: воспаление дыхательных путей и приступы.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkAsthmaComponent : Component
{
    public float Inflammation;
    public TimeSpan NextAttackAllowed;
    public TimeSpan? AttackEnd;
    public float AttackRate;
    public TimeSpan NextCheck;
}

/// <summary>/datum/quirk/blooddeficiency.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkBloodDeficiencyComponent : Component
{
    public TimeSpan NextCheck;
}

/// <summary>/datum/quirk/item_quirk/brainproblems: опухоль медленно разрушает мозг.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkBrainTumorComponent : Component
{
    public TimeSpan NextCheck;
}

/// <summary>/datum/disease/chronic_illness: пять стадий, сансуфентанил откатывает болезнь.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkChronicIllnessComponent : Component
{
    public int Stage = 1;
    public TimeSpan NextCheck;
}

/// <summary>/datum/component/omen/quirk: несчастья на каждом шагу.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkCursedComponent : Component
{
    public Vector2i LastTile;
    public TimeSpan NextMisfortune;
}

/// <summary>TRAIT_DEAF.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkDeafComponent : Component;

/// <summary>/datum/component/manual_blinking: без моргания глаза сохнут.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkFluorideStareComponent : Component
{
    public EntityUid? Action;
    public TimeSpan DryAt;
    public bool Warned;
    public TimeSpan NextDamage;
}

/// <summary>TRAIT_EASILY_WOUNDED.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkFrailComponent : Component
{
    [DataField]
    public float Multiplier = 1.25f;
}

/// <summary>/datum/quirk/glass_jaw.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkGlassJawComponent : Component;

/// <summary>/datum/brain_trauma/severe/paralysis/hemiplegic: одна рука не работает, нога волочится.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkHemiplegicComponent : Component
{
    public string? Hand;
}

/// <summary>TRAIT_ILLITERATE.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkIlliterateComponent : Component;

/// <summary>/datum/quirk/insanity: галлюцинации, токсин «Разрушитель разума» их подавляет.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkInsanityComponent : Component
{
    public bool Suppressed;
    public TimeSpan NextCheck;
}

/// <summary>Нарколепсия, которую подавляют стимуляторы.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkNarcolepsyComponent : Component
{
    public TimeSpan NextCheck;
}

/// <summary>/datum/quirk/photophobia: вспышки бьют по глазам сильнее.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkPhotophobiaComponent : Component;

/// <summary>/datum/quirk/poor_aim: разброс +10…35°.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkPoorAimComponent : Component;

/// <summary>/datum/quirk/social_anxiety.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkSocialAnxietyComponent : Component
{
    public bool DumbThing = true;
    public TimeSpan SilencedUntil;
}

/// <summary>TRAIT_GRABWEAKNESS: из захвата вырваться труднее (стадия +1). Проверяется в захватах боевых искусств по имени компонента.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkPushoverComponent : Component;

/// <summary>TRAIT_SOFTSPOKEN: голос слышно только рядом.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkSoftSpokenComponent : Component
{
    [DataField]
    public float Range = 2f;
}

/// <summary>/datum/quirk/tin_man: дешёвые протезы органов отказывают от ЭМИ.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkTinManComponent : Component;

/// <summary>/datum/quirk/paraplegic: бесплатная коляска при появлении.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkParaplegicComponent : Component
{
    [DataField]
    public EntProtoId Wheelchair = "ImperialVehicleWheelchair";
}

#endregion

public sealed partial class ImperialQuirkSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;

    private static readonly ProtoId<ReagentPrototype> Epinephrine = "Epinephrine";
    private static readonly ProtoId<ReagentPrototype> Histamine = "Histamine";
    private static readonly ProtoId<ReagentPrototype> Salbutamol = "Salbutamol";
    private static readonly ProtoId<ReagentPrototype> Mindbreaker = "MindbreakerToxin";
    private static readonly ProtoId<ReagentPrototype> Sansufentanyl = "ImperialSansufentanyl";
    private static readonly ProtoId<ReagentPrototype>[] Stimulants = ["Ephedrine", "Desoxyephedrine", "Stimulants", "Coffee"];
    private static readonly EntProtoId BlinkAction = "ImperialActionQuirkBlink";
    private static readonly ProtoId<ImperialQuirkPrototype> InsanityQuirk = "ImperialQuirkInsanity";
    private static readonly EntProtoId PillProto = "Pill";
    private static readonly SoundSpecifier FlashRing = new SoundPathSpecifier("/Audio/Weapons/flash.ogg");

    /// <summary>GLOB.possible_junkie_addictions.</summary>
    private static readonly ProtoId<ReagentPrototype>[] JunkieDrugs =
        ["Desoxyephedrine", "SpaceDrugs", "THC", "Happiness", "Bananadine"];

    /// <summary>Лекарства, на которые не бывает аллергии (blacklist TG).</summary>
    private static readonly HashSet<string> AllergyBlacklist =
        ["Epinephrine", "Saline", "Salbutamol", "Inaprovaline", "AdminOrdrazine", "Omnizine", "ImperialSansufentanyl"];

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private void InitializeNegative()
    {
        SubscribeLocalEvent<ImperialQuirkAddictionComponent, ImperialQuirkAddedEvent>(OnAddictionAdded);
        SubscribeLocalEvent<ImperialQuirkMedicineAllergyComponent, ImperialQuirkAddedEvent>(OnAllergyAdded);
        SubscribeLocalEvent<ImperialQuirkAsthmaComponent, ImperialQuirkAddedEvent>(OnAsthmaAdded);
        SubscribeLocalEvent<ImperialQuirkFluorideStareComponent, ImperialQuirkAddedEvent>(OnFluorideAdded);
        SubscribeLocalEvent<ImperialQuirkFluorideStareComponent, ImperialQuirkBlinkActionEvent>(OnBlink);
        SubscribeLocalEvent<ImperialQuirkHemiplegicComponent, ImperialQuirkAddedEvent>(OnHemiplegicAdded);
        SubscribeLocalEvent<ImperialQuirkHemiplegicComponent, VirtualItemDeletedEvent>(OnHemiplegicHandFreed);
        SubscribeLocalEvent<ImperialQuirkParaplegicComponent, ImperialQuirkAddedEvent>(OnParaplegicAdded);
        SubscribeLocalEvent<ImperialQuirkCursedComponent, MoveEvent>(OnCursedMove);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<ImperialQuirkFrailComponent, DamageModifyEvent>(OnFrailDamage);
        SubscribeLocalEvent<ImperialQuirkGlassJawComponent, DamageChangedEvent>(OnGlassJawDamage);
        SubscribeLocalEvent<ImperialQuirkIlliterateComponent, UserOpenActivatableUIAttemptEvent>(OnIlliterateOpenUi);
        SubscribeLocalEvent<ImperialQuirkPhotophobiaComponent, AfterFlashedEvent>(OnPhotophobiaFlashed);
        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<ImperialQuirkSocialAnxietyComponent, AccentGetEvent>(OnAnxietyAccent);
        SubscribeLocalEvent<ImperialQuirkSocialAnxietyComponent, SpeakAttemptEvent>(OnAnxietySpeakAttempt);
        SubscribeLocalEvent<ImperialQuirkTinManComponent, EmpPulseEvent>(OnTinManEmp);
    }

    #region Общее

    /// <summary>has_reagent: вещество в любом растворе тела (кровь, метаболиты).</summary>
    private bool HasReagent(EntityUid mob, string reagent, float min = 0.01f)
    {
        foreach (var (_, solution) in _solutions.EnumerateSolutions(mob))
        {
            var contents = solution.Comp.Solution;
            if (contents.GetTotalPrototypeQuantity(reagent).Float() >= min)
                return true;
        }

        return false;
    }

    private float ReagentQuantity(EntityUid mob, string reagent)
    {
        var total = 0f;
        foreach (var (_, solution) in _solutions.EnumerateSolutions(mob))
        {
            var contents = solution.Comp.Solution;
            total += contents.GetTotalPrototypeQuantity(reagent).Float();
        }

        return total;
    }

    private void AddReagent(EntityUid mob, string reagent, float amount)
    {
        if (TryComp<BloodstreamComponent>(mob, out var blood)
            && _solutions.ResolveSolution(mob, blood.BloodSolutionName, ref blood.BloodSolution))
        {
            _solutions.TryAddReagent(blood.BloodSolution.Value, reagent, amount, out _);
        }
    }

    private void Damage(EntityUid mob, string type, float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict[type] = amount;
        _damageable.TryChangeDamage(mob, spec, true, false);
    }

    private bool Alive(EntityUid mob) => !_mobState.IsDead(mob);

    /// <summary>Статус есть, только если у моба вообще есть контейнер статусов (иначе система пишет ошибку).</summary>
    private bool HasStatus(EntityUid mob, EntProtoId status)
    {
        return HasComp<Content.Shared.StatusEffectNew.Components.StatusEffectContainerComponent>(mob) && _status.HasStatusEffect(mob, status);
    }

    /// <summary>Баночка таблеток с веществом (pill_bottle из TG).</summary>
    private void GivePills(EntityUid mob, EntProtoId canister, string reagent, int count, float volume)
    {
        var bottle = Spawn(canister, Transform(mob).Coordinates);
        for (var i = 0; i < count; i++)
        {
            var pill = Spawn(PillProto, Transform(mob).Coordinates);
            if (_solutions.TryGetSolution(pill, "food", out var solution))
            {
                _solutions.RemoveAllSolution(solution.Value);
                _solutions.TryAddReagent(solution.Value, reagent, volume, out _);
            }

            if (!_storage.Insert(bottle, pill, out _, playSound: false))
                GiveItem(mob, pill);
        }

        GiveItem(mob, bottle);
    }

    #endregion

    #region Зависимости

    private void OnAddictionAdded(Entity<ImperialQuirkAddictionComponent> ent, ref ImperialQuirkAddedEvent args)
    {
        if (ent.Comp.PickOne && ent.Comp.Reagents.Count == 0)
            ent.Comp.Reagents.Add(_random.Pick(JunkieDrugs));

        ent.Comp.LastFix = _timing.CurTime;
        if (ent.Comp.Container is { } container && ent.Comp.Pills > 0 && ent.Comp.Reagents.Count > 0)
            GivePills(ent, container, ent.Comp.Reagents[0], ent.Comp.Pills, ent.Comp.PillVolume);

        if (ent.Comp.PickOne)
        {
            var name = _proto.Index(ent.Comp.Reagents[0]).LocalizedName;
            Message(args.Player, Loc.GetString("imperial-quirk-junkie-drug", ("drug", name)));
        }
    }

    private bool Satisfied(Entity<ImperialQuirkAddictionComponent> ent)
    {
        if (ent.Comp.Drunk && HasStatus(ent, SharedDrunkSystem.Drunk))
            return true;

        foreach (var reagent in ent.Comp.Reagents)
        {
            if (HasReagent(ent, reagent))
                return true;
        }

        return false;
    }

    /// <summary>Ломка: 5 минут без дозы — тяга, 10 — тошнота, 15 — судороги и падения.</summary>
    private void UpdateAddiction(Entity<ImperialQuirkAddictionComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck || !Alive(ent))
            return;

        ent.Comp.NextCheck = now + TimeSpan.FromSeconds(5);
        if (Satisfied(ent))
        {
            ent.Comp.LastFix = now;
            return;
        }

        var minutes = (now - ent.Comp.LastFix).TotalMinutes;
        if (minutes < 5)
            return;

        if (_random.Prob(0.15f))
            _popup.PopupEntity(Loc.GetString($"imperial-quirk-withdrawal-{_random.Next(1, 5)}"), ent, ent, PopupType.MediumCaution);

        if (minutes >= 5)
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(4), true, 4f, 4f);

        if (minutes >= 10 && _random.Prob(0.04f))
            _vomit.Vomit(ent);

        if (minutes >= 15)
        {
            Damage(ent, "Poison", 0.5f);
            if (_random.Prob(0.03f))
            {
                _popup.PopupEntity(Loc.GetString("imperial-quirk-withdrawal-collapse"), ent, ent, PopupType.LargeCaution);
                _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(3), true);
            }
        }
    }

    #endregion

    #region Аллергия на лекарства

    private void OnAllergyAdded(Entity<ImperialQuirkMedicineAllergyComponent> ent, ref ImperialQuirkAddedEvent args)
    {
        var medicine = _proto.EnumeratePrototypes<ReagentPrototype>()
            .Where(r => !r.Abstract && r.Group == "Medicine" && !AllergyBlacklist.Contains(r.ID))
            .ToList();
        _random.Shuffle(medicine);
        ent.Comp.Allergies = medicine.Take(6).Select(r => new ProtoId<ReagentPrototype>(r.ID)).ToList();

        var names = string.Join(", ", medicine.Take(6).Select(r => r.LocalizedName));
        Message(args.Player, Loc.GetString("imperial-quirk-allergy-list", ("list", names)));

        // Жетон аллергика: запись для медиков.
        var tag = Spawn("Paper", Transform(ent).Coordinates);
        _metaData.SetEntityName(tag, Loc.GetString("imperial-quirk-allergy-tag-name"));
        if (TryComp<PaperComponent>(tag, out var paper))
            _paper.SetContent((tag, paper), Loc.GetString("imperial-quirk-allergy-tag", ("name", Name(ent)), ("list", names)));
        GiveItem(ent, tag);
    }

    private void UpdateAllergy(Entity<ImperialQuirkMedicineAllergyComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck || !Alive(ent))
            return;

        ent.Comp.NextCheck = now + Second;
        if (!ent.Comp.Allergies.Any(a => HasReagent(ent, a)) || HasReagent(ent, Epinephrine))
            return;

        Damage(ent, "Poison", 3f);
        AddReagent(ent, Histamine, 3f);
        if (_random.Prob(0.1f))
            _vomit.Vomit(ent);
    }

    #endregion

    #region Астма

    private void OnAsthmaAdded(Entity<ImperialQuirkAsthmaComponent> ent, ref ImperialQuirkAddedEvent args)
    {
        ent.Comp.NextAttackAllowed = _timing.CurTime + TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Воспаление растёт от дыма и гистамина, падает само и от сальбутамола; выше 450 из 500 лёгкие почти не работают.
    /// Приступы — с 10-й минуты, не чаще раза в 15–25 минут.
    /// </summary>
    private void UpdateAsthma(Entity<ImperialQuirkAsthmaComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck || !Alive(ent))
            return;

        ent.Comp.NextCheck = now + Second;
        var comp = ent.Comp;
        var change = -0.15f;

        if (HasComp<Content.Shared.Chemistry.Components.SmokeAffectedComponent>(ent))
            change += 7.5f;

        var histamine = ReagentQuantity(ent, Histamine);
        if (histamine >= 30)
            change += 10f;
        else if (histamine > 0)
            change += 2f;

        var albuterol = HasReagent(ent, Salbutamol);
        if (albuterol)
        {
            change -= 3f;
            comp.AttackEnd = null;
        }

        if (comp.AttackEnd is { } end)
        {
            if (now >= end)
            {
                comp.AttackEnd = null;
                comp.NextAttackAllowed = now + TimeSpan.FromMinutes(_random.Next(15, 26));
            }
            else
            {
                change += comp.AttackRate;
            }
        }
        else if (!albuterol && now >= comp.NextAttackAllowed && _random.Prob(0.0005f))
        {
            StartAsthmaAttack(ent);
        }

        var old = comp.Inflammation;
        comp.Inflammation = Math.Clamp(comp.Inflammation + change, 0f, 500f);
        if (comp.Inflammation > old && _random.Prob(0.05f))
            _popup.PopupEntity(Loc.GetString("imperial-quirk-asthma-tight"), ent, ent, PopupType.SmallCaution);
        if (comp.Inflammation > old && _random.Prob(0.01f))
            _chat.TryEmoteWithChat(ent, "Cough");

        var breathing = 1f - Math.Min(comp.Inflammation / 450f, 1f);
        if (breathing < 0.6f)
            Damage(ent, "Asphyxiation", (0.6f - breathing) * 4f);
    }

    /// <summary>asthma_attack_rarities: лёгкий 300, средний 400, тяжёлый 100, критический 1.</summary>
    private void StartAsthmaAttack(Entity<ImperialQuirkAsthmaComponent> ent)
    {
        var roll = _random.Next(801);
        var (rate, seconds, key) = roll switch
        {
            < 300 => (2f, 60, "minor"),
            < 700 => (4f, 90, "moderate"),
            < 800 => (8f, 120, "severe"),
            _ => (16f, 180, "critical"),
        };

        ent.Comp.AttackRate = rate;
        ent.Comp.AttackEnd = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        _popup.PopupEntity(Loc.GetString($"imperial-quirk-asthma-attack-{key}"), ent, ent, PopupType.LargeCaution);
        _chat.TryEmoteWithChat(ent, "Cough");
    }

    #endregion

    #region Кровь, мозг, хроническая болезнь

    /// <summary>lose_blood: кровь убывает до безопасного уровня минус 25 (≈80 % нормы).</summary>
    private void UpdateBloodDeficiency(Entity<ImperialQuirkBloodDeficiencyComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck || !Alive(ent))
            return;

        ent.Comp.NextCheck = now + Second;
        if (_bloodstream.GetBloodLevel(ent.Owner) > 0.8f)
            _bloodstream.TryModifyBloodLevel(ent.Owner, -0.6f);
    }

    /// <summary>adjust_organ_loss(brain, 0.2/с): без лечения смерть мозга примерно через 16 минут.</summary>
    private void UpdateBrainTumor(Entity<ImperialQuirkBrainTumorComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck || !Alive(ent))
            return;

        ent.Comp.NextCheck = now + Second;
        Damage(ent, "Cellular", 0.2f);
    }

    /// <summary>chronic_illness stage_act; стадия растёт с шансом 0.25 %/с, сансуфентанил откатывает.</summary>
    private void UpdateChronicIllness(Entity<ImperialQuirkChronicIllnessComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck)
            return;

        ent.Comp.NextCheck = now + Second;
        var comp = ent.Comp;
        if (HasReagent(ent, Sansufentanyl))
        {
            if (comp.Stage > 1 && _random.Prob(0.05f))
            {
                comp.Stage--;
                _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-better"), ent, ent);
            }

            return;
        }

        if (comp.Stage < 5 && _random.Prob(0.0025f))
            comp.Stage++;

        switch (comp.Stage)
        {
            case 2:
                if (_random.Prob(0.005f))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-dizzy"), ent, ent, PopupType.SmallCaution);
                    _jitter.DoJitter(ent, TimeSpan.FromSeconds(6), true);
                }

                break;
            case 3:
                if (_random.Prob(0.005f))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-chest"), ent, ent, PopupType.MediumCaution);
                    if (_random.Prob(0.45f))
                        _vomit.Vomit(ent);
                }

                if (_random.Prob(0.005f))
                    _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(2), true);
                if (_random.Prob(0.005f))
                    Damage(ent, "Blunt", 1f);
                break;
            case 4:
                if (_random.Prob(0.01f))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-gruesome"), ent, ent, PopupType.LargeCaution);
                    if (_random.Prob(0.75f))
                        _vomit.Vomit(ent);
                }

                if (_random.Prob(0.01f))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-collapse", ("user", ent.Owner)), ent, PopupType.MediumCaution);
                    _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(4), true);
                }

                if (_random.Prob(0.005f))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-torn"), ent, ent, PopupType.LargeCaution);
                    _chat.TryEmoteWithChat(ent, "Scream");
                    Damage(ent, "Blunt", 10f);
                }

                break;
            case 5:
                if (_random.Prob(0.5f))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-realign"), ent, ent);
                    comp.Stage = 1;
                    break;
                }

                // «Для вас нет места в этой временной линии».
                _popup.PopupEntity(Loc.GetString("imperial-quirk-chronic-erased", ("user", ent.Owner)), ent, PopupType.LargeCaution);
                _chat.TryEmoteWithChat(ent, "Scream");
                Spawn("Ash", Transform(ent).Coordinates);
                QueueDel(ent);
                break;
        }
    }

    #endregion

    #region Проклятие, глухота, тихий голос

    /// <summary>omen: на новом тайле с небольшим шансом случается беда.</summary>
    private void OnCursedMove(Entity<ImperialQuirkCursedComponent> ent, ref MoveEvent args)
    {
        if (Transform(ent).GridUid is not { } grid || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(grid, out var gridComp))
            return;

        var tile = _map.TileIndicesFor(grid, gridComp, args.NewPosition);
        if (tile == ent.Comp.LastTile)
            return;

        ent.Comp.LastTile = tile;
        var now = _timing.CurTime;
        if (now < ent.Comp.NextMisfortune || !Alive(ent) || !_random.Prob(0.02f))
            return;

        ent.Comp.NextMisfortune = now + TimeSpan.FromSeconds(30);
        switch (_random.Next(4))
        {
            case 0:
                _popup.PopupEntity(Loc.GetString("imperial-quirk-cursed-trip", ("user", ent.Owner)), ent, PopupType.MediumCaution);
                _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(2), true);
                break;
            case 1:
                _popup.PopupEntity(Loc.GetString("imperial-quirk-cursed-head", ("user", ent.Owner)), ent, PopupType.MediumCaution);
                Damage(ent, "Blunt", _random.Next(5, 11));
                break;
            case 2:
                _popup.PopupEntity(Loc.GetString("imperial-quirk-cursed-toe"), ent, ent, PopupType.SmallCaution);
                Damage(ent, "Blunt", 2f);
                _chat.TryEmoteWithChat(ent, "Scream");
                break;
            default:
                _popup.PopupEntity(Loc.GetString("imperial-quirk-cursed-shock", ("user", ent.Owner)), ent, PopupType.MediumCaution);
                Damage(ent, "Shock", 5f);
                _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(1));
                break;
        }
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent args)
    {
        var soft = TryComp<ImperialQuirkSoftSpokenComponent>(args.Source, out var softComp)
                   && MathHelper.CloseTo(args.VoiceRange, ChatSystem.VoiceRange);

        var removed = new List<Robust.Shared.Player.ICommonSession>();
        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer || session.AttachedEntity is not { } listener || listener == args.Source)
                continue;

            if (HasComp<ImperialQuirkDeafComponent>(listener) || soft && data.Range > softComp!.Range)
                removed.Add(session);
        }

        foreach (var session in removed)
        {
            args.Recipients.Remove(session);
        }
    }

    #endregion

    #region Глаза

    private void OnFluorideAdded(Entity<ImperialQuirkFluorideStareComponent> ent, ref ImperialQuirkAddedEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, BlinkAction);
        ent.Comp.DryAt = _timing.CurTime + TimeSpan.FromMinutes(3);
    }

    private void OnBlink(Entity<ImperialQuirkFluorideStareComponent> ent, ref ImperialQuirkBlinkActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.DryAt = _timing.CurTime + TimeSpan.FromMinutes(3);
        ent.Comp.Warned = false;
        _popup.PopupEntity(Loc.GetString("imperial-quirk-blink"), ent, ent);
    }

    /// <summary>manual_blinking: за 30 секунд — предупреждение, потом глаза повреждаются, пока не моргнёшь.</summary>
    private void UpdateFluoride(Entity<ImperialQuirkFluorideStareComponent> ent, TimeSpan now)
    {
        if (!Alive(ent))
            return;

        if (!ent.Comp.Warned && now >= ent.Comp.DryAt - TimeSpan.FromSeconds(30))
        {
            ent.Comp.Warned = true;
            _popup.PopupEntity(Loc.GetString("imperial-quirk-eyes-dry"), ent, ent, PopupType.MediumCaution);
        }

        if (now < ent.Comp.DryAt || now < ent.Comp.NextDamage)
            return;

        ent.Comp.NextDamage = now + TimeSpan.FromSeconds(10);
        _blindable.AdjustEyeDamage(ent.Owner, 1);
        _popup.PopupEntity(Loc.GetString("imperial-quirk-eyes-burn"), ent, ent, PopupType.LargeCaution);
    }

    private void OnPhotophobiaFlashed(Entity<ImperialQuirkPhotophobiaComponent> ent, ref AfterFlashedEvent args)
    {
        // FLASH_PROTECTION_HYPER_SENSITIVE: вспышка бьёт по глазам и держит дольше.
        _blindable.AdjustEyeDamage(ent.Owner, 1);
        _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(1));
        _popup.PopupEntity(Loc.GetString("imperial-quirk-photophobia-flash"), ent, ent, PopupType.MediumCaution);
    }

    #endregion

    #region Урон

    private void OnFrailDamage(EntityUid uid, ImperialQuirkFrailComponent comp, DamageModifyEvent args)
    {
        foreach (var type in new[] { "Blunt", "Slash", "Piercing" })
        {
            if (args.Damage.DamageDict.TryGetValue(type, out var value) && value > 0)
                args.Damage.DamageDict[type] = value * comp.Multiplier;
        }
    }

    /// <summary>punch_out: удар по голове от 5 урона с шансом, равным урону, отправляет в нокаут (голова — каждый шестой удар).</summary>
    private void OnGlassJawDamage(Entity<ImperialQuirkGlassJawComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null || args.DamageDelta == null)
            return;

        var brute = 0f;
        foreach (var type in new[] { "Blunt", "Slash", "Piercing" })
        {
            if (args.DamageDelta.DamageDict.TryGetValue(type, out var value))
                brute += value.Float() * (type == "Blunt" ? 1f : 0.65f);
        }

        if (brute < 5 || !_random.Prob(1f / 6f) || !_random.Prob(Math.Min(1f, brute / 100f)))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-quirk-glass-jaw", ("user", ent.Owner)), ent, PopupType.MediumCaution);
        _status.TryUpdateStatusEffectDuration(ent, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(3));
    }

    private void OnTinManEmp(Entity<ImperialQuirkTinManComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        _popup.PopupEntity(Loc.GetString("imperial-quirk-tin-man-emp"), ent, ent, PopupType.LargeCaution);
        Damage(ent, "Asphyxiation", 25f);
        Damage(ent, "Poison", 15f);
        _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(4));
    }

    #endregion

    #region Паралич

    private void OnHemiplegicAdded(Entity<ImperialQuirkHemiplegicComponent> ent, ref ImperialQuirkAddedEvent args)
    {
        if (TryComp<Content.Shared.Hands.Components.HandsComponent>(ent, out var hands))
            ent.Comp.Hand = _random.Pick(hands.Hands.Keys.ToList());

        BlockHand(ent);
        RecalcSpeed(ent);
    }

    private void BlockHand(Entity<ImperialQuirkHemiplegicComponent> ent)
    {
        if (ent.Comp.Hand is not { } hand || TerminatingOrDeleted(ent))
            return;

        _hands.TryDrop(ent.Owner, hand);
        _virtualItem.TrySpawnVirtualItemInHand(ent, ent, out _, false, hand, true);
    }

    private void OnHemiplegicHandFreed(Entity<ImperialQuirkHemiplegicComponent> ent, ref VirtualItemDeletedEvent args)
    {
        if (args.BlockingEntity == ent.Owner)
            Robust.Shared.Timing.Timer.Spawn(0, () => BlockHand(ent));
    }

    private void OnParaplegicAdded(Entity<ImperialQuirkParaplegicComponent> ent, ref ImperialQuirkAddedEvent args)
    {
        if (TryComp<Content.Shared.Buckle.Components.BuckleComponent>(ent, out var buckle) && buckle.BuckledTo != null)
            _buckle.TryUnbuckle((ent, buckle), ent, popup: false);

        var chair = Spawn(ent.Comp.Wheelchair, Transform(ent).Coordinates);
        _buckle.TryBuckle(ent, ent, chair, popup: false);
    }

    #endregion

    #region Неграмотность, РДС, нарколепсия

    private void OnIlliterateOpenUi(Entity<ImperialQuirkIlliterateComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<PaperComponent>(args.Target) && !HasComp<Content.Server.Construction.Components.ComputerComponent>(args.Target))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("imperial-quirk-illiterate"), ent, ent);
    }

    /// <summary>Разрушитель разума подавляет галлюцинации, пока он в крови.</summary>
    private void UpdateInsanity(Entity<ImperialQuirkInsanityComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck)
            return;

        ent.Comp.NextCheck = now + TimeSpan.FromSeconds(2);
        var suppressed = HasReagent(ent, Mindbreaker);
        if (suppressed == ent.Comp.Suppressed)
            return;

        ent.Comp.Suppressed = suppressed;
        if (suppressed)
            RemComp<ParacusiaComponent>(ent);
        else
            EntityManager.AddComponents(ent, _proto.Index(InsanityQuirk).Components, false);
    }

    /// <summary>Стимуляторы не дают уснуть при нарколепсии.</summary>
    private void UpdateNarcolepsy(Entity<ImperialQuirkNarcolepsyComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck)
            return;

        ent.Comp.NextCheck = now + Second;
        if (Stimulants.Any(s => HasReagent(ent, s)))
            if (HasStatus(ent, SleepingSystem.StatusEffectForcedSleeping))
                _status.TryRemoveStatusEffect(ent, SleepingSystem.StatusEffectForcedSleeping);
    }

    #endregion

    #region Стрельба

    /// <summary>bonus_spread_values +10…+35: снаряды уходят в сторону.</summary>
    private void OnGunShot(Entity<GunComponent> ent, ref GunShotEvent args)
    {
        if (!HasComp<ImperialQuirkPoorAimComponent>(args.User))
            return;

        foreach (var (uid, _) in args.Ammo)
        {
            if (uid is not { } projectile || !TryComp<PhysicsComponent>(projectile, out var physics))
                continue;

            var spread = Angle.FromDegrees(_random.NextFloat(10f, 35f) / 2f * (_random.Prob(0.5f) ? 1 : -1));
            var velocity = spread.RotateVec(physics.LinearVelocity);
            _physics.SetLinearVelocity(projectile, velocity, body: physics);
            _transform.SetWorldRotation(projectile, velocity.ToWorldAngle() + Angle.FromDegrees(90));
        }
    }

    #endregion

    #region Социальная тревожность

    /// <summary>handle_speech: «ээ», «эмм» между словами, иногда персонаж замолкает.</summary>
    private void OnAnxietyAccent(EntityUid uid, ImperialQuirkSocialAnxietyComponent comp, AccentGetEvent args)
    {
        var ent = new Entity<ImperialQuirkSocialAnxietyComponent>(uid, comp);
        AnxietyAccent(ent, args);
    }

    private void AnxietyAccent(Entity<ImperialQuirkSocialAnxietyComponent> ent, AccentGetEvent args)
    {
        var mod = AnxietyMod(ent);
        var words = args.Message.Split(' ');
        var result = new List<string>();
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0 && _random.Prob(Math.Max(5f, mod) / 100f))
            {
                result.Add(_random.Pick(new[] { "ээ,", "эмм,", "ам," }));
                if (_random.Prob(Math.Min(5f, mod) / 100f))
                {
                    ent.Comp.SilencedUntil = _timing.CurTime + TimeSpan.FromSeconds(6);
                    _popup.PopupEntity(Loc.GetString("imperial-quirk-anxiety-stop"), ent, ent, PopupType.MediumCaution);
                    break;
                }
            }

            result.Add(words[i]);
        }

        args.Message = string.Join(' ', result);

        if (!_random.Prob(Math.Min(50f, 0.5f * mod) / 100f))
            return;

        if (ent.Comp.DumbThing)
        {
            ent.Comp.DumbThing = false;
            _popup.PopupEntity(Loc.GetString("imperial-quirk-anxiety-dumb"), ent, ent, PopupType.MediumCaution);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("imperial-quirk-anxiety-nothing"), ent, ent);
            if (_random.Prob(Math.Min(25f, 0.25f * mod) / 100f))
            {
                _popup.PopupEntity(Loc.GetString("imperial-quirk-anxiety-withdraw"), ent, ent, PopupType.MediumCaution);
                ent.Comp.SilencedUntil = _timing.CurTime + TimeSpan.FromSeconds(10);
            }
        }

        args.Message = _random.Pick(new[] { "Ээ.", "Эмм.", "Ам." });
    }

    /// <summary>calculate_mood_mod без настроения: 12.5 за каждого человека в трёх клетках.</summary>
    private float AnxietyMod(EntityUid mob)
    {
        var people = 0;
        foreach (var other in _lookup.GetEntitiesInRange<Content.Shared.Mind.Components.MindContainerComponent>(Transform(mob).Coordinates, 3f))
        {
            if (other.Owner != mob && other.Comp.HasMind && HasComp<Content.Shared.Humanoid.HumanoidProfileComponent>(other))
                people++;
        }

        return people * 12.5f;
    }

    private void OnAnxietySpeakAttempt(Entity<ImperialQuirkSocialAnxietyComponent> ent, ref SpeakAttemptEvent args)
    {
        if (_timing.CurTime < ent.Comp.SilencedUntil)
            args.Cancel();
    }

    /// <summary>eye_contact: взгляд в глаза при осмотре пугает.</summary>
    private void AnxietyEyeContact(EntityUid anxious, EntityUid other, bool examiner)
    {
        if (!_random.Prob(0.25f))
            return;

        var key = _random.Next(1, 4);
        switch (key)
        {
            case 1:
                _jitter.DoJitter(anxious, TimeSpan.FromSeconds(20), true);
                break;
            case 2:
                _stutter.DoStutter(anxious, TimeSpan.FromSeconds(6), true);
                break;
            default:
                _stun.TryUpdateStunDuration(anxious, TimeSpan.FromSeconds(2));
                break;
        }

        _popup.PopupEntity(Loc.GetString(examiner ? $"imperial-quirk-anxiety-look-{key}" : $"imperial-quirk-anxiety-looked-{key}",
            ("target", other)), anxious, anxious, PopupType.MediumCaution);
    }

    #endregion

    private void UpdateNegative(float frameTime)
    {
        var now = _timing.CurTime;

        var addicts = EntityQueryEnumerator<ImperialQuirkAddictionComponent>();
        while (addicts.MoveNext(out var uid, out var comp))
            UpdateAddiction((uid, comp), now);

        var allergies = EntityQueryEnumerator<ImperialQuirkMedicineAllergyComponent>();
        while (allergies.MoveNext(out var uid, out var comp))
            UpdateAllergy((uid, comp), now);

        var asthma = EntityQueryEnumerator<ImperialQuirkAsthmaComponent>();
        while (asthma.MoveNext(out var uid, out var comp))
            UpdateAsthma((uid, comp), now);

        var blood = EntityQueryEnumerator<ImperialQuirkBloodDeficiencyComponent>();
        while (blood.MoveNext(out var uid, out var comp))
            UpdateBloodDeficiency((uid, comp), now);

        var tumors = EntityQueryEnumerator<ImperialQuirkBrainTumorComponent>();
        while (tumors.MoveNext(out var uid, out var comp))
            UpdateBrainTumor((uid, comp), now);

        var illness = EntityQueryEnumerator<ImperialQuirkChronicIllnessComponent>();
        while (illness.MoveNext(out var uid, out var comp))
            UpdateChronicIllness((uid, comp), now);

        var eyes = EntityQueryEnumerator<ImperialQuirkFluorideStareComponent>();
        while (eyes.MoveNext(out var uid, out var comp))
            UpdateFluoride((uid, comp), now);

        var insanity = EntityQueryEnumerator<ImperialQuirkInsanityComponent>();
        while (insanity.MoveNext(out var uid, out var comp))
            UpdateInsanity((uid, comp), now);

        var narcolepsy = EntityQueryEnumerator<ImperialQuirkNarcolepsyComponent>();
        while (narcolepsy.MoveNext(out var uid, out var comp))
            UpdateNarcolepsy((uid, comp), now);
    }
}
