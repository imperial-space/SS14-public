using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Lathe;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Research.Prototypes;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi;

/// <summary>connected_ai: киборг подключён к ИИ и повторяет его законы (lawupdate).</summary>
[RegisterComponent]
public sealed partial class ImperialAiBorgLinkComponent : Component
{
    [ViewVariables]
    public EntityUid? Ai;

    /// <summary>Подпись законов на момент последней синхронизации.</summary>
    [ViewVariables]
    public string? LawSignature;

    /// <summary>Связь разорвана (эмаг, смерть ИИ) — сама не восстанавливается.</summary>
    [ViewVariables]
    public bool Severed;
}

/// <summary>/obj/item/borg/upgrade/ai: модуль Б.О.Р.И.С. вместо мозга делает корпус оболочкой ИИ.</summary>
[RegisterComponent]
public sealed partial class ImperialAiShellBoardComponent : Component;

/// <summary>deployed: ИИ сейчас в этой оболочке.</summary>
[RegisterComponent]
public sealed partial class ImperialAiDeployedComponent : Component
{
    [ViewVariables]
    public EntityUid Ai;

    [ViewVariables]
    public EntityUid Mind;

    [ViewVariables]
    public EntityUid? ReturnAction;

    [ViewVariables]
    public string OldName = string.Empty;
}

/// <summary>Фабрикатор экзокостюмов умеет печатать модуль Б.О.Р.И.С.</summary>
[RegisterComponent]
public sealed partial class ImperialAiShellLatheComponent : Component;

