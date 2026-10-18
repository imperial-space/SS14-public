using System.Linq;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Forensics.Components;
using Content.Shared.Imperial.Disease;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>
/// PanD.E.M.I.C 2200 (pandemic.dm): анализ образца крови, печать культуры и вакцины.
/// Образец крови в SS14 хранится компонентом на самом контейнере.
/// </summary>
public sealed partial class DiseaseSystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    /// <summary>Шприц, которым недавно брали кровь: ждём, когда в него нальётся кровь.</summary>
    private readonly Dictionary<EntityUid, (EntityUid Source, TimeSpan At)> _pendingDraws = new();

    private void InitializeMachines()
    {
        SubscribeLocalEvent<PandemicComponent, BoundUIOpenedEvent>(OnPandemicUiOpened);
        SubscribeLocalEvent<PandemicComponent, EntInsertedIntoContainerMessage>(OnPandemicContainerChanged);
        SubscribeLocalEvent<PandemicComponent, EntRemovedFromContainerMessage>(OnPandemicContainerChanged);
        SubscribeLocalEvent<PandemicComponent, PandemicEjectBeakerMessage>(OnEjectBeaker);
        SubscribeLocalEvent<PandemicComponent, PandemicEmptyBeakerMessage>(OnEmptyBeaker);
        SubscribeLocalEvent<PandemicComponent, PandemicCreateCultureMessage>(OnCreateCulture);
        SubscribeLocalEvent<PandemicComponent, PandemicCreateVaccineMessage>(OnCreateVaccine);
        SubscribeLocalEvent<PandemicComponent, PandemicRenameDiseaseMessage>(OnRenameDisease);

        SubscribeLocalEvent<DiseasedComponent, InteractUsingEvent>(OnDiseasedInteractUsing);
        SubscribeLocalEvent<DiseaseImmunityComponent, InteractUsingEvent>(OnImmuneInteractUsing);
        SubscribeLocalEvent<DiseaseSampleComponent, SolutionContainerChangedEvent>(OnSampleSolutionChanged);
        SubscribeLocalEvent<Content.Shared.Body.BodyComponent, InteractUsingEvent>(OnBodyInteractUsing);
    }

    #region Образцы крови

    /// <summary>Запоминаем, у кого набирают кровь, чтобы перенести в шприц болезни и антитела.</summary>
    private void OnDiseasedInteractUsing(Entity<DiseasedComponent> ent, ref InteractUsingEvent args)
    {
        RememberDraw(ent, args.Used);
    }

    private void OnImmuneInteractUsing(Entity<DiseaseImmunityComponent> ent, ref InteractUsingEvent args)
    {
        RememberDraw(ent, args.Used);
    }

    private void RememberDraw(EntityUid source, EntityUid used)
    {
        if (!HasComp<Content.Shared.Chemistry.Components.InjectorComponent>(used))
            return;

        _pendingDraws[used] = (source, _timing.CurTime + TimeSpan.FromSeconds(10));
    }

    /// <summary>Перенос болезней из носителя в ёмкость вместе с кровью.</summary>
    public void CopySampleFrom(EntityUid container, EntityUid source)
    {
        var sample = EnsureComp<DiseaseSampleComponent>(container);
        sample.Diseases.Clear();

        if (TryComp<DiseasedComponent>(source, out var diseased))
        {
            foreach (var disease in diseased.Diseases)
            {
                sample.Diseases.Add(disease.Copy());
            }
        }

        sample.Resistances.Clear();
        if (TryComp<DiseaseImmunityComponent>(source, out var immunity))
            sample.Resistances.UnionWith(immunity.Resistances);

        if (TryComp<DnaComponent>(source, out var dna))
            sample.Dna = dna.DNA;
    }

    /// <summary>Образец без крови теряет смысл.</summary>
    private void OnSampleSolutionChanged(Entity<DiseaseSampleComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.Solution.GetTotalPrototypeQuantity("Blood") <= 0)
            RemCompDeferred<DiseaseSampleComponent>(ent);
    }

    /// <summary>Укол вакциной или культурой: содержимое шприца попадает в кровь цели.</summary>
    private void OnBodyInteractUsing(Entity<Content.Shared.Body.BodyComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<Content.Shared.Chemistry.Components.InjectorComponent>(args.Used))
            return;

        if (!HasComp<DiseaseVaccineComponent>(args.Used) && !HasComp<DiseaseSampleComponent>(args.Used))
            return;

        _pendingInjections[args.Used] = (ent.Owner, _timing.CurTime + TimeSpan.FromSeconds(10));
    }

    /// <summary>Когда шприц опустел, его содержимое считается введённым.</summary>
    private void UpdateInjections(TimeSpan now)
    {
        foreach (var (injector, data) in _pendingInjections.ToArray())
        {
            if (!Exists(injector) || !Exists(data.Source) || now > data.At)
            {
                _pendingInjections.Remove(injector);
                continue;
            }

            if (GetBloodSolution(injector) != null)
                continue;

            _pendingInjections.Remove(injector);

            if (TryComp<DiseaseVaccineComponent>(injector, out var vaccine))
                ApplyVaccine(data.Source, vaccine.Diseases);

            if (TryComp<DiseaseSampleComponent>(injector, out var sample))
            {
                foreach (var disease in sample.Diseases)
                {
                    TryInfect(data.Source, disease);
                }
            }
        }
    }

    private readonly Dictionary<EntityUid, (EntityUid Source, TimeSpan At)> _pendingInjections = new();

    /// <summary>reagent/vaccine/expose_mob(): вакцина даёт иммунитет и лечит свою болезнь.</summary>
    public void ApplyVaccine(EntityUid target, IEnumerable<string> diseases)
    {
        var immunity = EnsureComp<DiseaseImmunityComponent>(target);
        foreach (var id in diseases)
        {
            immunity.Resistances.Add(id);

            if (!TryComp<DiseasedComponent>(target, out var diseased))
                continue;

            foreach (var disease in diseased.Diseases.ToArray())
            {
                if (disease.GetId() == id)
                    Cure(target, disease);
            }
        }
    }

    private void UpdateDraws(TimeSpan now)
    {
        foreach (var (injector, data) in _pendingDraws.ToArray())
        {
            if (now <= data.At && Exists(injector) && Exists(data.Source))
            {
                // Кровь набрана — переносим вместе с ней болезни.
                if (HasBlood(injector))
                {
                    CopySampleFrom(injector, data.Source);
                    _pendingDraws.Remove(injector);
                }

                continue;
            }

            _pendingDraws.Remove(injector);
        }
    }

    private bool HasBlood(EntityUid uid)
    {
        return GetBloodSolution(uid) is { } solution && solution.GetTotalPrototypeQuantity("Blood") > 0;
    }

    private Solution? GetBloodSolution(EntityUid uid)
    {
        if (!TryComp<SolutionContainerManagerComponent>(uid, out var manager))
            return null;

        foreach (var (name, solution) in _solutions.EnumerateSolutions((uid, manager)))
        {
            _ = solution;
            if (_solutions.TryGetSolution((uid, manager), name, out _, out var contents)
                && contents.GetTotalPrototypeQuantity("Blood") > 0)
                return contents;
        }

        return null;
    }

    #endregion

    #region PanD.E.M.I.C

    private void OnPandemicUiOpened(Entity<PandemicComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdatePandemicUi(ent);
    }

    private void OnPandemicContainerChanged<T>(Entity<PandemicComponent> ent, ref T args)
    {
        UpdatePandemicUi(ent);
    }

    private EntityUid? GetBeaker(Entity<PandemicComponent> ent)
    {
        return _itemSlots.GetItemOrNull(ent, ent.Comp.BeakerSlot);
    }

    private void UpdatePandemicUi(Entity<PandemicComponent> ent)
    {
        var ready = ent.Comp.ReadyAt == null || _timing.CurTime >= ent.Comp.ReadyAt;
        var beaker = GetBeaker(ent);

        if (beaker == null)
        {
            _ui.SetUiState(ent.Owner, PandemicUiKey.Key, new PandemicBoundUserInterfaceState(
                ready, false, false, 0, 0, "", "", Array.Empty<PandemicDiseaseData>(), Array.Empty<PandemicResistanceData>()));
            return;
        }

        var solution = GetBloodSolution(beaker.Value);
        var volume = 0f;
        var capacity = 0f;
        if (TryComp<SolutionContainerManagerComponent>(beaker.Value, out var manager))
        {
            foreach (var (name, _) in _solutions.EnumerateSolutions((beaker.Value, manager)))
            {
                if (!_solutions.TryGetSolution((beaker.Value, manager), name, out _, out var sol))
                    continue;

                volume += (float) sol.Volume;
                capacity += (float) sol.MaxVolume;
            }
        }

        if (solution == null || !TryComp<DiseaseSampleComponent>(beaker.Value, out var sample))
        {
            _ui.SetUiState(ent.Owner, PandemicUiKey.Key, new PandemicBoundUserInterfaceState(
                ready, true, false, volume, capacity, "", "", Array.Empty<PandemicDiseaseData>(), Array.Empty<PandemicResistanceData>()));
            return;
        }

        var diseases = new List<PandemicDiseaseData>();
        var index = 1;
        foreach (var disease in sample.Diseases)
        {
            if ((disease.Visibility & DiseaseVisibility.Pandemic) != 0)
                continue;

            diseases.Add(BuildDiseaseData(disease, index++));
        }

        var resistances = new List<PandemicResistanceData>();
        foreach (var id in sample.Resistances)
        {
            resistances.Add(new PandemicResistanceData(id, GetDiseaseNameById(id)));
        }

        _ui.SetUiState(ent.Owner, PandemicUiKey.Key, new PandemicBoundUserInterfaceState(
            ready, true, true, volume, capacity, sample.Dna ?? Loc.GetString("pandemic-none"),
            sample.BloodType ?? Loc.GetString("pandemic-none"), diseases.ToArray(), resistances.ToArray()));
    }

    private string GetDiseaseNameById(string id)
    {
        if (_proto.TryIndex<DiseasePrototype>(id, out var proto))
            return Loc.GetString(proto.Name);

        return GetDiseaseName(id);
    }

    private PandemicDiseaseData BuildDiseaseData(DiseaseInstance disease, int index)
    {
        var symptoms = new List<PandemicSymptomData>();
        if (disease.Advanced)
        {
            foreach (var symptom in disease.Symptoms)
            {
                if (!_proto.TryIndex(symptom.Proto, out var proto))
                    continue;

                var thresholds = proto.Thresholds
                    .Select(t => Loc.GetString("pandemic-threshold",
                        ("property", Loc.GetString($"disease-property-{t.Property.ToString().ToLowerInvariant()}")),
                        ("value", t.Value),
                        ("description", Loc.GetString(t.Description))))
                    .ToArray();

                var cure = proto.Cure is { } cureId && _proto.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(cureId, out var cureProto)
                    ? cureProto.LocalizedName
                    : Loc.GetString("pandemic-none");

                var name = symptom.Neutered
                    ? Loc.GetString("pandemic-symptom-neutered", ("name", Loc.GetString(proto.Name)))
                    : Loc.GetString(proto.Name);

                symptoms.Add(new PandemicSymptomData(name, Loc.GetString(proto.Description), proto.Stealth,
                    proto.Resistance, proto.StageSpeed, proto.Transmittable, proto.Level, cure, thresholds));
            }
        }

        var cureText = disease.Advanced
            ? Loc.GetString("pandemic-cure-symptoms")
            : string.Join(", ", disease.Cures.Select(c =>
                _proto.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(c, out var p) ? p.LocalizedName : c.Id));

        var displayName = disease.Advanced ? GetDiseaseName(disease.GetId()) : disease.Name;
        var canRename = disease.Advanced && disease.Mutable && displayName == Loc.GetString("disease-name-unknown");

        return new PandemicDiseaseData(index, displayName, Loc.GetString(disease.Agent),
            disease.Description is { } desc ? Loc.GetString(desc) : Loc.GetString("pandemic-none"),
            GetSpreadText(disease), string.IsNullOrEmpty(cureText) ? Loc.GetString("pandemic-none") : cureText,
            disease.Advanced, canRename, disease.Resistance, disease.StageRate, disease.Stealth,
            disease.Transmittable, disease.SeverityValue, symptoms.ToArray());
    }

    private DiseaseInstance? GetDiseaseByIndex(Entity<PandemicComponent> ent, int index)
    {
        if (GetBeaker(ent) is not { } beaker || !TryComp<DiseaseSampleComponent>(beaker, out var sample))
            return null;

        var i = 1;
        foreach (var disease in sample.Diseases)
        {
            if ((disease.Visibility & DiseaseVisibility.Pandemic) != 0)
                continue;

            if (i++ == index)
                return disease;
        }

        return null;
    }

    private void OnEjectBeaker(Entity<PandemicComponent> ent, ref PandemicEjectBeakerMessage args)
    {
        _itemSlots.TryEject(ent, ent.Comp.BeakerSlot, args.Actor, out _);
        UpdatePandemicUi(ent);
    }

    private void OnEmptyBeaker(Entity<PandemicComponent> ent, ref PandemicEmptyBeakerMessage args)
    {
        if (GetBeaker(ent) is not { } beaker || !TryComp<SolutionContainerManagerComponent>(beaker, out var manager))
            return;

        foreach (var (_, solution) in _solutions.EnumerateSolutions((beaker, manager)))
        {
            _solutions.RemoveAllSolution(solution);
        }

        RemComp<DiseaseSampleComponent>(beaker);
        UpdatePandemicUi(ent);
    }

    /// <summary>create_culture_bottle(): пробирка с культурой вируса.</summary>
    private void OnCreateCulture(Entity<PandemicComponent> ent, ref PandemicCreateCultureMessage args)
    {
        if (ent.Comp.ReadyAt != null && _timing.CurTime < ent.Comp.ReadyAt)
            return;

        if (GetDiseaseByIndex(ent, args.Index) is not { } disease)
            return;

        if (!disease.Advanced || !disease.Mutable)
        {
            _popup.PopupEntity(Loc.GetString("pandemic-cannot-replicate"), ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        var bottle = Spawn(ent.Comp.BottleProto, _transform.GetMoverCoordinates(ent));
        var copy = disease.Copy();
        var sample = EnsureComp<DiseaseSampleComponent>(bottle);
        sample.Diseases.Add(copy);

        _metaData.SetEntityName(bottle, Loc.GetString("pandemic-culture-name", ("disease", GetDiseaseName(copy.GetId()))));
        _metaData.SetEntityDescription(bottle, Loc.GetString("pandemic-culture-desc", ("agent", Loc.GetString(copy.Agent))));
        FillBottle(bottle, "Blood", 20);

        ent.Comp.ReadyAt = _timing.CurTime + ent.Comp.CultureDelay;
        UpdatePandemicUi(ent);
    }

    /// <summary>create_vaccine_bottle(): пробирка с вакциной.</summary>
    private void OnCreateVaccine(Entity<PandemicComponent> ent, ref PandemicCreateVaccineMessage args)
    {
        if (ent.Comp.ReadyAt != null && _timing.CurTime < ent.Comp.ReadyAt)
            return;

        var bottle = Spawn(ent.Comp.BottleProto, _transform.GetMoverCoordinates(ent));
        var vaccine = EnsureComp<DiseaseVaccineComponent>(bottle);
        vaccine.Diseases.Add(args.Id);

        _metaData.SetEntityName(bottle, Loc.GetString("pandemic-vaccine-name", ("disease", GetDiseaseNameById(args.Id))));
        FillBottle(bottle, "Vaccine", 15);

        ent.Comp.ReadyAt = _timing.CurTime + ent.Comp.VaccineDelay;
        UpdatePandemicUi(ent);
    }

    private void FillBottle(EntityUid bottle, string reagent, float amount)
    {
        if (!TryComp<SolutionContainerManagerComponent>(bottle, out var manager))
            return;

        foreach (var (_, solution) in _solutions.EnumerateSolutions((bottle, manager)))
        {
            _solutions.TryAddReagent(solution, reagent, Content.Shared.FixedPoint.FixedPoint2.New(amount), out _);
            return;
        }
    }

    /// <summary>rename_disease(): имя безымянной болезни закрепляется за её набором симптомов.</summary>
    private void OnRenameDisease(Entity<PandemicComponent> ent, ref PandemicRenameDiseaseMessage args)
    {
        if (GetDiseaseByIndex(ent, args.Index) is not { } disease || !disease.Advanced || !disease.Mutable)
            return;

        var name = args.Name.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 42)
            return;

        AssignName(disease, name);
        UpdatePandemicUi(ent);
    }

    #endregion

    [Dependency] private readonly MetaDataSystem _metaData = default!;
}
