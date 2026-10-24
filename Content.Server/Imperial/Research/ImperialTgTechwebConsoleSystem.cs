using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Imperial.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research;

/// <summary>Очередь изучения сервера techweb TG (queue_nodes).</summary>
[RegisterComponent]
public sealed partial class ImperialTgResearchQueueComponent : Component
{
    [ViewVariables]
    public List<(string Node, string By, EntityUid Console, EntityUid User)> Queue = new();
}

/// <summary>Состояние сервера исследований TG: research_disabled и EMPED.</summary>
[RegisterComponent]
public sealed partial class ImperialTgServerStateComponent : Component
{
    [ViewVariables]
    public bool Disabled;

    [ViewVariables]
    public TimeSpan EmpedUntil;

    /// <summary>deconstructed_items: типы, уже давшие очки в деструктивном анализаторе.</summary>
    [ViewVariables]
    public HashSet<string> DeconstructedItems = new();
}

/// <summary>
/// Консоль и сервер techweb TG: изучение со скидкой discount_experiments, очередь (enqueueNode/dequeueNode),
/// блокировка (toggleLock), объявления announce_channels и пассивный доход 1 очко TG/сек при работающем сервере.
/// </summary>
public sealed class ImperialTgTechwebConsoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly Content.Server.Cargo.Systems.CargoSystem _cargo = default!;
    [Dependency] private readonly Content.Server.Station.Systems.StationSystem _station = default!;

    /// <summary>SSeconomy.techweb_bounty: премия отделу науки за каждый изученный узел.</summary>
    private const int TechwebBounty = 250;

    private static readonly ProtoId<Content.Shared.Radio.RadioChannelPrototype> CommonChannel = "Common";

    /// <summary>EMP выводит сервер из строя на 60 секунд (fix_emp).</summary>
    private static readonly TimeSpan EmpDuration = TimeSpan.FromSeconds(60);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _tgRecipes = null);
        SubscribeLocalEvent<ResearchConsoleComponent, ImperialTgResearchNodeMessage>(OnResearchNode);
        SubscribeLocalEvent<ResearchConsoleComponent, ImperialTgEnqueueNodeMessage>(OnEnqueue);
        SubscribeLocalEvent<ResearchConsoleComponent, ImperialTgDequeueNodeMessage>(OnDequeue);
        SubscribeLocalEvent<ResearchConsoleComponent, ImperialTgToggleLockMessage>(OnToggleLock);

        SubscribeLocalEvent<ResearchServerComponent, EmpPulseEvent>(OnServerEmp);
        SubscribeLocalEvent<ResearchServerComponent, GetVerbsEvent<AlternativeVerb>>(OnServerVerbs);
        SubscribeLocalEvent<ResearchServerComponent, ExaminedEvent>(OnServerExamined);
    }

    private bool Allowed(EntityUid console, EntityUid user)
    {
        if (!TryComp<AccessReaderComponent>(console, out var reader) || _access.IsAllowed(user, console, reader))
            return true;

        _popup.PopupEntity(Loc.GetString("research-console-no-access-popup"), user);
        return false;
    }

    #region Изучение

    private ICollection<string> CompletedExperiments(EntityUid server)
    {
        return TryComp<TgExperimentStateComponent>(server, out var state) ? state.Completed : new HashSet<string>();
    }

    private void OnResearchNode(Entity<ResearchConsoleComponent> ent, ref ImperialTgResearchNodeMessage args)
    {
        if (!_power.IsPowered(ent.Owner) || !Allowed(ent, args.Actor))
            return;

        if (!_proto.TryIndex<TechnologyPrototype>(args.Node, out var tech))
            return;

        if (!TryResearch(ent, tech, args.Actor))
            Say(ent, Loc.GetString("imperial-techweb-say-no-points"));
    }

    /// <summary>
    /// research_node: цена узла уменьшается на скидку выполненных экспериментов, затем узел изучается
    /// и объявляется по радио в его каналы.
    /// </summary>
    private bool TryResearch(EntityUid console, TechnologyPrototype tech, EntityUid user)
    {
        if (!TryComp<ResearchClientComponent>(console, out var client) || client.Server is not { } server ||
            !TryComp<ResearchServerComponent>(server, out var serverComp))
            return false;

        var discount = ImperialTgTechweb.GetDiscount(_proto, tech, CompletedExperiments(server));
        if (serverComp.Points < tech.Cost - discount)
            return false;

        // Ванильная разблокировка списывает полную цену — заранее возвращаем скидку.
        _research.ModifyServerPoints(server, discount, serverComp);
        if (!_research.UnlockTechnology(console, tech, user))
        {
            _research.ModifyServerPoints(server, -discount, serverComp);
            return false;
        }

        // get_that_dosh: премия на счёт отдела науки.
        if (_station.GetOwningStation(console) is { } station && tech.Cost > 0)
            _cargo.UpdateBankAccount(station, TechwebBounty, "Science");

        Say(console, Loc.GetString("imperial-techweb-say-researched", ("node", Loc.GetString(tech.Name))));
        Announce(console, tech);

        if (TryComp<ImperialTgResearchQueueComponent>(server, out var queue))
            queue.Queue.RemoveAll(q => q.Node == tech.ID);

        return true;
    }

    /// <summary>on_station_research: «Исследован технологический узел «X»» в announce_channels; при взломе — в общий канал.</summary>
    private void Announce(EntityUid console, TechnologyPrototype tech)
    {
        if (!_proto.TryIndex<ImperialTgNodePrototype>(tech.ID, out var node) || node.Channels.Count == 0 || tech.Cost == 0)
            return;

        var message = Loc.GetString("imperial-techweb-announce", ("node", Loc.GetString(tech.Name)));
        var channels = _emag.CheckFlag(console, EmagType.Interaction) ? new List<ProtoId<Content.Shared.Radio.RadioChannelPrototype>> { CommonChannel } : node.Channels;
        foreach (var channel in channels)
            _radio.SendRadioMessage(console, message, channel, console);
    }

    private void Say(EntityUid console, string message)
    {
        _chat.TrySendInGameICMessage(console, message, InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }

    #endregion

    #region Очередь и блокировка

    private void OnEnqueue(Entity<ResearchConsoleComponent> ent, ref ImperialTgEnqueueNodeMessage args)
    {
        var actor = args.Actor;
        var node = args.Node;
        if (!Allowed(ent, actor) || !TryComp<ResearchClientComponent>(ent, out var client) || client.Server is not { } server)
            return;

        if (!_proto.HasIndex<TechnologyPrototype>(node))
            return;

        var queue = EnsureComp<ImperialTgResearchQueueComponent>(server);
        if (queue.Queue.Any(q => q.Node == node))
            return;

        queue.Queue.Add((node, Name(actor), ent.Owner, actor));
        Sync();
    }

    private void OnDequeue(Entity<ResearchConsoleComponent> ent, ref ImperialTgDequeueNodeMessage args)
    {
        var actor = args.Actor;
        var node = args.Node;
        if (!TryComp<ResearchClientComponent>(ent, out var client) || client.Server is not { } server ||
            !TryComp<ImperialTgResearchQueueComponent>(server, out var queue))
            return;

        // Убрать из очереди может только тот, кто поставил (enqueued_by_user).
        queue.Queue.RemoveAll(q => q.Node == node && q.User == actor);
        Sync();
    }

    private void OnToggleLock(Entity<ResearchConsoleComponent> ent, ref ImperialTgToggleLockMessage args)
    {
        if (!Allowed(ent, args.Actor))
            return;

        var comp = EnsureComp<ImperialTgTechwebConsoleComponent>(ent);
        comp.Locked = !comp.Locked;
        Dirty(ent, comp);
        if (comp.Locked)
            Say(ent, Loc.GetString("imperial-techweb-say-locked"));
    }

    #endregion

    #region Сервер

    /// <summary>working: питание есть, нет EMP и исследования не отключены.</summary>
    public bool IsWorking(EntityUid server)
    {
        if (!_power.IsPowered(server))
            return false;

        return !TryComp<ImperialTgServerStateComponent>(server, out var state) || !state.Disabled && _timing.CurTime >= state.EmpedUntil;
    }

    private void OnServerEmp(Entity<ResearchServerComponent> ent, ref EmpPulseEvent args)
    {
        EnsureComp<ImperialTgServerStateComponent>(ent).EmpedUntil = _timing.CurTime + EmpDuration;
        args.Affected = true;
    }

    /// <summary>Пульт управления серверами TG (server_control): включить или отключить исследования.</summary>
    private void OnServerVerbs(Entity<ResearchServerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var state = EnsureComp<ImperialTgServerStateComponent>(ent);
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(state.Disabled ? "imperial-techweb-server-enable" : "imperial-techweb-server-disable"),
            Act = () =>
            {
                if (!Allowed(ent, user))
                    return;

                state.Disabled = !state.Disabled;
                _popup.PopupEntity(Loc.GetString(state.Disabled ? "imperial-techweb-server-disabled" : "imperial-techweb-server-enabled"), ent, user);
            },
        });
    }

    private void OnServerExamined(Entity<ResearchServerComponent> ent, ref ExaminedEvent args)
    {
        string status;
        if (TryComp<ImperialTgServerStateComponent>(ent, out var state) && _timing.CurTime < state.EmpedUntil)
            status = Loc.GetString("imperial-techweb-server-status-emp");
        else if (!_power.IsPowered(ent.Owner))
            status = Loc.GetString("imperial-techweb-server-status-offline");
        else if (state?.Disabled == true)
            status = Loc.GetString("imperial-techweb-server-status-disabled");
        else
            status = Loc.GetString("imperial-techweb-server-status-online");

        args.PushMarkup(status);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        // SSresearch.fire: сеть получает доход, пока работает её сервер.
        var servers = EntityQueryEnumerator<ResearchServerComponent>();
        while (servers.MoveNext(out var uid, out var server))
        {
            if (IsWorking(uid))
                _research.ModifyServerPoints(uid, ImperialTgTechweb.ServerIncome, server);
        }

        SyncHostedTechnologies();
        ProcessQueues();
        Sync();
    }

    /// <summary>Рецепты, которые открывают узлы TG (кэш до перезагрузки прототипов).</summary>
    private HashSet<ProtoId<LatheRecipePrototype>>? _tgRecipes;

    private HashSet<ProtoId<LatheRecipePrototype>> TgRecipes()
    {
        if (_tgRecipes != null)
            return _tgRecipes;

        _tgRecipes = new HashSet<ProtoId<LatheRecipePrototype>>();
        foreach (var tech in _proto.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (tech.ID.StartsWith("ImperialTg"))
                _tgRecipes.UnionWith(tech.RecipeUnlocks);
        }

        return _tgRecipes;
    }

    /// <summary>
    /// Ванильные технологии, разложенные по узлам TG, считаются изученными, когда изучен их узел и открыты все их рецепты из дерева TG:
    /// так ярусы ванильных дисциплин и предшественники технологий вне дерева TG (закрытая сборка) работают как в SS14.
    /// Рецепты, которых в дереве TG нет (добавлены закрытой сборкой), открываются вместе с технологией.
    /// </summary>
    private void SyncHostedTechnologies()
    {
        if (!_proto.TryIndex<ImperialTgHostedPrototype>(new Robust.Shared.Prototypes.ProtoId<ImperialTgHostedPrototype>("Default"), out var hosted))
            return;

        var tgRecipes = TgRecipes();
        var query = EntityQueryEnumerator<ResearchServerComponent, TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out _, out var database))
        {
            var unlockedRecipes = database.UnlockedRecipes;
            foreach (var (techId, host) in hosted.Technologies)
            {
                if (!_proto.TryIndex<TechnologyPrototype>(techId, out var tech) || !_proto.TryIndex<TechnologyPrototype>(host, out var hostTech))
                    continue;

                if (_research.IsTechnologyUnlocked(uid, tech, database) || !_research.IsTechnologyUnlocked(uid, hostTech, database))
                    continue;

                if (tech.RecipeUnlocks.All(r => !tgRecipes.Contains(r) || unlockedRecipes.Contains(r)))
                    _research.AddTechnology(uid, tech, database);
            }
        }
    }

    /// <summary>Первый узел очереди изучается, как только хватает очков; бесплатные — сразу.</summary>
    private void ProcessQueues()
    {
        var query = EntityQueryEnumerator<ImperialTgResearchQueueComponent>();
        while (query.MoveNext(out _, out var queue))
        {
            for (var i = 0; i < queue.Queue.Count; i++)
            {
                var (node, _, console, user) = queue.Queue[i];
                if (!_proto.TryIndex<TechnologyPrototype>(node, out var tech) ||
                    !TryComp<TechnologyDatabaseComponent>(console, out var database) ||
                    _research.IsTechnologyUnlocked(console, tech, database))
                {
                    queue.Queue.RemoveAt(i);
                    i--;
                    continue;
                }

                if ((i == 0 || tech.Cost == 0) && _research.IsTechnologyAvailable(database, tech) &&
                    TryResearch(console, tech, Exists(user) ? user : console))
                    i--;
            }
        }
    }

    private void Sync()
    {
        var query = EntityQueryEnumerator<ResearchConsoleComponent, ResearchClientComponent>();
        while (query.MoveNext(out var uid, out _, out var client))
        {
            var comp = EnsureComp<ImperialTgTechwebConsoleComponent>(uid);
            var queue = client.Server is { } server && TryComp<ImperialTgResearchQueueComponent>(server, out var q)
                ? q.Queue
                : new List<(string, string, EntityUid, EntityUid)>();
            var rate = 0;
            if (client.Server is { } srv)
            {
                rate = _research.GetPointsPerSecond(srv);
                if (IsWorking(srv))
                    rate += ImperialTgTechweb.ServerIncome;
            }
            var sec = !_emag.CheckFlag(uid, EmagType.Interaction);

            var nodes = queue.Select(e => e.Item1).ToList();
            var by = queue.Select(e => e.Item2).ToList();
            if (comp.PointsPerSecond == rate && comp.SecProtocols == sec && comp.Queue.SequenceEqual(nodes) && comp.QueueBy.SequenceEqual(by))
                continue;

            comp.PointsPerSecond = rate;
            comp.SecProtocols = sec;
            comp.Queue = nodes;
            comp.QueueBy = by;
            Dirty(uid, comp);
        }
    }
}
