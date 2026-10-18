using Content.Shared.Imperial.Research;
using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Contraband;
using Content.Shared.Examine;
using Content.Shared.Imperial.Research.Experiments;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research;

/// <summary>
/// Раскрытие скрытых узлов TG: технодиски BEPIS (design_disk/bepis) и деструктивный анализатор
/// (required_items_to_unlock: инопланетные технологии, предметы Синдиката).
/// </summary>
public sealed class TgRevealSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ImperialTgResearchSystem _tg = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TgExperimentSystem _experiments = default!;

    private static readonly SoundSpecifier UploadSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
    private static readonly SoundSpecifier GrindSound = new SoundPathSpecifier("/Audio/Machines/blender.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TgBepisDiskComponent, MapInitEvent>(OnDiskMapInit);
        SubscribeLocalEvent<TgBepisDiskComponent, ExaminedEvent>(OnDiskExamined);
        SubscribeLocalEvent<TgBepisDiskComponent, AfterInteractEvent>(OnDiskAfterInteract);

        SubscribeLocalEvent<TgDestructiveAnalyzerComponent, GetVerbsEvent<AlternativeVerb>>(OnAnalyzerVerbs);
        SubscribeLocalEvent<TgDestructiveAnalyzerComponent, EntInsertedIntoContainerMessage>(OnAnalyzerContainerChanged);
        SubscribeLocalEvent<TgDestructiveAnalyzerComponent, EntRemovedFromContainerMessage>(OnAnalyzerContainerChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TgDestructiveAnalyzerComponent>();
        while (query.MoveNext(out var uid, out var analyzer))
        {
            if (analyzer.DoneAt is not { } done || now < done)
                continue;

            analyzer.DoneAt = null;
            FinishAnalysis((uid, analyzer));
        }
    }

    private static string ToCamel(string node)
    {
        return string.Concat(node.Split('_').Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private string NodeName(string node)
    {
        return _proto.TryIndex<TechnologyPrototype>(ImperialTgResearchSystem.TechPrefix + ToCamel(node), out var tech)
            ? Loc.GetString(tech.Name)
            : node;
    }

    #region Технодиск BEPIS

    private void OnDiskMapInit(Entity<TgBepisDiskComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Node == null && ent.Comp.Nodes.Count > 0)
            ent.Comp.Node = _random.Pick(ent.Comp.Nodes);
    }

    private void OnDiskExamined(Entity<TgBepisDiskComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Node != null)
            args.PushMarkup(Loc.GetString("tg-bepis-disk-examine", ("node", NodeName(ent.Comp.Node))));
    }

    /// <summary>on_upload(): узел раскрывается и изучается без затрат очков.</summary>
    private void OnDiskAfterInteract(Entity<TgBepisDiskComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || ent.Comp.Node is not { } node)
            return;

        EntityUid? server = null;
        if (HasComp<ResearchServerComponent>(target))
            server = target;
        else if (HasComp<ResearchConsoleComponent>(target) && TryComp<ResearchClientComponent>(target, out var client))
            server = client.Server;

        if (server == null)
            return;

        args.Handled = true;
        var techId = ImperialTgResearchSystem.TechPrefix + ToCamel(node);
        if (!_proto.TryIndex<TechnologyPrototype>(techId, out var tech) || _research.IsTechnologyUnlocked(server.Value, tech))
        {
            _popup.PopupEntity(Loc.GetString("tg-bepis-disk-already"), args.User, args.User);
            return;
        }

        _tg.RevealNode(server.Value, ToCamel(node));
        _research.AddTechnology(server.Value, tech);
        _audio.PlayPvs(UploadSound, target);
        _popup.PopupEntity(Loc.GetString("tg-bepis-disk-uploaded", ("node", NodeName(node))), args.User, args.User);
        _radio.SendRadioMessage(server.Value, Loc.GetString("tg-bepis-disk-radio", ("node", NodeName(node))), "Science", server.Value);
        QueueDel(ent);
    }

    #endregion

    #region Деструктивный анализатор

    private void OnAnalyzerContainerChanged<T>(Entity<TgDestructiveAnalyzerComponent> ent, ref T args)
    {
        UpdateAnalyzerVisuals(ent);
    }

    private void UpdateAnalyzerVisuals(Entity<TgDestructiveAnalyzerComponent> ent)
    {
        var state = ent.Comp.DoneAt != null ? "process" : _itemSlots.GetItemOrNull(ent, ent.Comp.Slot) != null ? "loaded" : "icon";
        _appearance.SetData(ent, TgDestructiveAnalyzerVisuals.State, state);
    }

    private void OnAnalyzerVerbs(Entity<TgDestructiveAnalyzerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.DoneAt != null || _itemSlots.GetItemOrNull(ent, ent.Comp.Slot) == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("tg-analyzer-verb"),
            Priority = 2,
            Act = () => StartAnalysis(ent, user),
        });
    }

    private void StartAnalysis(Entity<TgDestructiveAnalyzerComponent> ent, EntityUid user)
    {
        if (!this.IsPowered(ent, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("tg-analyzer-no-power"), ent, user);
            return;
        }

        if (_itemSlots.TryGetSlot(ent, ent.Comp.Slot, out var slot))
            _itemSlots.SetLock(ent, slot, true);

        ent.Comp.User = user;
        ent.Comp.DoneAt = _timing.CurTime + ent.Comp.ProcessTime;
        _audio.PlayPvs(GrindSound, ent);
        UpdateAnalyzerVisuals(ent);
    }

    /// <summary>destroy_item(): предмет уничтожается, подходящие скрытые узлы раскрываются.</summary>
    private void FinishAnalysis(Entity<TgDestructiveAnalyzerComponent> ent)
    {
        if (_itemSlots.TryGetSlot(ent, ent.Comp.Slot, out var slot))
            _itemSlots.SetLock(ent, slot, false);

        var item = _itemSlots.GetItemOrNull(ent, ent.Comp.Slot);
        var server = _experiments.FindServer(ent);
        if (item == null)
        {
            UpdateAnalyzerVisuals(ent);
            return;
        }

        var revealed = new List<string>();
        if (server != null)
        {
            foreach (var rule in _proto.EnumeratePrototypes<TgRevealRulePrototype>())
            {
                if (Matches(rule, item.Value) && _tg.RevealNode(server.Value, ToCamel(rule.Node)))
                    revealed.Add(NodeName(rule.Node));
            }
        }

        // techweb_point_items: ядро аномалии даёт TECHWEB_TIER_5_POINTS один раз на тип (deconstructed_items).
        var points = 0;
        var itemProto = MetaData(item.Value).EntityPrototype?.ID;
        if (server != null && itemProto != null && itemProto.StartsWith("AnomalyCore") && !itemProto.EndsWith("Inert"))
        {
            var state = EnsureComp<ImperialTgServerStateComponent>(server.Value);
            if (state.DeconstructedItems.Add(itemProto))
            {
                points = 200 * ImperialTgTechweb.PointScale;
                _research.ModifyServerPoints(server.Value, points);
            }
        }

        Del(item.Value);
        UpdateAnalyzerVisuals(ent);

        var message = revealed.Count > 0
            ? Loc.GetString("tg-analyzer-revealed", ("nodes", string.Join(", ", revealed)))
            : points > 0
                ? Loc.GetString("tg-analyzer-points", ("points", points))
                : Loc.GetString("tg-analyzer-nothing");
        _popup.PopupEntity(message, ent);

        if ((revealed.Count > 0 || points > 0) && server != null)
            _radio.SendRadioMessage(server.Value, message, "Science", server.Value);
    }

    private bool Matches(TgRevealRulePrototype rule, EntityUid item)
    {
        var proto = MetaData(item).EntityPrototype?.ID;
        if (proto != null && rule.Prototypes.Any(p => p.Id == proto))
            return true;

        if (rule.Whitelist != null && _whitelist.IsValid(rule.Whitelist, item))
            return true;

        return rule.ContrabandSeverity != null
               && TryComp<ContrabandComponent>(item, out var contraband)
               && contraband.Severity.Id == rule.ContrabandSeverity;
    }

    #endregion
}
