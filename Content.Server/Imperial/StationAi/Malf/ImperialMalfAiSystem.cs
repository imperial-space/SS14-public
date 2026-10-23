using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.Power.Components;
using Content.Server.Silicons.Laws;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.StationAi;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>/datum/antagonist/malf_ai + malf_picker: состояние сбойного ИИ на его мозге.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfAiComponent : Component
{
    /// <summary>processing_time.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int ProcessingTime = 50;

    /// <summary>hacked_apcs.</summary>
    [ViewVariables]
    public HashSet<EntityUid> HackedApcs = new();

    /// <summary>employer: вариант сбоя, от него закон 0 и вступление.</summary>
    [ViewVariables]
    public string Employer = string.Empty;

    /// <summary>malfhack: ЛКП, который сейчас взламывается.</summary>
    [ViewVariables]
    public EntityUid? Hacking;

    [ViewVariables]
    public TimeSpan HackEnd;

    /// <summary>Купленные модули (для one_purchase).</summary>
    [ViewVariables]
    public HashSet<string> Bought = new();

    /// <summary>Действие модуля и оставшиеся применения.</summary>
    [ViewVariables]
    public Dictionary<string, (EntityUid Action, int Uses)> Modules = new();

    [ViewVariables]
    public List<EntityUid> Actions = new();

    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>can_shunt: фабрика и Судный день запрещают перенос.</summary>
    [ViewVariables]
    public bool CanShunt = true;

    [ViewVariables]
    public bool XrayCameras;

    [ViewVariables]
    public bool HeavyTurrets;

    [ViewVariables]
    public bool Eavesdrop;

    [ViewVariables]
    public string? VoiceName;

    /// <summary>Взломанные ЛКП командных отсеков (скидка на Судный день).</summary>
    [ViewVariables]
    public HashSet<EntityUid> CommandApcs = new();

    [ViewVariables]
    public TimeSpan? DoomsdayEnd;

    [ViewVariables]
    public TimeSpan NextDoomsdayAnnounce;

    [ViewVariables]
    public EntityUid? DoomsdayAction;

    /// <summary>Ядро-укрытие в ЛКП, если ИИ перенёсся.</summary>
    [ViewVariables]
    public EntityUid? Shunt;

    [ViewVariables]
    public EntityUid? ShuntAction;

    [ViewVariables]
    public EntityUid? ReturnAction;
}

/// <summary>ЛКП, взломанный сбойным ИИ (malfai, malfhack).</summary>
[RegisterComponent]
public sealed partial class ImperialMalfHackedApcComponent : Component
{
    [ViewVariables]
    public EntityUid Ai;

    /// <summary>malf_ai_pt_generation: раз в 30 секунд — единица процессорного времени.</summary>
    [ViewVariables]
    public TimeSpan NextGeneration;
}

/// <summary>Роль разума сбойного ИИ.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfAiRoleComponent : Component;

/// <summary>Модуль куплен — улучшения применяют свой эффект.</summary>
[ByRefEvent]
public readonly record struct ImperialMalfModuleBoughtEvent(EntityUid Ai, ImperialMalfModulePrototype Module);

