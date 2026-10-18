using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Research;
using Content.Shared.Imperial.Research.Experiments;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Research;

/// <summary>
/// Окно консоли исследований — перенос tgui Techweb (tgui/packages/tgui/interfaces/Techweb):
/// шапка (протоколы, очки, очередь, блокировка), вкладки «Изучено/Доступно/Будущее» с поиском,
/// карточки узлов с прогрессом, дизайнами и экспериментами, экран подробностей «Требуется/Открывает».
/// </summary>
public sealed class ImperialTgResearchConsoleMenu : FancyWindow
{
    private const string TechPrefix = "ImperialTg";
    private const string GatePrefix = "ImperialTgUnlock";
    private const string ExperimentGatePrefix = "ImperialTgUnlockExp";
    private const string DynamicPackPrefix = "ImperialTgDynamic";

    // Цвета стандартной темы tgui.
    private static readonly Color Primary = Color.FromHex("#4972a1");
    private static readonly Color Good = Color.FromHex("#5baa27");
    private static readonly Color Average = Color.FromHex("#f29a29");
    private static readonly Color Bad = Color.FromHex("#db2828");
    private static readonly Color LabelColor = Color.FromHex("#7e90a7");
    private static readonly Color SectionBg = Color.FromHex("#131313");
    private static readonly Color ExperimentTitle = Color.FromHex("#40628a");

    private enum Tab
    {
        Researched,
        Available,
        Future,
    }

    private readonly IEntityManager _entity;
    private readonly IPrototypeManager _proto;
    private readonly IPlayerManager _player;
    private readonly Content.Client.Research.ResearchSystem _researchSystem;
    private HashSet<string> _unlocked = new();

