using System.Linq;
using Content.Server.Actions;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Генокрад TG (code/modules/antagonists/changeling): химикаты, магазин эволюции, профили ДНК и способности.
/// Профили хранятся ванильным хранилищем личностей SS14 (копии жертв на паузной карте).
/// </summary>
public sealed partial class ImperialChangelingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedChangelingIdentitySystem _identity = default!;

    public static readonly ProtoId<RadioChannelPrototype> HiveChannel = "ImperialChangelingHive";
    private static readonly EntProtoId EmporiumAction = "ActionImperialChangelingEmporium";

    /// <summary>GLOB.greek_letters.</summary>
    private static readonly string[] GreekLetters =
    {
        "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa", "Lambda", "Mu",
        "Nu", "Xi", "Omicron", "Pi", "Rho", "Sigma", "Tau", "Upsilon", "Phi", "Chi", "Psi", "Omega",
    };

    /// <summary>strings/names/hive_names.txt.</summary>
    private static readonly string[] HiveNames =
    {
        "Алеф", "Айин", "Бет", "Далет", "Дигг", "Гами", "Гимел", "Хе", "Хет", "Хлл", "Кап", "Ламед", "Мем", "Нахс",
        "Нун", "Пе", "Пит", "Коф", "Реш", "Самех", "Шин", "Симс", "Таф", "Тау", "Тет", "Цейд", "Вав", "Вет", "Йод", "Заин", "Цик",
    };

    /// <summary>possible_changeling_IDs: общий на раунд пул букв.</summary>
    private readonly List<string> _freeIds = new();

    /// <summary>Способности: обработчик sting_action(user, target) → успех.</summary>
    private readonly Dictionary<string, Func<Entity<ImperialChangelingComponent>, EntityUid?, bool>> _powers = new();

    /// <summary>Перед проверками: повторное нажатие втягивает оружие или сбрасывает костюм бесплатно.</summary>
    private readonly Dictionary<string, Func<Entity<ImperialChangelingComponent>, bool>> _powerPreUse = new();

    /// <summary>on_purchase(): что сделать при получении способности.</summary>
    private readonly Dictionary<string, Action<Entity<ImperialChangelingComponent>>> _powerGranted = new();

    /// <summary>Что сделать при потере способности (Remove()).</summary>
    private readonly Dictionary<string, Action<Entity<ImperialChangelingComponent>>> _powerRemoved = new();

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialChangelingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingPowerActionEvent>(OnPowerAction);
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingTargetPowerActionEvent>(OnTargetPowerAction);
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingEmporiumActionEvent>(OnEmporiumAction);
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingEvolveMessage>(OnEvolve);
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingReadaptMessage>(OnReadapt);
        SubscribeLocalEvent<ImperialChangelingComponent, EntitySpokeEvent>(OnSpoke, before: new[] { typeof(RadioSystem) });
        SubscribeLocalEvent<ImperialChangelingComponent, TransformSpeakerNameEvent>(OnSpeakerName);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _freeIds.Clear());

        InitializeInnate();
        InitializeStings();
        InitializeMutations();
        InitializeUtility();
        InitializeBodies();
        InitializeUi();
        InitializeChoice();
        InitializeDetails();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<ImperialChangelingSystem>();
    }

    #region Создание

    /// <summary>on_gain(): позывной, магазин, врождённые способности, исходный профиль.</summary>
    public ImperialChangelingComponent MakeChangeling(EntityUid body, bool headslug = false)
    {
        if (TryComp<ImperialChangelingComponent>(body, out var existing))
            return existing;

        var comp = AddComp<ImperialChangelingComponent>(body);
        comp.Headslug = headslug;
        if (headslug)
        {
            // /datum/antagonist/changeling/headslug
            comp.GeneticPoints = comp.TotalGeneticPoints = 5;
            comp.Chemicals = 10;
            comp.MaxChemicals = 50;
        }

        comp.ChangelingId = GenerateId(body);
        comp.HiveName = _random.Pick(HiveNames) + "-hive";

        var ent = (body, comp);
        _actions.AddAction(body, ref comp.EmporiumAction, EmporiumAction);

        var ui = EnsureComp<UserInterfaceComponent>(body);
        _ui.SetUi((body, ui), ImperialChangelingUiKey.Emporium, new InterfaceData("ImperialChangelingEmporiumBoundUserInterface"));
        _ui.SetUi((body, ui), ImperialChangelingUiKey.Transform, new InterfaceData("ImperialChangelingTransformBoundUserInterface"));

        foreach (var power in _proto.EnumeratePrototypes<ImperialChangelingPowerPrototype>())
        {
            if (!power.Innate)
                continue;

            comp.Innate.Add(power.ID);
            GrantAction(ent, power);
        }

        GrantInfo(ent);
        CreateInitialProfile(ent);
        AddHivemind(body);
        Dirty(body, comp);
        SyncMind(ent);
        Greet(ent);
        return comp;
    }

    /// <summary>generate_name(): Mr./Ms./Mx. + греческая буква.</summary>
    private string GenerateId(EntityUid body)
    {
        var honorific = "Mx.";
        if (TryComp<Content.Shared.Humanoid.HumanoidProfileComponent>(body, out var profile))
        {
            honorific = profile.Gender switch
            {
                Robust.Shared.Enums.Gender.Female => "Ms.",
                Robust.Shared.Enums.Gender.Male => "Mr.",
                _ => "Mx.",
            };
        }

        if (_freeIds.Count == 0 && !_idsInitialized)
        {
            _idsInitialized = true;
            _freeIds.AddRange(GreekLetters);
        }

        if (_freeIds.Count == 0)
            return $"{honorific} {_random.Next(1, 1000)}";

        var letter = _random.PickAndTake(_freeIds);
        return $"{honorific} {letter}";
    }

    private bool _idsInitialized;

    private void OnShutdown(Entity<ImperialChangelingComponent> ent, ref ComponentShutdown args)
    {
        RemovePowers(ent, includeInnate: true);
        _actions.RemoveAction(ent.Owner, ent.Comp.EmporiumAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.InfoAction);
        RemoveHivemind(ent);
    }

    #endregion

    #region Химикаты

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        // on_life(): раз в секунду вместо тика SSmobs.
        _nextTick = now + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<ImperialChangelingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            if (_mobState.IsDead(uid) && !comp.FakeDeath)
                AdjustChemicals(ent, comp.RechargeRate - comp.RechargeSlowdown, comp.MaxChemicals * 0.5f);
            else if (TryComp<FlammableComponent>(uid, out var flammable) && flammable.OnFire)
                AdjustChemicals(ent, comp.RechargeRate - 0.75f);
            else
                AdjustChemicals(ent, comp.RechargeRate - comp.RechargeSlowdown);

            UpdateInnate(ent, now);
            UpdateUtility(ent, now);
        }

        UpdateStings(now);
        UpdateMutations(now);
        UpdateMelt(now);
        UpdateBodies(now);
        UpdateDetails(now);
    }

    /// <summary>adjust_chemicals(): зажать между 0 и пределом.</summary>
    public void AdjustChemicals(Entity<ImperialChangelingComponent> ent, float amount, float? cap = null)
    {
        var old = ent.Comp.Chemicals;
        ent.Comp.Chemicals = Math.Clamp(ent.Comp.Chemicals + amount, 0, cap ?? ent.Comp.MaxChemicals);
        if (Math.Abs(old - ent.Comp.Chemicals) > 0.001f)
            Dirty(ent);
    }

    #endregion

    #region Способности

    /// <summary>on_purchase() → Grant().</summary>
    private void GrantAction(Entity<ImperialChangelingComponent> ent, ImperialChangelingPowerPrototype power)
    {
        if (power.Action is not { } actionProto || ent.Comp.Actions.ContainsKey(power.ID))
            return;

        if (_actions.AddAction(ent.Owner, actionProto) is { } action)
            ent.Comp.Actions[power.ID] = action;
    }

    private void RemoveAction(Entity<ImperialChangelingComponent> ent, string power)
    {
        if (_powerRemoved.TryGetValue(power, out var removed))
            removed(ent);

        if (ent.Comp.Actions.Remove(power, out var action))
            _actions.RemoveAction(ent.Owner, action);
    }

    /// <summary>remove_changeling_powers().</summary>
    private void RemovePowers(Entity<ImperialChangelingComponent> ent, bool includeInnate)
    {
        ent.Comp.ChosenSting = null;
        foreach (var power in ent.Comp.Purchased.ToList())
        {
            RemoveAction(ent, power);
        }

        ent.Comp.Purchased.Clear();
        if (includeInnate)
        {
            foreach (var power in ent.Comp.Innate.ToList())
            {
                RemoveAction(ent, power);
            }

            ent.Comp.Innate.Clear();
        }

        ent.Comp.GeneticPoints = ent.Comp.TotalGeneticPoints;
        ent.Comp.Chemicals = Math.Min(ent.Comp.Chemicals, ent.Comp.MaxChemicals);
        ent.Comp.RechargeRate = 1;
        ent.Comp.RechargeSlowdown = 0;
        Dirty(ent);
    }

    private void OnPowerAction(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingPowerActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryUsePower(ent, args.Power, null);
    }

    private void OnTargetPowerAction(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingTargetPowerActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryUsePower(ent, args.Power, args.Target);
    }

    /// <summary>try_to_sting(): проверки, sting_action(), списание химикатов.</summary>
    public bool TryUsePower(Entity<ImperialChangelingComponent> ent, ProtoId<ImperialChangelingPowerPrototype> powerId, EntityUid? target)
    {
        if (!_proto.TryIndex(powerId, out var power) || !_powers.TryGetValue(power.ID, out var action))
            return false;

        if (_powerPreUse.TryGetValue(power.ID, out var preUse) && preUse(ent))
            return false;

        // sting/Trigger(): взвод жала не проверяет can_sting().
        if (_stings.ContainsKey(power.ID))
            return ToggleSting(ent, power.ID);

        var cost = GetChemicalCost(ent, power);
        if (!CanUsePower(ent, power, cost))
            return false;

        if (power.DisabledByFire && TryComp<FlammableComponent>(ent, out var flammable) && flammable.OnFire)
        {
            Popup(ent, "imperial-changeling-on-fire");
            return false;
        }

        if (!action(ent, target))
            return false;

        AdjustChemicals(ent, -cost);
        return true;
    }

    /// <summary>can_sting() → can_be_used_by().</summary>
    private bool CanUsePower(Entity<ImperialChangelingComponent> ent, ImperialChangelingPowerPrototype power, float cost)
    {
        if (!HasComp<HumanoidProfileComponent>(ent) && !IsLesserForm(ent))
            return false;

        if (power.ReqHuman && IsLesserForm(ent))
        {
            Popup(ent, "imperial-changeling-req-human");
            return false;
        }

        if (ent.Comp.Chemicals < cost)
        {
            Popup(ent, "imperial-changeling-req-chems", ("amount", cost));
            return false;
        }

        if (ent.Comp.AbsorbedCount < power.ReqDna)
        {
            Popup(ent, "imperial-changeling-req-dna", ("amount", power.ReqDna));
            return false;
        }

        if (ent.Comp.TrueAbsorbs < power.ReqAbsorbs)
        {
            Popup(ent, "imperial-changeling-req-absorbs", ("amount", power.ReqAbsorbs));
            return false;
        }

        if (GetStat(ent) > power.ReqStat)
        {
            Popup(ent, "imperial-changeling-incapacitated");
            return false;
        }

        if (ent.Comp.FakeDeath && !power.IgnoresFakedeath)
        {
            Popup(ent, "imperial-changeling-faking-death");
            return false;
        }

        return true;
    }

    private ImperialChangelingStat GetStat(EntityUid uid)
    {
        if (_mobState.IsDead(uid))
            return ImperialChangelingStat.Dead;

        return _mobState.IsCritical(uid) ? ImperialChangelingStat.HardCrit : ImperialChangelingStat.Conscious;
    }

    /// <summary>ismonkey(): низшая форма (этап Г4).</summary>
    public bool IsLesserForm(EntityUid uid)
    {
        return HasComp<ImperialChangelingLesserFormComponent>(uid);
    }

    private void Popup(EntityUid uid, string loc, params (string, object)[] args)
    {
        _popup.PopupEntity(Loc.GetString(loc, args), uid, uid);
    }

    #endregion

    #region Магазин эволюции

    private void OnEmporiumAction(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingEmporiumActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.TryToggleUi(ent.Owner, ImperialChangelingUiKey.Emporium, ent.Owner);
    }

    private void OnEvolve(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingEvolveMessage args)
    {
        if (_proto.TryIndex<ImperialChangelingPowerPrototype>(args.Power, out var power))
            PurchasePower(ent, power);
    }

    private void OnReadapt(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingReadaptMessage args)
    {
        if (ent.Comp.CanRespec > 0)
            Readapt(ent);
    }

    /// <summary>purchase_power().</summary>
    public bool PurchasePower(Entity<ImperialChangelingComponent> ent, ImperialChangelingPowerPrototype power)
    {
        if (ent.Comp.Purchased.Contains(power.ID))
        {
            Popup(ent, "imperial-changeling-evolve-already");
            return false;
        }

        if (ent.Comp.GeneticPoints < power.DnaCost)
        {
            Popup(ent, "imperial-changeling-evolve-points");
            return false;
        }

        if (ent.Comp.AbsorbedCount < power.ReqDna)
        {
            Popup(ent, "imperial-changeling-evolve-dna");
            return false;
        }

        if (ent.Comp.TrueAbsorbs < power.ReqAbsorbs)
        {
            Popup(ent, "imperial-changeling-evolve-absorbs");
            return false;
        }

        if (power.DnaCost < 0 || power.Innate)
        {
            Popup(ent, "imperial-changeling-evolve-cannot");
            return false;
        }

        // TRAIT_DEATHCOMA
        if (ent.Comp.FakeDeath)
        {
            Popup(ent, "imperial-changeling-evolve-deathcoma");
            return false;
        }

        GivePower(ent, power);
        ent.Comp.GeneticPoints -= power.DnaCost;
        Dirty(ent);
        SyncMind(ent);
        return true;
    }

    /// <summary>give_power().</summary>
    public void GivePower(Entity<ImperialChangelingComponent> ent, ImperialChangelingPowerPrototype power)
    {
        ent.Comp.Purchased.Add(power.ID);
        GrantAction(ent, power);
        if (_powerGranted.TryGetValue(power.ID, out var granted))
            granted(ent);

        // augmented_eyesight/on_purchase()
        if (power.ID == EyesightPower)
            Popup(ent, "imperial-changeling-eyesight-protect");
        Dirty(ent);
    }

    /// <summary>readapt().</summary>
    public bool Readapt(Entity<ImperialChangelingComponent> ent)
    {
        if (!HasComp<HumanoidProfileComponent>(ent) || IsLesserForm(ent))
        {
            Popup(ent, "imperial-changeling-readapt-form");
            return false;
        }

        if (ent.Comp.FakeDeath)
        {
            Popup(ent, "imperial-changeling-readapt-busy");
            return false;
        }

        if (ent.Comp.CanRespec <= 0)
        {
            Popup(ent, "imperial-changeling-readapt-power");
            return false;
        }

        Popup(ent, "imperial-changeling-readapt-done");
        RemovePowers(ent, includeInnate: false);
        ent.Comp.CanRespec--;
        Dirty(ent);
        return true;
    }

    #endregion

    #region Улей

    /// <summary>Генокрады, у которых мы сами завели передатчик (его надо убрать вместе с ульем).</summary>
    private readonly HashSet<EntityUid> _ownTransmitter = new();

    private readonly HashSet<EntityUid> _ownReceiver = new();

    /// <summary>Кто прямо сейчас говорит в улей: имя в радио — позывной.</summary>
    private EntityUid? _hiveSpeaker;

    /// <summary>TRAIT_CHANGELING_HIVEMIND: канал улья (say :е).</summary>
    private void AddHivemind(EntityUid body)
    {
        if (!HasComp<IntrinsicRadioTransmitterComponent>(body))
        {
            var transmitter = AddComp<IntrinsicRadioTransmitterComponent>(body);
            // По умолчанию там общий канал — без гарнитуры его быть не должно.
            transmitter.Channels.Clear();
            _ownTransmitter.Add(body);
        }

        if (!HasComp<IntrinsicRadioReceiverComponent>(body))
        {
            AddComp<IntrinsicRadioReceiverComponent>(body);
            _ownReceiver.Add(body);
        }

        Comp<IntrinsicRadioTransmitterComponent>(body).Channels.Add(HiveChannel);
        EnsureComp<ActiveRadioComponent>(body).Channels.Add(HiveChannel);
    }

    private void RemoveHivemind(EntityUid body)
    {
        if (TryComp<IntrinsicRadioTransmitterComponent>(body, out var transmitter))
            transmitter.Channels.Remove(HiveChannel);

        if (TryComp<ActiveRadioComponent>(body, out var active))
            active.Channels.Remove(HiveChannel);

        if (_ownTransmitter.Remove(body))
            RemComp<IntrinsicRadioTransmitterComponent>(body);

        if (_ownReceiver.Remove(body))
            RemComp<IntrinsicRadioReceiverComponent>(body);
    }

    private void OnSpoke(EntityUid uid, ImperialChangelingComponent comp, EntitySpokeEvent args)
    {
        _hiveSpeaker = args.Channel is { } channel && channel.ID == HiveChannel.Id ? uid : null;
    }

    private void OnSpeakerName(EntityUid uid, ImperialChangelingComponent comp, TransformSpeakerNameEvent args)
    {
        if (_hiveSpeaker != uid)
        {
            // override_voice: имитация голоса.
            if (comp.MimicVoice != null)
                args.VoiceName = comp.MimicVoice;
            return;
        }

        // saymode/changeling: <b>[changelingID]:</b>
        _hiveSpeaker = null;
        args.VoiceName = comp.ChangelingId;
    }

    #endregion

    #region Админ

    /// <summary>changelingdna: очки генов (и общий запас для переадаптации), поглощения дают и ДНК, и переадаптацию.</summary>
    public void GiveDna(Entity<ImperialChangelingComponent> ent, int dna, int absorbs)
    {
        ent.Comp.GeneticPoints = Math.Max(0, ent.Comp.GeneticPoints + dna);
        ent.Comp.TotalGeneticPoints = Math.Max(0, ent.Comp.TotalGeneticPoints + dna);
        if (absorbs != 0)
        {
            ent.Comp.TrueAbsorbs = Math.Max(0, ent.Comp.TrueAbsorbs + absorbs);
            ent.Comp.AbsorbedCount = Math.Max(0, ent.Comp.AbsorbedCount + absorbs);
            ent.Comp.CanRespec = Math.Max(0, ent.Comp.CanRespec + absorbs);
        }

        Dirty(ent);
        SyncMind(ent);
        UpdateInfoUi(ent);
    }

    #endregion

    #region Разум

    /// <summary>Зеркалит счётчики на разум: цели и итоги раунда переживают тело.</summary>
    public void SyncMind(Entity<ImperialChangelingComponent> ent)
    {
        if (!_mind.TryGetMind(ent.Owner, out var mindId, out _))
            return;

        var data = EnsureComp<ImperialChangelingMindComponent>(mindId);
        data.AbsorbedCount = ent.Comp.AbsorbedCount;
        data.EffectivePoints = ent.Comp.GeneticPoints;
        foreach (var power in ent.Comp.Purchased)
        {
            if (_proto.TryIndex(power, out var proto) && proto.DnaCost > 0)
                data.EffectivePoints += proto.DnaCost;
        }
    }

    #endregion

    #region Профили

    /// <summary>create_initial_profile(): исходный облик — первый защищённый профиль.</summary>
    private void CreateInitialProfile(Entity<ImperialChangelingComponent> ent)
    {
        if (!HasComp<HumanoidProfileComponent>(ent))
            return;

        var identity = EnsureComp<ChangelingIdentityComponent>(ent);
        if (identity.CurrentIdentity is not { } clone)
        {
            if (_identity.CloneToPausedMap((ent.Owner, identity), ent.Owner) is not { } made)
                return;

            clone = made;
            identity.CurrentIdentity = clone;
            Dirty(ent.Owner, identity);
        }

        ent.Comp.FirstProfile = clone;
        ent.Comp.CurrentProfile = clone;
        ent.Comp.Profiles.Add(clone);
        ent.Comp.AbsorbedCount++;
    }

    /// <summary>add_new_profile() → create_profile() + add_profile().</summary>
    public EntityUid? AddNewProfile(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        if (ent.Comp.Profiles.Count > ent.Comp.DnaMax && !PushOutProfile(ent))
            return null;

        var identity = EnsureComp<ChangelingIdentityComponent>(ent);
        if (_identity.CloneToPausedMap((ent.Owner, identity), target) is not { } clone)
            return null;

        if (ent.Comp.FirstProfile == null)
        {
            ent.Comp.FirstProfile = clone;
            ent.Comp.CurrentProfile = clone;
        }

        ent.Comp.Profiles.Add(clone);
        ent.Comp.AbsorbedCount++;
        Dirty(ent);
        SyncMind(ent);
        return clone;
    }

    /// <summary>push_out_profile(): вытесняет старейший незащищённый профиль.</summary>
    private bool PushOutProfile(Entity<ImperialChangelingComponent> ent)
    {
        foreach (var profile in ent.Comp.Profiles)
        {
            if (profile == ent.Comp.FirstProfile)
                continue;

            ent.Comp.Profiles.Remove(profile);
            _identity.DropStoredIdentity(ent.Owner, profile);
            Dirty(ent);
            return true;
        }

        return false;
    }

    /// <summary>has_profile_with_dna().</summary>
    public bool HasProfileWithDna(Entity<ImperialChangelingComponent> ent, string? dna)
    {
        if (dna == null)
            return false;

        foreach (var profile in ent.Comp.Profiles)
        {
            if (TryComp<Content.Shared.Forensics.Components.DnaComponent>(profile, out var stored) && stored.DNA == dna)
                return true;
        }

        return false;
    }

    #endregion
}