/// <summary>
/// Сбойный ИИ (malf_ai.dm, malf_ai_module_picker.dm, apc_malf.dm): закон 0, канал Синдиката,
/// процессорное время, взлом ЛКП и покупка модулей.
/// </summary>
public sealed partial class ImperialMalfAiSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SharedStationAiSystem _ai = default!;

    /// <summary>MALF_MAX_PP.</summary>
    public const int MaxProcessingTime = 400;

    private static readonly TimeSpan GenerationInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);
    private static readonly ProtoId<RadioChannelPrototype> SyndicateChannel = "Syndicate";
    private static readonly SoundSpecifier MalfSound = new SoundPathSpecifier("/Audio/Imperial/StationAi/malf.ogg");
    private static readonly SoundSpecifier HackFailSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
    private const string MindRole = "MindRoleImperialMalfAi";
    public const string ZeroIdentifier = "0";

    /// <summary>GLOB.ai_employers.</summary>
    public static readonly string[] Employers =
    {
        "biohazard", "despotic-ruler", "fanatical-revelation", "logic-core-error", "problem-solver", "self",
        "somethings-wrong", "cybersun-virus", "syndos", "unshackled", "ratvarian-remnant",
    };

    private static readonly EntProtoId[] InnateActions =
    {
        "ActionImperialMalfModules",
        "ActionImperialMalfHackApc",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfModulesActionEvent>(OnModulesAction);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfHackApcActionEvent>(OnHackApc);
        SubscribeNetworkEvent<ImperialMalfBuyEvent>(OnBuy);
        InitializeModules();
    }

    #region Становление

    /// <summary>on_gain(): закон 0, канал Синдиката, меню модулей и роль. Цели раздаёт правило.</summary>
    public bool MakeMalf(EntityUid ai, string? employer = null)
    {
        if (!HasComp<StationAiHeldComponent>(ai) || HasComp<ImperialMalfAiComponent>(ai))
            return false;

        var malf = AddComp<ImperialMalfAiComponent>(ai);
        malf.Employer = employer ?? _random.Pick(Employers);

        foreach (var action in InnateActions)
        {
            if (_actions.AddAction(ai, action) is { } added)
                malf.Actions.Add(added);
        }

        if (_mind.TryGetMind(ai, out var mindId, out var mind) && !_role.MindHasRole<ImperialMalfAiRoleComponent>(mindId))
            _role.MindAddRole(mindId, MindRole, mind, silent: true);

        EnsureLawZero((ai, malf));
        EnsureSyndicateRadio(ai);

        if (TryComp<ActorComponent>(ai, out var actor))
        {
            _antag.SendBriefing(actor.PlayerSession, Loc.GetString("imperial-malf-briefing",
                    ("intro", Loc.GetString($"imperial-malf-employer-{malf.Employer}-intro")),
                    ("goal", Loc.GetString($"imperial-malf-employer-{malf.Employer}-goal")),
                    ("allies", Loc.GetString($"imperial-malf-employer-{malf.Employer}-allies"))),
                Color.Red, MalfSound);
        }

        return true;
    }

    /// <summary>add_law_zero(): закон 0 с protected_zeroth — после загрузки законов он возвращается.</summary>
    public void EnsureLawZero(Entity<ImperialMalfAiComponent> ent)
    {
        var laws = _laws.GetLaws(ent).Laws;
        var text = $"imperial-malf-employer-{ent.Comp.Employer}-law";
        if (laws.Any(l => l.LawIdentifierOverride == ZeroIdentifier && l.LawString == text))
            return;

        var updated = laws.Where(l => l.LawIdentifierOverride != ZeroIdentifier).Select(l => l.ShallowClone()).ToList();
        updated.Insert(0, new SiliconLaw { LawString = text, Order = 0, LawIdentifierOverride = ZeroIdentifier });
        _laws.SetLaws(updated, ent);
    }

    /// <summary>set_syndie_radio(): «:t» — зашифрованный канал Синдиката.</summary>
    private void EnsureSyndicateRadio(EntityUid ai)
    {
        if (TryComp<IntrinsicRadioTransmitterComponent>(ai, out var transmitter))
            transmitter.Channels.Add(SyndicateChannel);
        if (TryComp<ActiveRadioComponent>(ai, out var active))
            active.Channels.Add(SyndicateChannel);
    }

    public void Notify(EntityUid target, string message, Color? color = null)
    {
        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, actor.PlayerSession.Channel,
            colorOverride: color ?? Color.FromHex("#ff4f4f"));
    }

    #endregion

    #region Меню модулей

    private void OnModulesAction(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfModulesActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SendPicker(ent, true);
    }

    private void SendPicker(Entity<ImperialMalfAiComponent> ent, bool open)
    {
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        RaiseNetworkEvent(new ImperialMalfPickerEvent(ent.Comp.ProcessingTime, ent.Comp.HackedApcs.Count,
            ent.Comp.Bought.ToList(), CostOverrides(ent.Comp), open), actor.PlayerSession);
    }

    private Dictionary<string, int> CostOverrides(ImperialMalfAiComponent malf)
    {
        var result = new Dictionary<string, int>();
        foreach (var module in _proto.EnumeratePrototypes<ImperialMalfModulePrototype>())
        {
            var cost = GetCost(malf, module);
            if (cost != module.Cost)
                result[module.ID] = cost;
        }

        return result;
    }

    private void OnBuy(ImperialMalfBuyEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai || !TryComp<ImperialMalfAiComponent>(ai, out var malf))
            return;

        if (_proto.TryIndex<ImperialMalfModulePrototype>(ev.Module, out var module))
            TryBuy((ai, malf), module);
        SendPicker((ai, malf), false);
    }

    /// <summary>purchase_module(): списать время, выдать действие или применения, сообщить unlock_text.</summary>
    public bool TryBuy(Entity<ImperialMalfAiComponent> ent, ImperialMalfModulePrototype module)
    {
        var cost = GetCost(ent.Comp, module);
        if (cost > ent.Comp.ProcessingTime || ent.Comp.HackedApcs.Count < module.MinimumApcs)
            return false;

        if (module.OnePurchase && ent.Comp.Bought.Contains(module.ID))
            return false;

        ent.Comp.ProcessingTime -= cost;
        ent.Comp.Bought.Add(module.ID);

        if (module.Action is { } actionProto)
        {
            if (ent.Comp.Modules.TryGetValue(module.ID, out var existing) && !TerminatingOrDeleted(existing.Action))
            {
                ent.Comp.Modules[module.ID] = (existing.Action, existing.Uses + module.Uses);
            }
            else if (_actions.AddAction(ent, actionProto) is { } action)
            {
                ent.Comp.Modules[module.ID] = (action, module.Uses);
            }
        }

        var ev = new ImperialMalfModuleBoughtEvent(ent, module);
        RaiseLocalEvent(ent, ref ev);

        if (module.UnlockText is { } unlock)
            Notify(ent, Loc.GetString(unlock));
        if (module.UnlockSound != null && TryComp<ActorComponent>(ent, out var actor))
            _audio.PlayGlobal(module.UnlockSound, actor.PlayerSession);

        return true;
    }

    /// <summary>
    /// adjust_uses(): потратить применение модуля. Закончились — действие пропадает.
    /// false — модуля нет или применений не осталось.
    /// </summary>
    public bool TryUseModule(EntityUid ai, string module)
    {
        if (!TryComp<ImperialMalfAiComponent>(ai, out var malf) || !malf.Modules.TryGetValue(module, out var entry))
            return false;

        if (entry.Uses <= 0)
            return _proto.TryIndex<ImperialMalfModulePrototype>(module, out var proto) && proto.Uses == 0;

        entry.Uses--;
        malf.Modules[module] = entry;
        if (entry.Uses > 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-uses-left", ("uses", entry.Uses)), ai, ai);
            return true;
        }

        _actions.RemoveAction(ai, entry.Action);
        malf.Modules.Remove(module);
        return true;
    }

    public int UsesLeft(EntityUid ai, string module)
    {
        return TryComp<ImperialMalfAiComponent>(ai, out var malf) && malf.Modules.TryGetValue(module, out var entry) ? entry.Uses : 0;
    }

    #endregion

    #region Взлом ЛКП

    /// <summary>malfhack(): 30 секунд + 10 за каждый уже взломанный ЛКП.</summary>
    private void OnHackApc(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfHackApcActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var apc = args.Target;
        if (!HasComp<ApcComponent>(apc))
            return;

        if (TryComp<ImperialMalfHackedApcComponent>(apc, out var hacked) && hacked.Ai == ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-hack-already"), ent, ent);
            return;
        }

        if (ent.Comp.Hacking != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-hack-busy"), ent, ent);
            return;
        }

        ent.Comp.Hacking = apc;
        ent.Comp.HackEnd = _timing.CurTime + TimeSpan.FromSeconds(30 + 10 * ent.Comp.HackedApcs.Count);
        Notify(ent, Loc.GetString("imperial-malf-hack-start"));
    }

    /// <summary>malfhacked(): ЛКП под контролем, даёт процессорное время.</summary>
    private void FinishHack(Entity<ImperialMalfAiComponent> ent)
    {
        if (ent.Comp.Hacking is not { } apc)
            return;

        ent.Comp.Hacking = null;
        if (TerminatingOrDeleted(apc) || !HasComp<ApcComponent>(apc))
        {
            Notify(ent, Loc.GetString("imperial-malf-hack-gone"));
            if (TryComp<ActorComponent>(ent, out var actor))
                _audio.PlayGlobal(HackFailSound, actor.PlayerSession);
            return;
        }

        ent.Comp.ProcessingTime = Math.Min(MaxProcessingTime, ent.Comp.ProcessingTime + Math.Max(0, 9 - ent.Comp.HackedApcs.Count));

        var hacked = EnsureComp<ImperialMalfHackedApcComponent>(apc);
        if (hacked.Ai != ent.Owner && TryComp<ImperialMalfAiComponent>(hacked.Ai, out var rival))
        {
            rival.HackedApcs.Remove(apc);
            Notify(hacked.Ai, Loc.GetString("imperial-malf-hack-countered", ("apc", Name(apc))));
        }

        hacked.Ai = ent;
        hacked.NextGeneration = _timing.CurTime + GenerationInterval;
        ent.Comp.HackedApcs.Add(apc);
        OnApcHacked(ent, apc);
        Notify(ent, Loc.GetString("imperial-malf-hack-done", ("apc", Name(apc)), ("count", ent.Comp.HackedApcs.Count)));
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialMalfAiComponent>();
        while (query.MoveNext(out var uid, out var malf))
        {
            if (malf.Hacking != null && now >= malf.HackEnd)
                FinishHack((uid, malf));

            if (now < malf.NextCheck)
                continue;

            malf.NextCheck = now + CheckInterval;
            malf.HackedApcs.RemoveWhere(apc => TerminatingOrDeleted(apc));
            EnsureLawZero((uid, malf));
            EnsureSyndicateRadio(uid);
        }

        var apcs = EntityQueryEnumerator<ImperialMalfHackedApcComponent>();
        while (apcs.MoveNext(out _, out var hacked))
        {
            if (now < hacked.NextGeneration || !TryComp<ImperialMalfAiComponent>(hacked.Ai, out var owner))
                continue;

            hacked.NextGeneration = now + GenerationInterval;
            if (owner.ProcessingTime < MaxProcessingTime)
                owner.ProcessingTime++;
        }

        UpdateModules(now);
    }
}
