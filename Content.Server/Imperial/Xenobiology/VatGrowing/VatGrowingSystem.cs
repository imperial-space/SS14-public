using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.VatGrowing;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology.VatGrowing;

/// <summary>datum/micro_organism: клеточная линия с прогрессом роста или вирус.</summary>
public sealed class VatOrganism
{
    public ProtoId<CellLinePrototype>? Line;
    public bool Virus;
    public float Growth;

    public VatOrganism Copy()
    {
        return new VatOrganism { Line = Line, Virus = Virus };
    }
}

/// <summary>datum/biological_sample.</summary>
public sealed class VatSample
{
    public List<VatOrganism> Organisms = new();
    public int Layers = 1;
    public string Color = "yellow";
}

/// <summary>Тампон или инструмент для биопсии (component/swabbing).</summary>
[RegisterComponent]
public sealed partial class VatSwabComponent : Component
{
    [DataField]
    public bool CanSwabObjects = true;

    [DataField]
    public bool CanSwabMobs;

    [ViewVariables]
    public VatSample? Sample;
}

/// <summary>obj/item/petri_dish.</summary>
[RegisterComponent]
public sealed partial class VatPetriDishComponent : Component
{
    [ViewVariables]
    public VatSample? Sample;

    /// <summary>petri_dish/random: случайная проба при появлении.</summary>
    [DataField]
    public bool RandomSample;
}

/// <summary>obj/machinery/vatgrower.</summary>
[RegisterComponent]
public sealed partial class VatGrowerComponent : Component
{
    [DataField]
    public string Solution = "vat";

    /// <summary>Малый чан взрывается, если вырастить что-то крупнее мелкого существа.</summary>
    [DataField]
    public bool Small;

    [ViewVariables]
    public VatSample? Sample;

    [ViewVariables]
    public bool Resampler;

    [ViewVariables]
    public TimeSpan NextTick;
}

/// <summary>С этого объекта уже взята проба (swabable/Detach).</summary>
[RegisterComponent]
public sealed partial class VatSwabbedComponent : Component;

/// <summary>obj/structure/microscope.</summary>
[RegisterComponent]
public sealed partial class VatMicroscopeComponent : Component;