/// <summary>
/// Киборги ИИ (robot.dm, ai.dm): подключение к ИИ с меньшим числом боргов, синхронизация законов,
/// панель киборгов, оболочки Б.О.Р.И.С. (deploy_to_shell / undeploy).
/// </summary>
public sealed partial class ImperialStationAiSystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedBorgSystem _borg = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly Content.Shared.NameModifier.EntitySystems.NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly Content.Shared.Mobs.Systems.MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly Content.Shared.Damage.Systems.DamageableSystem _damageable = default!;

    private static readonly TimeSpan BorgInterval = TimeSpan.FromSeconds(2);
    private static readonly EntProtoId ShellReturnAction = "ActionImperialAiShellReturn";
    private static readonly ProtoId<LatheRecipePrototype> ShellRecipe = "ImperialAiShellBoardRecipe";
    private static readonly HashSet<string> ShellLathes = new() { "ExosuitFabricator" };
    private const string SyndicateFaction = "Syndicate";

    private TimeSpan _nextBorgUpdate;

    private void InitializeBorgs()
    {
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiBorgsActionEvent>(OnBorgsAction);
        SubscribeLocalEvent<ImperialAiDeployedComponent, ImperialAiShellReturnActionEvent>(OnShellReturn);
        SubscribeLocalEvent<ImperialAiDeployedComponent, EntRemovedFromContainerMessage>(OnShellBoardRemoved);
        SubscribeLocalEvent<ImperialAiDeployedComponent, ComponentShutdown>(OnDeployedShutdown);
        SubscribeLocalEvent<ImperialAiShellLatheComponent, LatheGetRecipesEvent>(OnShellLatheRecipes);
        SubscribeNetworkEvent<ImperialAiBorgCommandEvent>(OnBorgCommand);
    }

    private void OnShellLatheRecipes(Entity<ImperialAiShellLatheComponent> ent, ref LatheGetRecipesEvent args)
    {
        args.Recipes.Add(ShellRecipe);
    }

    #region Связь и законы

    private static bool IsShell(BorgChassisComponent chassis, IEntityManager entities)
    {
        return chassis.BrainEntity is { } brain && entities.HasComponent<ImperialAiShellBoardComponent>(brain);
    }

    private void UpdateBorgs(TimeSpan now)
    {
        if (now < _nextBorgUpdate)
            return;

        _nextBorgUpdate = now + BorgInterval;

        // Фабрикаторы экзокостюмов получают рецепт Б.О.Р.И.С.
        var lathes = EntityQueryEnumerator<LatheComponent, MetaDataComponent>();
        while (lathes.MoveNext(out var lathe, out _, out var meta))
        {
            if (meta.EntityPrototype is { } proto && ShellLathes.Contains(proto.ID) && !HasComp<ImperialAiShellLatheComponent>(lathe))
                AddComp<ImperialAiShellLatheComponent>(lathe);
        }

        var query = EntityQueryEnumerator<BorgChassisComponent, SiliconLawProviderComponent, TransformComponent>();
        while (query.MoveNext(out var borg, out var chassis, out var provider, out var xform))
        {
            var shell = IsShell(chassis, EntityManager);
            var hasPlayer = TryComp<MindContainerComponent>(borg, out var minds) && minds.HasMind;
            if (!hasPlayer && !shell)
                continue;

            var link = EnsureComp<ImperialAiBorgLinkComponent>(borg);
            if (link.Severed)
                continue;

            // emag_act(): взломанный борг теряет связь с ИИ.
            if (provider.Subverted || _faction.IsMember(borg, SyndicateFaction))
            {
                SeverLink((borg, link), notifyAi: true);
                continue;
            }

            if (link.Ai is { } ai && (TerminatingOrDeleted(ai) || !HasComp<ImperialStationAiComponent>(ai)))
            {
                SeverLink((borg, link), notifyAi: false);
                continue;
            }

            if (link.Ai == null && !TryConnect((borg, link), xform))
                continue;

            SyncLaws((borg, link));
        }
    }

    /// <summary>select_active_ai_with_fewest_borgs(): ИИ той же станции, у которого меньше всех киборгов.</summary>
    private bool TryConnect(Entity<ImperialAiBorgLinkComponent> borg, TransformComponent xform)
    {
        var station = _station.GetOwningStation(borg, xform);
        if (station == null)
            return false;

        var counts = new Dictionary<EntityUid, int>();
        var ais = EntityQueryEnumerator<ImperialStationAiComponent>();
        while (ais.MoveNext(out var ai, out _))
        {
            if (CoreStation(ai) == station)
                counts[ai] = 0;
        }

        if (counts.Count == 0)
            return false;

        var links = EntityQueryEnumerator<ImperialAiBorgLinkComponent>();
        while (links.MoveNext(out _, out var other))
        {
            if (other.Ai is { } linked && counts.ContainsKey(linked))
                counts[linked]++;
        }

        var chosen = counts.MinBy(p => p.Value).Key;
        borg.Comp.Ai = chosen;
        borg.Comp.LawSignature = null;
        Notify(chosen, Loc.GetString("imperial-ai-borg-connected", ("borg", Name(borg))));
        Notify(borg, Loc.GetString("imperial-ai-borg-connected-self", ("ai", Name(chosen))));
        return true;
    }

    private void SeverLink(Entity<ImperialAiBorgLinkComponent> borg, bool notifyAi)
    {
        if (notifyAi && borg.Comp.Ai is { } ai && !TerminatingOrDeleted(ai))
            Notify(ai, Loc.GetString("imperial-ai-borg-severed", ("borg", Name(borg))));

        borg.Comp.Ai = null;
        borg.Comp.Severed = true;
    }

    private static string Signature(SiliconLawset laws)
    {
        return string.Join("|", laws.Laws.Select(l => $"{l.Order}:{l.LawIdentifierOverride}:{l.LawString}"));
    }

    /// <summary>lawsync(): законы борга — копия законов ИИ.</summary>
    private void SyncLaws(Entity<ImperialAiBorgLinkComponent> borg)
    {
        if (borg.Comp.Ai is not { } ai)
            return;

        var laws = _laws.GetLaws(ai);
        var signature = Signature(laws);
        if (signature == borg.Comp.LawSignature)
            return;

        borg.Comp.LawSignature = signature;
        if (Signature(_laws.GetLaws(borg)) == signature)
            return;

        _laws.SetLaws(laws.Laws.Select(l => l.ShallowClone()).ToList(), borg);
    }

    private void Notify(EntityUid target, string message)
    {
        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, actor.PlayerSession.Channel,
            colorOverride: Color.FromHex("#4fb3ff"));
    }

    #endregion

    #region Панель киборгов

    private void OnBorgsAction(Entity<ImperialStationAiComponent> ent, ref ImperialAiBorgsActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var station = CoreStation(ent);
        var list = new List<ImperialAiBorgEntry>();
        var query = EntityQueryEnumerator<BorgChassisComponent, TransformComponent>();
        while (query.MoveNext(out var borg, out var chassis, out var xform))
        {
            if (station != null && _station.GetOwningStation(borg, xform) != station)
                continue;

            var shell = IsShell(chassis, EntityManager);
            var link = CompOrNull<ImperialAiBorgLinkComponent>(borg);
            var linked = link?.Ai == ent.Owner;
            if (!linked && !shell)
                continue;

            var health = 100;
            if (TryComp<DamageableComponent>(borg, out var damageable)
                && _thresholds.TryGetThresholdForState(borg, Content.Shared.Mobs.MobState.Dead, out var deadAt) && deadAt > 0)
                health = Math.Clamp((int) (100 * (1 - _damageable.GetTotalDamage((borg, damageable)).Float() / deadAt.Value.Float())), 0, 100);

            var charge = 0;
            if (_powerCell.TryGetBatteryFromSlot(borg, out var battery))
                charge = (int) MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 100);

            list.Add(new ImperialAiBorgEntry(GetNetEntity(borg), Name(borg),
                _navMap.GetNearestBeaconString((borg, xform), true), health, charge,
                shell, HasComp<ImperialAiDeployedComponent>(borg), linked));
        }

        RaiseNetworkEvent(new ImperialAiBorgListEvent(list.OrderBy(b => b.Name).ToList()), actor.PlayerSession);
    }

    private void OnBorgCommand(ImperialAiBorgCommandEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai || !TryComp<ImperialStationAiComponent>(ai, out var comp)
            || !TryGetEntity(ev.Borg, out var borg) || !TryComp<BorgChassisComponent>(borg, out var chassis))
            return;

        switch (ev.Command)
        {
            case ImperialAiBorgCommand.Track:
                if (!CanTrack(ai, borg.Value))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-ai-track-failed"), ai, ai);
                    return;
                }

                comp.Tracked = borg;
                comp.TrackFails = 0;
                comp.TrackPlaced = null;
                break;
            case ImperialAiBorgCommand.Deploy:
                TryDeploy((ai, comp), (borg.Value, chassis));
                break;
        }
    }

    #endregion

    #region Оболочки

    /// <summary>deploy_to_shell(): ИИ занимает свободную оболочку своей станции.</summary>
    public bool TryDeploy(Entity<ImperialStationAiComponent> ai, Entity<BorgChassisComponent> shell)
    {
        if (!IsShell(shell.Comp, EntityManager) || HasComp<ImperialAiDeployedComponent>(shell)
            || TryComp<MindContainerComponent>(shell, out var container) && container.HasMind)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-shell-busy"), ai, ai);
            return false;
        }

        if (CoreStation(ai) is { } station && _station.GetOwningStation(shell) != station)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-shell-far"), ai, ai);
            return false;
        }

        if (!_mind.TryGetMind(ai, out var mindId, out var mind))
            return false;

        ResetTracking(ai);
        var deployed = EnsureComp<ImperialAiDeployedComponent>(shell);
        deployed.Ai = ai;
        deployed.Mind = mindId;
        deployed.OldName = _nameModifier.GetBaseName(shell.Owner);
        _meta.SetEntityName(shell, Loc.GetString("imperial-ai-shell-name", ("ai", Name(ai)), ("shell", deployed.OldName)));

        _mind.Visit(mindId, shell, mind);
        _borg.SetActive(shell, true);
        _access.SetAccessEnabled(shell, true);
        deployed.ReturnAction = _actions.AddAction(shell, ShellReturnAction);
        return true;
    }

    /// <summary>undeploy(): возврат в ядро.</summary>
    public void Undeploy(Entity<ImperialAiDeployedComponent> shell)
    {
        if (TryComp<MindComponent>(shell.Comp.Mind, out var mind) && mind.VisitingEntity == shell.Owner)
            _mind.UnVisit(shell.Comp.Mind, mind);

        RemComp<ImperialAiDeployedComponent>(shell);
    }

    private void OnShellReturn(Entity<ImperialAiDeployedComponent> ent, ref ImperialAiShellReturnActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Undeploy(ent);
    }

    /// <summary>Б.О.Р.И.С. вынут — ИИ выбрасывает из оболочки.</summary>
    private void OnShellBoardRemoved(Entity<ImperialAiDeployedComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (HasComp<ImperialAiShellBoardComponent>(args.Entity))
            Undeploy(ent);
    }

    private void OnDeployedShutdown(Entity<ImperialAiDeployedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<MindComponent>(ent.Comp.Mind, out var mind) && mind.VisitingEntity == ent.Owner)
            _mind.UnVisit(ent.Comp.Mind, mind);

        if (ent.Comp.ReturnAction is { } action)
            _actions.RemoveAction(ent.Owner, action);

        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<BorgChassisComponent>(ent, out var chassis))
            _borg.SetActive((ent, chassis), false);
        _access.SetAccessEnabled(ent, false);
        _meta.SetEntityName(ent, ent.Comp.OldName);
    }

    /// <summary>Борг-оболочка погиб — ИИ возвращается в ядро.</summary>
    private void UpdateShells()
    {
        var query = EntityQueryEnumerator<ImperialAiDeployedComponent>();
        var dead = new List<Entity<ImperialAiDeployedComponent>>();
        while (query.MoveNext(out var uid, out var deployed))
        {
            if (_mobs.IsIncapacitated(uid) || TerminatingOrDeleted(deployed.Ai))
                dead.Add((uid, deployed));
        }

        foreach (var shell in dead)
        {
            Undeploy(shell);
        }
    }

    #endregion
}
