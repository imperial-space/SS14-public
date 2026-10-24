using Content.Server.Research.Systems;
using Content.Server.Research.Components;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using System.Linq;
using Content.Shared.Imperial.Research;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Imperial.Research;

/// <summary>
/// Дерево исследований TG/BandaStation (techweb) вместо ванильного дерева SS14.
/// Серверы исследований поддерживают только дисциплины TG, стартовые узлы открыты сразу,
/// консоль исследований показывает всё дерево и позволяет купить любой доступный узел.
/// Скрытые узлы и узлы с обязательными экспериментами требуют маркер ImperialTgUnlock*,
/// который открывают эксперименты, BEPIS и анализ находок (<see cref="RevealNode"/>).
/// </summary>
public sealed class ImperialTgResearchSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public const string TechPrefix = "ImperialTg";
    public const string GatePrefix = "ImperialTgUnlock";
    private static readonly EntProtoId Template = "ImperialTgResearchTemplate";

    private readonly List<TechnologyPrototype> _startingTech = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResearchServerComponent, MapInitEvent>(OnServerMapInit, after: [typeof(ResearchSystem)]);
        SubscribeLocalEvent<ResearchConsoleComponent, MapInitEvent>(OnConsoleMapInit, after: [typeof(ResearchSystem)]);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        CacheStartingTech();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<TechnologyPrototype>())
            CacheStartingTech();
    }

    private void CacheStartingTech()
    {
        _startingTech.Clear();
        foreach (var tech in _proto.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (tech.ID.StartsWith(TechPrefix) && !tech.ID.StartsWith(GatePrefix) && !tech.Hidden && tech.Cost == 0)
                _startingTech.Add(tech);
        }
    }

    /// <summary>starting_node: дисциплины TG (из шаблона) и стартовые узлы на новом сервере исследований.</summary>
    private void OnServerMapInit(Entity<ResearchServerComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<TechnologyDatabaseComponent>(ent))
            return;

        var template = Spawn(Template, MapCoordinates.Nullspace);
        AddForeignDisciplines(template);
        _research.Sync(ent, template);
        Del(template);

        foreach (var tech in _startingTech)
        {
            if (!_research.IsTechnologyUnlocked(ent, tech))
                _research.AddTechnology(ent, tech);
        }

        Log.Info($"Дерево TG на {ToPrettyString(ent)}: стартовых узлов {_startingTech.Count}");
    }

    /// <summary>
    /// Дисциплины технологий не из дерева TG (закрытая сборка, новые ванильные), чтобы их можно было изучать.
    /// Список дисциплин закрыт Access — компонент собирается через сериализацию, как из прототипа.
    /// </summary>
    private void AddForeignDisciplines(EntityUid template)
    {
        var disciplines = _proto.EnumeratePrototypes<TechDisciplinePrototype>()
            .Where(d => d.ID.StartsWith(TechPrefix))
            .Select(d => d.ID)
            .ToHashSet();

        var foreign = new List<string>();
        foreach (var tech in _proto.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (!ImperialTgForeignTech.IsForeign(_proto, tech))
                continue;
            disciplines.Add(tech.Discipline);
            foreign.Add(tech.ID);
        }

        if (foreign.Count == 0)
            return;

        var node = new MappingDataNode
        {
            ["supportedDisciplines"] = new SequenceDataNode(disciplines.Select(d => (DataNode) new ValueDataNode(d)).ToList()),
        };
        var database = _serialization.Read<TechnologyDatabaseComponent>(node, notNullableOverride: true);
        AddComp(template, database, overwrite: true);
        Log.Info($"Технологии вне дерева TG ({foreign.Count}): {string.Join(", ", foreign)}; дисциплин всего: {disciplines.Count}");
    }

    /// <summary>Консоль исследований открывает дерево TG вместо случайных карточек.</summary>
    private void OnConsoleMapInit(Entity<ResearchConsoleComponent> ent, ref MapInitEvent args)
    {
        _ui.SetUi(ent.Owner, ResearchConsoleUiKey.Key, new InterfaceData("ImperialTgResearchConsoleBoundUserInterface"));
    }

    /// <summary>Раскрыть скрытый узел или засчитать обязательный эксперимент на сервере исследований.</summary>
    public bool RevealNode(EntityUid server, string node)
    {
        if (!_proto.TryIndex<TechnologyPrototype>(GatePrefix + node, out var tech)
            && !_proto.TryIndex(GatePrefix + "Exp" + node, out tech)
            || _research.IsTechnologyUnlocked(server, tech))
            return false;

        _research.AddTechnology(server, tech);
        return true;
    }
}