    private readonly BoxContainer _root = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, Margin = new Thickness(6) };
    private readonly LineEdit _searchEdit;

    private EntityUid _owner;
    private int _points;
    private Tab _tab = Tab.Available;
    private string _search = string.Empty;
    private string? _detailNode;
    private bool _detailUnlocksTab;
    private readonly HashSet<string> _openCollapsibles = new();
    private string _signature = string.Empty;
    private ScrollContainer? _scroll;
    private string _scrollKey = string.Empty;
    private Vector2? _pendingScroll;
    private int _pendingFrames;

    /// <summary>Обновления элементов при изменении очков; true — нужна полная перестройка (сменилась кнопка узла).</summary>
    private readonly List<Func<bool>> _pointUpdaters = new();
    private float _pollTimer;

    public event Action<string>? OnResearch;
    public event Action<string>? OnEnqueue;
    public event Action<string>? OnDequeue;
    public event Action? OnToggleLock;
    public event Action? OnServerButton;

    public ImperialTgResearchConsoleMenu()
    {
        _entity = IoCManager.Resolve<IEntityManager>();
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _researchSystem = _entity.System<Content.Client.Research.ResearchSystem>();

        Title = Loc.GetString("imperial-techweb-title");
        MinSize = new Vector2(640, 560);
        SetSize = new Vector2(640, 735);
        ContentsContainer.AddChild(_root);

        _searchEdit = new LineEdit { PlaceHolder = Loc.GetString("imperial-techweb-search"), MinWidth = 150 };
        _searchEdit.OnTextChanged += args =>
        {
            _search = args.Text.ToLowerInvariant();
            Rebuild();
        };
    }

    public void SetEntity(EntityUid owner)
    {
        _owner = owner;
    }

    public void UpdateState(int points)
    {
        // Очки растут каждую секунду: обновляем только зависящие от них элементы, без перестройки окна.
        if (_pointUpdaters.Count > 0 && Signature() == _signature)
        {
            if (points == _points)
                return;

            _points = points;
            var needRebuild = false;
            foreach (var updater in _pointUpdaters)
                needRebuild |= updater();

            if (!needRebuild)
                return;
        }

        _points = points;
        Rebuild();
    }

    /// <summary>Очередь, блокировка, прирост и эксперименты приходят компонентами — сверяем их дважды в секунду.</summary>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Восстанавливаем прокрутку после перестройки, когда список уже разложен.
        if (_pendingScroll is { } pending && _scroll != null)
        {
            _scroll.SetScrollValue(pending);
            if (--_pendingFrames <= 0)
                _pendingScroll = null;
        }

        _pollTimer += args.DeltaSeconds;
        if (_pollTimer < 0.5f)
            return;
        _pollTimer = 0;

        if (Signature() != _signature)
            Rebuild();
    }

    private string Signature()
    {
        var parts = new List<string>();
        if (_entity.TryGetComponent<ImperialTgTechwebConsoleComponent>(_owner, out var console))
        {
            parts.Add(console.Locked.ToString());
            parts.Add(console.SecProtocols.ToString());
            parts.Add(console.PointsPerSecond.ToString());
            parts.Add(string.Join(",", console.Queue));
        }

        if (_entity.TryGetComponent<TgExperimentViewComponent>(_owner, out var view))
        {
            parts.Add(string.Join(",", view.Completed));
            parts.Add(string.Join(",", view.Progress.Select(p => string.Format("{0}={1}", p.Key, p.Value))));
        }

        if (_entity.TryGetComponent<TechnologyDatabaseComponent>(_owner, out var database))
        {
            var unlockedList = database.UnlockedTechnologies;
            parts.Add(unlockedList.Count.ToString());
        }

        return string.Join("|", parts);
    }

    #region Данные узлов

    /// <summary>Узел techweb в терминах tgui: tier 0 — изучен, 1 — доступен, 2 — будущий.</summary>
    private sealed record NodeInfo(
        TechnologyPrototype Tech,
        int Tier,
        bool ExperimentsDone,
        List<TechnologyPrototype> Prereqs,
        List<TgExperimentPrototype> Required,
        List<TgExperimentPrototype> Discount);

    private List<NodeInfo> GatherNodes(TechnologyDatabaseComponent database)
    {
        var unlocked = database.UnlockedTechnologies.Select(t => t.Id).ToHashSet();
        _unlocked = unlocked;
        var experiments = _proto.EnumeratePrototypes<TgExperimentPrototype>().ToList();
        var nodes = new List<NodeInfo>();

        foreach (var tech in _proto.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (!tech.ID.StartsWith(TechPrefix) || tech.ID.StartsWith(GatePrefix))
                continue;

            // Скрытые узлы (BEPIS, инопланетные, Синдикат) видны, только когда раскрыты.
            if (!unlocked.Contains(tech.ID) &&
                tech.TechnologyPrerequisites.Any(p => p.Id.StartsWith(GatePrefix) && !p.Id.StartsWith(ExperimentGatePrefix) && !unlocked.Contains(p.Id)))
                continue;

            var prereqs = new List<TechnologyPrototype>();
            foreach (var p in tech.TechnologyPrerequisites)
            {
                if (!p.Id.StartsWith(GatePrefix) && _proto.TryIndex(p, out var pre))
                    prereqs.Add(pre);
            }

            var experimentsDone = tech.TechnologyPrerequisites
                .Where(p => p.Id.StartsWith(ExperimentGatePrefix))
                .All(p => unlocked.Contains(p.Id));

            var nodeExps = experiments.Where(e => TechPrefix + ToCamel(e.Node) == tech.ID).ToList();

            var tier = unlocked.Contains(tech.ID) ? 0 : prereqs.All(p => unlocked.Contains(p.ID)) ? 1 : 2;

            nodes.Add(new NodeInfo(tech, tier, experimentsDone, prereqs,
                nodeExps.Where(e => e.Required).ToList(),
                nodeExps.Where(e => !e.Required).ToList()));
        }

        // Технологии вне дерева TG (закрытая сборка): свои цена, ярусы и предшественники по правилам SS14.
        foreach (var tech in _proto.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (!ImperialTgForeignTech.IsForeign(_proto, tech))
                continue;

            var prereqs = new List<TechnologyPrototype>();
            foreach (var p in tech.TechnologyPrerequisites)
            {
                if (_proto.TryIndex(p, out var pre))
                    prereqs.Add(pre);
            }

            var tier = unlocked.Contains(tech.ID) ? 0 : _researchSystem.IsTechnologyAvailable(database, tech) ? 1 : 2;
            nodes.Add(new NodeInfo(tech, tier, true, prereqs, new List<TgExperimentPrototype>(), new List<TgExperimentPrototype>()));
        }

        return nodes;
    }

    private static string ToCamel(string node)
    {
        return string.Concat(node.Split('_').Select(p => p.Length == 0 ? p : p.Substring(0, 1).ToUpperInvariant() + p.Substring(1)));
    }

    #endregion

    private void Rebuild()
    {
        _signature = Signature();
        _pointUpdaters.Clear();

        // Перестройка не должна сбрасывать прокрутку того же экрана.
        var key = CurrentScrollKey();
        if (_scroll != null && key == _scrollKey)
        {
            _pendingScroll = _scroll.GetScrollValue();
            _pendingFrames = 3;
        }
        else
            _pendingScroll = null;
        _scrollKey = key;
        _scroll = null;
        _searchEdit.Orphan();
        _root.RemoveAllChildren();

        _entity.TryGetComponent<ImperialTgTechwebConsoleComponent>(_owner, out var console);

        // TechwebStart: заблокированная консоль.
        if (console?.Locked == true)
        {
            _root.AddChild(Modal(Loc.GetString("imperial-techweb-locked"), Loc.GetString("imperial-techweb-unlock"), () => OnToggleLock?.Invoke()));
            return;
        }

        if (!_entity.TryGetComponent<TechnologyDatabaseComponent>(_owner, out var database) || database.SupportedDisciplines.Count == 0)
        {
            _root.AddChild(Modal(Loc.GetString("imperial-techweb-no-web"), Loc.GetString("research-console-menu-server-selection-button"), () => OnServerButton?.Invoke()));
            return;
        }

        var nodes = GatherNodes(database);
        _root.AddChild(BuildHeader(console));

        if (_detailNode != null && nodes.FirstOrDefault(n => n.Tech.ID == _detailNode) is { } detail)
            BuildDetail(detail, nodes, console);
        else
        {
            _detailNode = null;
            BuildOverview(nodes, console);
        }
    }

    private string CurrentScrollKey()
    {
        return _detailNode != null
            ? string.Format("detail:{0}:{1}", _detailNode, _detailUnlocksTab)
            : string.Format("overview:{0}:{1}", _tab, _search);
    }

    #region Шапка

    private Control BuildHeader(ImperialTgTechwebConsoleComponent? console)
    {
        var list = new GridContainer { Columns = 2, HorizontalExpand = true };

        // Протоколы безопасности.
        var secOn = console?.SecProtocols ?? true;
        list.AddChild(FieldLabel(Loc.GetString("imperial-techweb-security")));
        list.AddChild(new Label
        {
            Text = Loc.GetString(secOn ? "imperial-techweb-engaged" : "imperial-techweb-disengaged"),
            FontColorOverride = secOn ? Good : Bad,
            Margin = new Thickness(4, 0, 0, 2),
        });

        // Очки.
        var rate = console?.PointsPerSecond ?? 0;
        list.AddChild(FieldLabel(Loc.GetString("imperial-techweb-points-abbr")));
        var points = new RichTextLabel { Margin = new Thickness(4, 0, 0, 2) };
        points.SetMessage(Robust.Shared.Utility.FormattedMessage.FromMarkupOrThrow(rate > 0
            ? Loc.GetString("imperial-techweb-points-rate", ("points", _points), ("rate", rate))
            : Loc.GetString("imperial-techweb-points", ("points", _points))));
        list.AddChild(points);
        _pointUpdaters.Add(() =>
        {
            points.SetMessage(Robust.Shared.Utility.FormattedMessage.FromMarkupOrThrow(rate > 0
                ? Loc.GetString("imperial-techweb-points-rate", ("points", _points), ("rate", rate))
                : Loc.GetString("imperial-techweb-points", ("points", _points))));
            return false;
        });

        // Очередь.
        list.AddChild(FieldLabel(Loc.GetString("imperial-techweb-queue")));
        var queue = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(4, 0, 0, 2) };
        if (console == null || console.Queue.Count == 0)
            queue.AddChild(new Label { Text = Loc.GetString("imperial-techweb-queue-empty") });
        else
        {
            for (var i = 0; i < console.Queue.Count; i++)
            {
                var name = _proto.TryIndex<TechnologyPrototype>(console.Queue[i], out var tech) ? Loc.GetString(tech.Name) : console.Queue[i];
                queue.AddChild(new Button
                {
                    Text = name,
                    ToolTip = Loc.GetString("imperial-techweb-added-by", ("user", i < console.QueueBy.Count ? console.QueueBy[i] : "?")),
                    Margin = new Thickness(0, 0, 2, 0),
                });
            }
        }
        list.AddChild(queue);

        var lockButton = new Button { Text = Loc.GetString("imperial-techweb-lock"), HorizontalExpand = true };
        lockButton.OnPressed += _ => OnToggleLock?.Invoke();
        var serverButton = new Button { Text = Loc.GetString("research-console-menu-server-selection-button"), HorizontalExpand = true, Margin = new Thickness(0, 4, 0, 0) };
        serverButton.OnPressed += _ => OnServerButton?.Invoke();

        return Framed(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(6),
            Children =
            {
                list,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    MinWidth = 170,
                    Children = { lockButton, serverButton },
                },
            },
        });
    }

    #endregion

    #region Обзор (Overview)

    private void BuildOverview(List<NodeInfo> nodes, ImperialTgTechwebConsoleComponent? console)
    {
        var searching = _search.Trim().Length > 1;

        var tabs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        tabs.AddChild(TabButton(Loc.GetString("imperial-techweb-tab-researched"), !searching && _tab == Tab.Researched, () => SwitchTab(Tab.Researched)));
        tabs.AddChild(TabButton(Loc.GetString("imperial-techweb-tab-available"), !searching && _tab == Tab.Available, () => SwitchTab(Tab.Available)));
        tabs.AddChild(TabButton(Loc.GetString("imperial-techweb-tab-future"), !searching && _tab == Tab.Future, () => SwitchTab(Tab.Future)));
        if (searching)
            tabs.AddChild(TabButton(Loc.GetString("imperial-techweb-tab-search"), true, () => { }));

        _root.AddChild(TabsHeader(Loc.GetString("imperial-techweb-web-view"), tabs, _searchEdit));

        IEnumerable<NodeInfo> shown;
        if (searching)
        {
            shown = nodes.Where(n =>
                Loc.GetString(n.Tech.Name).ToLowerInvariant().Contains(_search) ||
                Description(n.Tech).ToLowerInvariant().Contains(_search) ||
                n.Tech.RecipeUnlocks.Any(r => _proto.TryIndex(r, out var recipe) && RecipeName(recipe).ToLowerInvariant().Contains(_search)));
        }
        else
        {
            shown = _tab switch
            {
                Tab.Researched => nodes.Where(n => n.Tier == 0),
                Tab.Available => nodes.Where(n => n.Tier == 1),
                _ => nodes.Where(n => n.Tier >= 2),
            };
            shown = shown.OrderBy(n => Loc.GetString(n.Tech.Name));
        }

        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        foreach (var node in shown)
            list.AddChild(BuildNode(node, nodes, console, nodetails: false));

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Margin = new Thickness(0, 6, 0, 0) };
        _scroll = scroll;
        scroll.AddChild(list);
        _root.AddChild(scroll);

        if (searching)
            _searchEdit.GrabKeyboardFocus();
    }

    private void SwitchTab(Tab tab)
    {
        _tab = tab;
        _search = string.Empty;
        _searchEdit.Text = string.Empty;
        Rebuild();
    }

    #endregion

    #region Подробности (detail)

    private void BuildDetail(NodeInfo node, List<NodeInfo> nodes, ImperialTgTechwebConsoleComponent? console)
    {
        var prereqNodes = nodes.Where(n => node.Prereqs.Any(p => p.ID == n.Tech.ID)).ToList();
        var complPrereq = prereqNodes.Count(n => n.Tier == 0);
        var unlockNodes = nodes.Where(n => n.Prereqs.Any(p => p.ID == node.Tech.ID)).ToList();

        var tabs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        tabs.AddChild(TabButton(Loc.GetString("imperial-techweb-tab-required", ("done", complPrereq), ("count", prereqNodes.Count)), !_detailUnlocksTab, () =>
        {
            _detailUnlocksTab = false;
            Rebuild();
        }));
        tabs.AddChild(TabButton(Loc.GetString("imperial-techweb-tab-unlocks", ("count", unlockNodes.Count)), _detailUnlocksTab, () =>
        {
            _detailUnlocksTab = true;
            Rebuild();
        }));

        var home = new Button { Text = Loc.GetString("imperial-techweb-home") };
        home.OnPressed += _ =>
        {
            _detailNode = null;
            Rebuild();
        };

        _root.AddChild(TabsHeader(Loc.GetString("imperial-techweb-node"), tabs, home));

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(0, 6, 0, 0) };
        top.AddChild(BuildNode(node, nodes, console, nodetails: true));
        top.AddChild(new PanelContainer { MinHeight = 2, Margin = new Thickness(0, 2, 0, 6), PanelOverride = new StyleBoxFlat { BackgroundColor = Primary.WithAlpha(0.5f) } });
        _root.AddChild(top);

        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        foreach (var n in _detailUnlocksTab ? unlockNodes : prereqNodes)
            list.AddChild(BuildNode(n, nodes, console, nodetails: false));

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _scroll = scroll;
        scroll.AddChild(list);
        _root.AddChild(scroll);
    }

    #endregion

    #region Карточка узла (TechNode)

    private Control BuildNode(NodeInfo node, List<NodeInfo> all, ImperialTgTechwebConsoleComponent? console, bool nodetails)
    {
        var tech = node.Tech;
        var queue = console?.Queue ?? new List<string>();
        var queueBy = console?.QueueBy ?? new List<string>();
        var localName = _player.LocalEntity is { } local ? _entity.GetComponent<MetaDataComponent>(local).EntityName : string.Empty;
        var queueIndex = queue.IndexOf(tech.ID);
        var enqueuedByUser = queueIndex >= 0 && queueIndex < queueBy.Count && queueBy[queueIndex] == localName;
        var completed = _entity.TryGetComponent<TgExperimentViewComponent>(_owner, out var expView) ? expView.Completed : new List<string>();
        var reqPts = tech.Cost - ImperialTgTechweb.GetDiscount(_proto, tech, completed);
        var isFree = reqPts == 0;
        var canUnlock = node.Tier == 1 && node.ExperimentsDone && _points >= reqPts;
        var techcompl = node.Prereqs.Count(p => _unlocked.Contains(p.ID));

        // Кнопки заголовка — та же логика, что у TechNode.tsx.
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        if (node.Tier > 0)
        {
            if (canUnlock && (isFree || queue.Count == 0))
            {
                var research = new Button { Text = Loc.GetString("imperial-techweb-research") };
                research.OnPressed += _ => OnResearch?.Invoke(tech.ID);
                buttons.AddChild(research);
            }
            else if (enqueuedByUser)
            {
                var dequeue = new Button { Text = Loc.GetString("imperial-techweb-dequeue"), ModulateSelfOverride = Bad };
                dequeue.OnPressed += _ => OnDequeue?.Invoke(tech.ID);
                buttons.AddChild(dequeue);
            }
            else if (queueIndex >= 0)
            {
                buttons.AddChild(new Button { Text = Loc.GetString("imperial-techweb-queued"), ModulateSelfOverride = Good });
            }
            else
            {
                var enqueue = new Button
                {
                    Text = Loc.GetString("imperial-techweb-enqueue"),
                    Disabled = !node.ExperimentsDone || techcompl < node.Prereqs.Count,
                };
                enqueue.OnPressed += _ => OnEnqueue?.Invoke(tech.ID);
                buttons.AddChild(enqueue);
            }
        }

        if (!nodetails)
        {
            var details = new Button { Text = Loc.GetString("imperial-techweb-details"), Margin = new Thickness(2, 0, 0, 0) };
            details.OnPressed += _ =>
            {
                _detailNode = tech.ID;
                _detailUnlocksTab = false;
                Rebuild();
            };
            buttons.AddChild(details);
        }

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(6, 6, 6, 6) };

        // Techweb__NodeProgress: очки, технологии, эксперименты.
        if (node.Tier != 0)
        {
            var progress = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var req = reqPts;
            var (pointsBar, setPoints) = BarWithSetter(req == 0 ? 1f : Math.Min(1f, (float) _points / req),
                Loc.GetString("imperial-techweb-progress-points", ("have", Math.Min(req, _points)), ("need", req)));
            progress.AddChild(pointsBar);
            var builtCanUnlock = canUnlock;
            _pointUpdaters.Add(() =>
            {
                if ((node.Tier == 1 && node.ExperimentsDone && _points >= req) != builtCanUnlock)
                    return true;

                setPoints(req == 0 ? 1f : Math.Min(1f, (float) _points / req),
                    Loc.GetString("imperial-techweb-progress-points", ("have", Math.Min(req, _points)), ("need", req)));
                return false;
            });
            if (node.Prereqs.Count > 0)
                progress.AddChild(Bar((float) techcompl / node.Prereqs.Count,
                    Loc.GetString("imperial-techweb-progress-tech", ("done", techcompl), ("count", node.Prereqs.Count))));
            if (node.Required.Count > 0)
            {
                var done = node.Required.Count(IsExperimentDone);
                progress.AddChild(Bar((float) done / node.Required.Count,
                    Loc.GetString("imperial-techweb-progress-exp", ("done", done), ("count", node.Required.Count))));
            }
            body.AddChild(progress);
        }

        var description = new RichTextLabel { Margin = new Thickness(0, 0, 0, 8) };
        description.SetMessage(Description(tech));
        body.AddChild(description);

        // Techweb__NodeUnlockedDesigns: иконки дизайнов с подсказкой.
        var designs = new GridContainer { Columns = 14, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var recipeId in tech.RecipeUnlocks)
        {
            if (!_proto.TryIndex(recipeId, out var recipe) || recipe.Result is not { } result)
                continue;

            var icon = new EntityPrototypeView { SetSize = new Vector2(32, 32) };
            icon.SetPrototype(result);
            var cell = new Button
            {
                MinSize = new Vector2(38, 38),
                Margin = new Thickness(0, 0, 2, 2),
                ToolTip = DesignTooltip(recipe),
            };
            cell.AddChild(icon);
            designs.AddChild(cell);
        }
        if (designs.ChildCount > 0)
            body.AddChild(designs);

        // Эксперименты: узнаваемы, когда узел доступен (иначе — LockedExperiment).
        var discovered = node.Tier <= 1;
        if (node.Required.Count > 0)
            body.AddChild(ExperimentsCollapsible(tech.ID + ":req", Loc.GetString("imperial-techweb-required-exp"), node.Required, discovered, discount: false));
        if (node.Discount.Count > 0)
            body.AddChild(ExperimentsCollapsible(tech.ID + ":disc", Loc.GetString("imperial-techweb-discount-exp"), node.Discount, discovered, discount: true));

        return SectionPanel(Loc.GetString(tech.Name), buttons, body);
    }

    private string Description(TechnologyPrototype tech)
    {
        if (ImperialTgForeignTech.IsForeign(_proto, tech) && _proto.TryIndex(tech.Discipline, out var discipline))
            return Loc.GetString("imperial-techweb-foreign-desc", ("discipline", Loc.GetString(discipline.Name)));

        var key = string.Format("{0}-desc", tech.Name);
        return Loc.TryGetString(key, out var desc) ? desc : string.Empty;
    }

    private string RecipeName(LatheRecipePrototype recipe)
    {
        if (recipe.Result is { } result && _proto.TryIndex<EntityPrototype>(result, out var proto))
            return proto.Name;
        return recipe.ID;
    }

    /// <summary>Подсказка дизайна: название, станки, материалы (MaterialCostSequence).</summary>
    private string DesignTooltip(LatheRecipePrototype recipe)
    {
        var lines = new List<string> { RecipeName(recipe) };

        var lathes = _proto.EnumeratePrototypes<LatheRecipePackPrototype>()
            .Where(p => p.ID.StartsWith(DynamicPackPrefix) && p.Recipes.Contains(recipe.ID))
            .Select(p => p.ID.Substring(DynamicPackPrefix.Length))
            .Select(id => _proto.TryIndex<EntityPrototype>(id, out var lathe) ? lathe.Name : id)
            .Distinct()
            .ToList();
        if (lathes.Count > 0)
            lines.Add(string.Format("• {0}", string.Join(", ", lathes)));

        var mats = new List<string>();
        foreach (var (material, amount) in recipe.Materials)
        {
            var name = _proto.TryIndex<MaterialPrototype>(material, out var mat) ? Loc.GetString(mat.Name) : material.Id;
            mats.Add(string.Format("{0}: {1:0.##}", name, amount / 100f));
        }
        if (mats.Count > 0)
        {
            lines.Add("────────");
            lines.AddRange(mats);
        }

        return string.Join("\n", lines);
    }

    #endregion

    #region Эксперименты

    private bool IsExperimentDone(TgExperimentPrototype exp)
    {
        return _entity.TryGetComponent<TgExperimentViewComponent>(_owner, out var view) && view.Completed.Contains(exp.ID);
    }

    private Control ExperimentsCollapsible(string key, string title, List<TgExperimentPrototype> experiments, bool discovered, bool discount)
    {
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        foreach (var exp in experiments)
            content.AddChild(discovered ? ExperimentPanel(exp, discount) : LockedExperiment());

        var body = new CollapsibleBody();
        body.AddChild(content);

        var heading = new CollapsibleHeading(title);
        var collapsible = new Collapsible(heading, body) { Margin = new Thickness(0, 2) };
        collapsible.BodyVisible = _openCollapsibles.Contains(key);
        heading.OnToggled += args =>
        {
            if (args.Pressed)
                _openCollapsibles.Add(key);
            else
                _openCollapsibles.Remove(key);
        };
        return collapsible;
    }

    /// <summary>ExperimentConfigure.Experiment: синяя полоса с названием и тегом, описание, стадия.</summary>
    private Control ExperimentPanel(TgExperimentPrototype exp, bool discount)
    {
        _entity.TryGetComponent<TgExperimentViewComponent>(_owner, out var view);
        var done = view?.Completed.Contains(exp.ID) == true;
        var count = exp.Requirement.Count;
        var progress = done ? count : Math.Min(view?.Progress.GetValueOrDefault(exp.ID) ?? 0, count);

        var title = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = done ? Good.WithAlpha(0.7f) : ExperimentTitle },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(6, 3),
                    Children =
                    {
                        new Label { Text = Loc.GetString(exp.Name), HorizontalExpand = true },
                        new Label { Text = ExperimentTag(exp), FontColorOverride = Color.White.WithAlpha(0.5f) },
                    },
                },
            },
        };

        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(6, 4) };
        var desc = new RichTextLabel { Margin = new Thickness(0, 0, 0, 4) };
        desc.SetMessage(Loc.GetString(exp.Description));
        content.AddChild(desc);

        if (discount)
        {
            content.AddChild(new Label
            {
                Text = Loc.GetString("imperial-techweb-discount-text", ("points", exp.Discount * 75)),
                FontColorOverride = LabelColor,
                Margin = new Thickness(0, 4),
            });
        }

        // ExperimentStageRow: флажок, стадия, прогресс.
        content.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(12, 0, 0, 0),
            Children =
            {
                new Label { Text = done ? "☑" : "☐", Margin = new Thickness(0, 0, 6, 0), FontColorOverride = done ? Good : Color.White },
                new Label { Text = Loc.GetString("imperial-techweb-exp-stage"), HorizontalExpand = true },
                Bar(count == 0 ? 1f : (float) progress / count, string.Format("{0}/{1}", progress, count), 120),
            },
        });

        return ExperimentFrame(title, content);
    }

    private string ExperimentTag(TgExperimentPrototype exp)
    {
        var type = exp.Requirement.GetType().Name.Replace("Tg", string.Empty).Replace("Requirement", string.Empty).ToLowerInvariant();
        return Loc.GetString(string.Format("imperial-techweb-exp-tag-{0}", type));
    }

    private Control LockedExperiment()
    {
        var title = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = ExperimentTitle.WithAlpha(0.5f) },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(6, 3),
                    Children =
                    {
                        new Label { Text = Loc.GetString("imperial-techweb-exp-locked"), HorizontalExpand = true, FontColorOverride = Color.Black.WithAlpha(0.6f) },
                        new Label { Text = "???", FontColorOverride = Color.Black.WithAlpha(0.5f) },
                    },
                },
            },
        };

        var desc = new RichTextLabel { Margin = new Thickness(6, 4) };
        desc.SetMessage(Loc.GetString("imperial-techweb-exp-locked-desc"));
        return ExperimentFrame(title, desc);
    }

    private static Control ExperimentFrame(Control title, Control content)
    {
        return new PanelContainer
        {
            Margin = new Thickness(4),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.33f), BorderColor = ExperimentTitle, BorderThickness = new Thickness(1) },
            Children = { new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Children = { title, content } } },
        };
    }

    #endregion

    #region Элементы оформления tgui

    private static Label FieldLabel(string text)
    {
        return new Label { Text = text, FontColorOverride = LabelColor, Margin = new Thickness(0, 0, 8, 2) };
    }

    /// <summary>Techweb__HeaderContent: чёрная панель с рамкой основного цвета.</summary>
    private static Control Framed(Control content)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black, BorderColor = Primary, BorderThickness = new Thickness(1) },
            Children = { content },
        };
    }

    /// <summary>Techweb__HeaderSectionTabs: заголовок с чертой справа, вкладки и элемент справа.</summary>
    private static Control TabsHeader(string title, Control tabs, Control right)
    {
        return new PanelContainer
        {
            Margin = new Thickness(0, 8, 0, 0),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black, BorderColor = Primary, BorderThickness = new Thickness(1) },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(6, 3),
                    Children =
                    {
                        new Label { Text = title, StyleClasses = { "LabelHeading" }, VerticalAlignment = VAlignment.Center },
                        new PanelContainer { MinWidth = 1, Margin = new Thickness(6, 0), PanelOverride = new StyleBoxFlat { BackgroundColor = Primary } },
                        tabs,
                        right,
                    },
                },
            },
        };
    }

    private static Button TabButton(string text, bool selected, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            Pressed = selected,
            Margin = new Thickness(0, 0, 2, 0),
        };
        button.OnPressed += _ => onPressed();
        return button;
    }

    /// <summary>Section tgui: заголовок с кнопками справа и чертой снизу, затем содержимое.</summary>
    private static Control SectionPanel(string title, Control buttons, Control body)
    {
        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(6, 4),
            Children =
            {
                new Label { Text = title, StyleClasses = { "LabelHeading" }, HorizontalExpand = true, VerticalAlignment = VAlignment.Center },
                buttons,
            },
        };

        return new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 6),
            PanelOverride = new StyleBoxFlat { BackgroundColor = SectionBg },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Children =
                    {
                        header,
                        new PanelContainer { MinHeight = 2, PanelOverride = new StyleBoxFlat { BackgroundColor = Primary } },
                        body,
                    },
                },
            },
        };
    }

    /// <summary>ProgressBar tgui с диапазонами: good ≥ 0.5, average ≥ 0.25, иначе bad.</summary>
    /// <summary>Полоса прогресса с функцией обновления значения без перестройки.</summary>
    private static (Control, Action<float, string>) BarWithSetter(float value, string text)
    {
        var control = Bar(value, text);
        var bar = control.Children.OfType<ProgressBar>().First();
        var label = control.Children.OfType<Label>().First();
        return (control, (v, t) =>
        {
            var color = v >= 0.5f ? Good : v >= 0.25f ? Average : Bad;
            bar.Value = Math.Clamp(v, 0, 1);
            bar.BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color.WithAlpha(0.2f), BorderColor = color, BorderThickness = new Thickness(1) };
            bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color.WithAlpha(0.7f) };
            label.Text = t;
        });
    }

    private static Control Bar(float value, string text, float minWidth = 0)
    {
        var color = value >= 0.5f ? Good : value >= 0.25f ? Average : Bad;
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = Math.Clamp(value, 0, 1),
            MinHeight = 20,
            HorizontalExpand = true,
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color.WithAlpha(0.2f), BorderColor = color, BorderThickness = new Thickness(1) },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color.WithAlpha(0.7f) },
        };

        return new Control
        {
            HorizontalExpand = true,
            MinWidth = minWidth,
            Margin = new Thickness(0, 0, 4, 0),
            Children =
            {
                bar,
                new Label { Text = text, HorizontalAlignment = HAlignment.Center, VerticalAlignment = VAlignment.Center },
            },
        };
    }

    /// <summary>Modal tgui: заблокированная консоль или нет сети.</summary>
    private static Control Modal(string text, string buttonText, Action onPressed)
    {
        var button = new Button { Text = buttonText, HorizontalAlignment = HAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        button.OnPressed += _ => onPressed();

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            Children =
            {
                new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black, BorderColor = Primary, BorderThickness = new Thickness(1) },
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Vertical,
                            Margin = new Thickness(16),
                            Children =
                            {
                                new Label { Text = text, StyleClasses = { "LabelHeading" }, HorizontalAlignment = HAlignment.Center },
                                button,
                            },
                        },
                    },
                },
            },
        };
    }

    #endregion
}