/// <summary>
/// Выращивание в чанах (code/modules/research/xenobiology/vatgrowing).
/// </summary>
public sealed class VatGrowingSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly string[] SampleColors = { "brown", "gray", "green", "purple", "yellow" };
    private static readonly ProtoId<ReagentPrototype> Spaceacillin = "Spaceacillin";
    private static readonly ProtoId<CellLineTablePrototype> CorgiTable = "CellTableCorgi";
    private static readonly ProtoId<CellLineTablePrototype> SnakeTable = "CellTableSnake";
    private static readonly ProtoId<CellLineTablePrototype> CockroachTable = "CellTableCockroach";
    private static readonly ProtoId<CellLineTablePrototype> BlobbernautTable = "CellTableBlobbernaut";

    /// <summary>REAGENTS_METABOLISM: сколько реагента линия съедает за тик.</summary>
    private const float ConsumptionRate = 0.2f;

    /// <summary>Тик машин SS13.</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    private static readonly SoundSpecifier Slosh = new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg");
    private static readonly SoundSpecifier Splat = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
    private static readonly SoundSpecifier Bubbles = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/bubbles.ogg");
    private static readonly SoundSpecifier Click = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VatSwabComponent, AfterInteractEvent>(OnSwabInteract);
        SubscribeLocalEvent<VatSwabComponent, VatSwabDoAfterEvent>(OnSwabDoAfter);
        SubscribeLocalEvent<VatSwabComponent, ExaminedEvent>(OnSwabExamined);
        SubscribeLocalEvent<VatPetriDishComponent, MapInitEvent>(OnPetriInit);
        SubscribeLocalEvent<VatPetriDishComponent, ExaminedEvent>(OnPetriExamined);
        SubscribeLocalEvent<VatGrowerComponent, InteractUsingEvent>(OnVatInteractUsing);
        SubscribeLocalEvent<VatGrowerComponent, InteractHandEvent>(OnVatInteractHand);
        SubscribeLocalEvent<VatGrowerComponent, GetVerbsEvent<AlternativeVerb>>(OnVatVerbs);
        SubscribeLocalEvent<VatGrowerComponent, ExaminedEvent>(OnVatExamined);
        SubscribeLocalEvent<VatGrowerComponent, SolutionContainerChangedEvent>(OnVatSolutionChanged);
        SubscribeLocalEvent<VatMicroscopeComponent, InteractUsingEvent>(OnMicroscopeInteract);
    }

    #region Пробы

    /// <summary>swabable/GenerateSample().</summary>
    private VatSample? GenerateSample(SwabSourcePrototype source)
    {
        return GenerateSample(source.Table, source.MinAmount, source.MaxAmount, source.VirusChance);
    }

    private VatSample? GenerateSample(ProtoId<CellLineTablePrototype> tableId, int minAmount, int maxAmount, float virusChance)
    {
        if (!_proto.TryIndex(tableId, out var table))
            return null;

        var sample = new VatSample { Color = _random.Pick(SampleColors) };
        var weights = table.Lines.Where(l => _proto.HasIndex(l.Key)).ToDictionary(l => l.Key, l => l.Value);
        var amount = _random.Next(minAmount, maxAmount + 1);
        for (var i = 0; i < amount && weights.Count > 0; i++)
        {
            var line = PickWeighted(weights);
            weights.Remove(line);
            sample.Organisms.Add(new VatOrganism { Line = line });
        }

        if (_random.Prob(virusChance / 100f))
            sample.Organisms.Add(new VatOrganism { Virus = true });

        return sample.Organisms.Count > 0 ? sample : null;
    }

    private ProtoId<CellLinePrototype> PickWeighted(Dictionary<ProtoId<CellLinePrototype>, float> weights)
    {
        var total = weights.Values.Sum();
        var roll = _random.NextFloat(total);
        foreach (var (line, weight) in weights)
        {
            roll -= weight;
            if (roll <= 0f)
                return line;
        }

        return weights.Keys.Last();
    }

    /// <summary>Источник пробы: прототип сущности (или его родители), либо реагент лужи.</summary>
    private SwabSourcePrototype? FindSource(EntityUid target)
    {
        var proto = MetaData(target).EntityPrototype;
        var ids = new HashSet<string>();
        if (proto != null)
            CollectParents(proto.ID, ids);

        PuddleComponent? puddle = null;
        Content.Shared.Chemistry.Components.Solution? puddleSolution = null;
        if (TryComp(target, out puddle) && _solutions.TryGetSolution(target, puddle.SolutionName, out _, out var sol))
            puddleSolution = sol;

        foreach (var source in _proto.EnumeratePrototypes<SwabSourcePrototype>())
        {
            if (source.Entities.Any(ids.Contains))
                return source;
            if (puddleSolution != null && source.PuddleReagents.Any(r => puddleSolution.GetTotalPrototypeQuantity(r) > 0))
                return source;
        }

        return null;
    }

    private void CollectParents(string id, HashSet<string> ids)
    {
        if (!ids.Add(id) || !_proto.TryIndex<EntityPrototype>(id, out var proto) || proto.Parents == null)
            return;
        foreach (var parent in proto.Parents)
        {
            CollectParents(parent, ids);
        }
    }

    private bool CanSwab(VatSwabComponent swab, EntityUid target)
    {
        return HasComp<MobStateComponent>(target) ? swab.CanSwabMobs : swab.CanSwabObjects;
    }

    #endregion

    #region Тампон

    private void OnSwabExamined(Entity<VatSwabComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Sample == null)
            return;
        args.PushMarkup(Loc.GetString("vat-swab-has-sample"));
        args.PushMarkup(Loc.GetString("vat-sample-organisms"));
        foreach (var organism in ent.Comp.Sample.Organisms)
        {
            args.PushMarkup(Details(organism, false));
        }
    }

    /// <summary>swabbing/try_to_swab(): мазок с цели или перенос пробы в чашку Петри.</summary>
    private void OnSwabInteract(Entity<VatSwabComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<VatPetriDishComponent>(target, out var dish))
        {
            if (ent.Comp.Sample == null || dish.Sample != null)
                return;
            args.Handled = true;
            dish.Sample = ent.Comp.Sample;
            ent.Comp.Sample = null;
            UpdateSwabVisuals(ent);
            UpdatePetriVisuals((target, dish));
            _popup.PopupEntity(Loc.GetString("vat-petri-deposit", ("dish", target)), args.User, args.User);
            return;
        }

        if (!CanSwab(ent.Comp, target))
            return;

        args.Handled = true;
        if (ent.Comp.Sample != null)
        {
            _popup.PopupEntity(Loc.GetString("vat-swab-full", ("swab", ent.Owner)), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("vat-swab-start", ("target", target)), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new VatSwabDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSwabDoAfter(Entity<VatSwabComponent> ent, ref VatSwabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;
        args.Handled = true;

        var source = HasComp<VatSwabbedComponent>(target) ? null : FindSource(target);
        var sample = source != null ? GenerateSample(source) : null;
        if (sample == null)
        {
            _popup.PopupEntity(Loc.GetString("vat-swab-nothing", ("target", target)), args.User, args.User);
            return;
        }

        EnsureComp<VatSwabbedComponent>(target);
        ent.Comp.Sample = sample;
        UpdateSwabVisuals(ent);
        _popup.PopupEntity(Loc.GetString("vat-swab-success", ("target", target)), args.User, args.User);
    }

    private void UpdateSwabVisuals(Entity<VatSwabComponent> ent)
    {
        _appearance.SetData(ent, VatGrowingVisuals.SampleColor, ent.Comp.Sample?.Color ?? "none");
    }

    #endregion

    #region Чашка Петри

    /// <summary>petri_dish/random: корги, змея, тараканы или блоббернаут.</summary>
    private void OnPetriInit(Entity<VatPetriDishComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.RandomSample)
            return;

        var options = new (ProtoId<CellLineTablePrototype>, float)[] { (CorgiTable, 5f), (SnakeTable, 5f), (CockroachTable, 7f), (BlobbernautTable, 5f) };
        var (table, virus) = _random.Pick(options);
        ent.Comp.Sample = GenerateSample(table, 1, 1, virus);
        UpdatePetriVisuals(ent);
    }

    private void OnPetriExamined(Entity<VatPetriDishComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Sample == null)
            return;
        args.PushMarkup(Loc.GetString("vat-sample-organisms"));
        foreach (var organism in ent.Comp.Sample.Organisms)
        {
            args.PushMarkup(Details(organism, false));
        }
    }

    private void UpdatePetriVisuals(Entity<VatPetriDishComponent> ent)
    {
        _appearance.SetData(ent, VatGrowingVisuals.SampleColor, ent.Comp.Sample?.Color ?? "none");
    }

    #endregion

    #region Описание

    /// <summary>micro_organism/get_details().</summary>
    private string Details(VatOrganism organism, bool full)
    {
        if (organism.Virus || organism.Line is not { } lineId || !_proto.TryIndex(lineId, out var line))
            return Loc.GetString("vat-organism-virus");

        var text = Loc.GetString("vat-organism-line", ("desc", Loc.GetString(line.Desc)), ("growth", (int) organism.Growth));
        if (!full)
            return text;

        string Names(IEnumerable<ProtoId<ReagentPrototype>> reagents)
        {
            var names = reagents.Select(r => _proto.TryIndex(r, out var p) ? p.LocalizedName : r.Id).ToList();
            return names.Count == 0 ? Loc.GetString("vat-organism-nothing") : string.Join(", ", names);
        }

        text += "\n" + Loc.GetString("vat-organism-requires", ("list", Names(line.Required)));
        text += "\n" + Loc.GetString("vat-organism-likes", ("list", Names(line.Supplementary.Keys)));
        text += "\n" + Loc.GetString("vat-organism-hates", ("list", Names(line.Suppressive.Keys)));
        return text;
    }

    /// <summary>Микроскоп: подробности пробы на чашке Петри.</summary>
    private void OnMicroscopeInteract(Entity<VatMicroscopeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        VatSample? sample = null;
        if (TryComp<VatPetriDishComponent>(args.Used, out var dish))
            sample = dish.Sample;
        else if (TryComp<VatSwabComponent>(args.Used, out var swab))
            sample = swab.Sample;
        else
            return;

        args.Handled = true;
        if (sample == null)
        {
            _popup.PopupEntity(Loc.GetString("vat-microscope-empty"), ent, args.User);
            return;
        }

        var text = Loc.GetString("vat-microscope-header") + "\n" + string.Join("\n", sample.Organisms.Select(o => Details(o, true)));
        if (TryComp<ActorComponent>(args.User, out var actor))
            _chat.ChatMessageToOne(ChatChannel.Server, text, text, EntityUid.Invalid, false, actor.PlayerSession.Channel);
    }

    #endregion

    #region Чан

    private void OnVatExamined(Entity<VatGrowerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Resampler ? "vat-resampler-on" : "vat-resampler-off"));
        if (ent.Comp.Sample == null)
            return;
        args.PushMarkup(Loc.GetString("vat-has-sample"));
        foreach (var organism in ent.Comp.Sample.Organisms)
        {
            args.PushMarkup(Details(organism, false));
        }
    }

    /// <summary>deposit_sample(): проба с чашки Петри копируется в чан.</summary>
    private void OnVatInteractUsing(Entity<VatGrowerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<VatPetriDishComponent>(args.Used, out var dish))
            return;
        args.Handled = true;

        if (dish.Sample == null)
        {
            _popup.PopupEntity(Loc.GetString("vat-dish-empty"), ent, args.User);
            return;
        }

        if (ent.Comp.Sample != null)
        {
            _popup.PopupEntity(Loc.GetString("vat-already-sample"), ent, args.User);
            return;
        }

        ent.Comp.Sample = new VatSample
        {
            Organisms = dish.Sample.Organisms.Select(o => o.Copy()).ToList(),
            Layers = dish.Sample.Layers,
            Color = dish.Sample.Color,
        };
        _popup.PopupEntity(Loc.GetString("vat-added-sample"), ent, args.User);
        _audio.PlayPvs(Bubbles, ent);
        UpdateVatVisuals(ent);
    }

    /// <summary>attack_hand(): переключить ресемплер.</summary>
    private void OnVatInteractHand(Entity<VatGrowerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        ent.Comp.Resampler = !ent.Comp.Resampler;
        _audio.PlayPvs(Click, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Resampler ? "vat-resampler-activated" : "vat-resampler-deactivated"), ent, args.User);
        UpdateVatVisuals(ent);
    }

    /// <summary>attack_hand_secondary(): слить содержимое.</summary>
    private void OnVatVerbs(Entity<VatGrowerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("vat-flush-verb"),
            Act = () =>
            {
                if (_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var sol, out _))
                    _solutions.RemoveAllSolution(sol.Value);
                ent.Comp.Sample = null;
                _popup.PopupEntity(Loc.GetString("vat-flushed"), ent, user);
                UpdateVatVisuals(ent);
            },
        });
    }

    private void OnVatSolutionChanged(Entity<VatGrowerComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId == ent.Comp.Solution)
            _appearance.SetData(ent, VatGrowingVisuals.HasReagents, args.Solution.Volume > 0);
    }

    private void UpdateVatVisuals(Entity<VatGrowerComponent> ent)
    {
        _appearance.SetData(ent, VatGrowingVisuals.Resampler, ent.Comp.Resampler);
        _appearance.SetData(ent, VatGrowingVisuals.Growing, ent.Comp.Sample != null);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VatGrowerComponent>();
        while (query.MoveNext(out var uid, out var vat))
        {
            if (vat.Sample == null || now < vat.NextTick)
                continue;
            vat.NextTick = now + TickInterval;
            Grow((uid, vat));
        }
    }

    /// <summary>biological_sample/handle_growth().</summary>
    private void Grow(Entity<VatGrowerComponent> ent)
    {
        if (ent.Comp.Sample is not { } sample || !_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solEnt, out var solution))
            return;

        var anyGrowing = false;
        foreach (var organism in sample.Organisms.ToList())
        {
            if (organism.Virus || organism.Line is not { } lineId || !_proto.TryIndex(lineId, out var line))
                continue;

            // try_eat(): все обязательные реагенты должны быть в чане.
            if (line.Required.Any(r => solution.GetTotalPrototypeQuantity(r) <= 0))
                continue;
            foreach (var reagent in line.Required)
            {
                _solutions.RemoveReagent(solEnt.Value, reagent, ConsumptionRate);
            }

            anyGrowing = true;
            var delta = line.GrowthRate;
            foreach (var (reagent, bonus) in line.Supplementary.Concat(line.Suppressive))
            {
                if (solution.GetTotalPrototypeQuantity(reagent) < ConsumptionRate)
                    continue;
                delta += bonus;
                _solutions.RemoveReagent(solEnt.Value, reagent, ConsumptionRate);
            }

            foreach (var _ in sample.Organisms.Where(o => o.Virus))
            {
                if (solution.GetTotalPrototypeQuantity(Spaceacillin) >= ConsumptionRate)
                {
                    _solutions.RemoveReagent(solEnt.Value, Spaceacillin, ConsumptionRate);
                    continue;
                }

                delta -= line.VirusSuspectibility;
            }

            organism.Growth = Math.Max(organism.Growth, organism.Growth + delta);
            if (organism.Growth < 100f)
                continue;

            FinishGrowing(ent, line);
            if (ent.Comp.Sample == null)
                return;
        }

        if (anyGrowing && _random.Prob(0.1f))
        {
            _audio.PlayPvs(Slosh, ent);
            _popup.PopupEntity(Loc.GetString(_random.Pick(new[] { "vat-grumbles", "vat-splashes", "vat-sloshes" }), ("vat", ent.Owner)), ent);
        }
    }

    /// <summary>finish_growing() + on_sample_growth_completed().</summary>
    private void FinishGrowing(Entity<VatGrowerComponent> ent, CellLinePrototype line)
    {
        _audio.PlayPvs(Splat, ent);
        var coords = _transform.GetMoverCoordinates(ent);
        for (var i = 0; i < line.ResultCount; i++)
        {
            var thing = Spawn(_random.Pick(line.Results), coords);
            _popup.PopupEntity(Loc.GetString("vat-pops-out", ("thing", thing), ("vat", ent.Owner)), thing, PopupType.Medium);

            // vatgrower/small: слишком крупное существо разрывает малый чан.
            if (ent.Comp.Small && HasComp<MobStateComponent>(thing) && !HasComp<Content.Shared.Item.ItemComponent>(thing)
                && TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(thing, out var physics) && physics.Mass > 20f)
            {
                _popup.PopupEntity(Loc.GetString("vat-small-burst", ("vat", ent.Owner)), ent, PopupType.LargeCaution);
                QueueDel(ent);
                return;
            }
        }

        if (ent.Comp.Resampler && ent.Comp.Sample != null)
        {
            foreach (var organism in ent.Comp.Sample.Organisms)
            {
                organism.Growth = 0f;
            }
        }
        else
        {
            ent.Comp.Sample = null;
        }

        UpdateVatVisuals(ent);
    }

    #endregion
}
