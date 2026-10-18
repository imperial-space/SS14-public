using System.Globalization;
using System.Linq;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.Imperial.Genetics.Mutations;
using Content.Shared.FixedPoint;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// Консоль генетики (dna_console из SS13): сканер, секвенсор, хранилища мутаций (консоль, дискета, продвинутые
/// инжекторы), хромосомы, комбинирование, ферменты и буферы генетического облика.
/// </summary>
public sealed class DnaConsoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly Content.Shared.Damage.Systems.DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GeneticMakeupSystem _makeup = default!;
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MedicalScannerSystem _scanner = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly char[] GeneLetters = { 'A', 'T', 'C', 'G' };

    /// <summary>GENETIC_DAMAGE_STRENGTH_MULTIPLIER и GENETIC_DAMAGE_ACCURACY_MULTIPLIER из SS13.</summary>
    private const int StrengthMultiplier = 1;
    private const int AccuracyMultiplier = 3;
    public const int PulseStrengthMax = 15;
    public const int PulseDurationMax = 30;

    /// <summary>Шанс получить хромосому из использованного активатора-исследования.</summary>
    private const float ChromosomeChance = 0.6f;

    public static readonly Dictionary<ChromosomeKind, EntProtoId> ChromosomeProtos = new()
    {
        [ChromosomeKind.Stabilizer] = "ImperialChromosomeStabilizer",
        [ChromosomeKind.Synchronizer] = "ImperialChromosomeSynchronizer",
        [ChromosomeKind.Power] = "ImperialChromosomePower",
        [ChromosomeKind.Energy] = "ImperialChromosomeEnergy",
    };

    /// <summary>Веса хромосом (generate_chromosome): стабилизатор редкий.</summary>
    private static readonly (ChromosomeKind Kind, float Weight)[] ChromosomeWeights =
    {
        (ChromosomeKind.Stabilizer, 1f),
        (ChromosomeKind.Synchronizer, 5f),
        (ChromosomeKind.Power, 5f),
        (ChromosomeKind.Energy, 5f),
    };

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DnaConsoleComponent, BoundUIOpenedEvent>((uid, comp, _) => UpdateUi((uid, comp)));
        SubscribeLocalEvent<DnaConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DnaScannerComponent, ContainerIsRemovingAttemptEvent>(OnScannerRemoveAttempt);

        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleScrambleMessage>(OnScramble);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleToggleLockMessage>(OnToggleLock);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleToggleDoorMessage>(OnToggleDoor);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleCancelDelayMessage>(OnCancelDelay);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleEjectDiskMessage>(OnEjectDisk);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsolePulseGeneMessage>(OnPulseGene);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleApplyChromoMessage>(OnApplyChromo);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleEjectChromoMessage>(OnEjectChromo);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsolePrintInjectorMessage>(OnPrintInjector);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleSaveConsoleMessage>(OnSaveConsole);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleSaveDiskMessage>(OnSaveDisk);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleDeleteMutationMessage>(OnDeleteMutation);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleNullifyMessage>(OnNullify);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleCombineMessage>(OnCombine);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleAddAdvInjMessage>(OnAddAdvInj);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleNewAdvInjMessage>(OnNewAdvInj);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleDeleteAdvInjMessage>(OnDeleteAdvInj);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsolePrintAdvInjMessage>(OnPrintAdvInj);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleSetPulseStrengthMessage>(OnSetPulseStrength);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleSetPulseDurationMessage>(OnSetPulseDuration);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleMakeupPulseMessage>(OnMakeupPulse);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleSaveMakeupMessage>(OnSaveMakeup);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleDeleteMakeupMessage>(OnDeleteMakeup);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleApplyMakeupMessage>(OnApplyMakeup);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleMakeupInjectorMessage>(OnMakeupInjector);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleSaveMakeupDiskMessage>(OnSaveMakeupDisk);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleLoadMakeupDiskMessage>(OnLoadMakeupDisk);
        SubscribeLocalEvent<DnaConsoleComponent, DnaConsoleDeleteDiskMakeupMessage>(OnDeleteDiskMakeup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var refresh = now >= _nextRefresh;
        if (refresh)
            _nextRefresh = now + RefreshInterval;

        var query = EntityQueryEnumerator<DnaConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.PulseIndex > 0 && now >= console.PulseAt)
                FinishPulse((uid, console));

            if (console.DelayedAction is { } delayed && IsViable(GetOccupant((uid, console))))
            {
                console.DelayedAction = null;
                ApplyMakeup((uid, console), delayed.Index, delayed.Type);
            }

            if (refresh && _ui.IsUiOpen(uid, DnaConsoleUiKey.Key))
                UpdateUi((uid, console));
        }
    }

    #region Сканер и пациент

    private EntityUid? FindScanner(Entity<DnaConsoleComponent> console)
    {
        foreach (var scanner in _lookup.GetEntitiesInRange<DnaScannerComponent>(Transform(console).Coordinates, console.Comp.ScannerRange))
        {
            return scanner.Owner;
        }

        return null;
    }

    private EntityUid? GetOccupant(Entity<DnaConsoleComponent> console)
    {
        return FindScanner(console) is { } scanner && TryComp<MedicalScannerComponent>(scanner, out var medical)
            ? medical.BodyContainer.ContainedEntity
            : null;
    }

    private bool IsViable(EntityUid? occupant)
    {
        return occupant is { } uid && _genetics.TryGetGenome(uid, out _);
    }

    /// <summary>Менять ДНК можно только живому (can_modify_occupant).</summary>
    private bool TryGetModifiable(Entity<DnaConsoleComponent> console, out Entity<GenomeComponent> genome)
    {
        genome = default;
        return GetOccupant(console) is { } occupant
            && !_mobState.IsDead(occupant)
            && _genetics.TryGetGenome(occupant, out genome);
    }

    private void OnScannerRemoveAttempt(Entity<DnaScannerComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.Locked)
            args.Cancel();
    }

    private void OnToggleLock(Entity<DnaConsoleComponent> console, ref DnaConsoleToggleLockMessage args)
    {
        if (FindScanner(console) is { } scanner && TryComp<DnaScannerComponent>(scanner, out var comp))
            comp.Locked = !comp.Locked;

        UpdateUi(console);
    }

    /// <summary>Открыть сканер — пациент выходит (toggle_door).</summary>
    private void OnToggleDoor(Entity<DnaConsoleComponent> console, ref DnaConsoleToggleDoorMessage args)
    {
        if (FindScanner(console) is { } scanner
            && TryComp<DnaScannerComponent>(scanner, out var comp)
            && !comp.Locked)
        {
            _scanner.EjectBody(scanner, null);
        }

        UpdateUi(console);
    }

    private void OnScramble(Entity<DnaConsoleComponent> console, ref DnaConsoleScrambleMessage args)
    {
        if (_timing.CurTime < console.Comp.NextScramble || !TryGetModifiable(console, out var genome))
            return;

        _genetics.Scramble(genome);
        _genetics.AddGeneticDamage(genome, StrengthMultiplier * 50);
        console.Comp.NextScramble = _timing.CurTime + console.Comp.ScrambleCooldown;
        _popup.PopupEntity(Loc.GetString("dna-console-scrambled"), console);
        UpdateUi(console);
    }

    private void OnCancelDelay(Entity<DnaConsoleComponent> console, ref DnaConsoleCancelDelayMessage args)
    {
        console.Comp.DelayedAction = null;
        UpdateUi(console);
    }

    #endregion

    #region Дискета

    private Entity<GeneticDataDiskComponent>? GetDisk(Entity<DnaConsoleComponent> console)
    {
        return _itemSlots.GetItemOrNull(console, console.Comp.DiskSlot) is { } disk
            && TryComp<GeneticDataDiskComponent>(disk, out var comp)
            ? (disk, comp)
            : null;
    }

    private void OnEjectDisk(Entity<DnaConsoleComponent> console, ref DnaConsoleEjectDiskMessage args)
    {
        if (_itemSlots.TryGetSlot(console, console.Comp.DiskSlot, out var slot))
            _itemSlots.TryEjectToHands(console, slot, args.Actor);
        UpdateUi(console);
    }

    #endregion

    #region Состояние

    public void UpdateUi(Entity<DnaConsoleComponent> console)
    {
        var now = _timing.CurTime;
        var comp = console.Comp;
        var scanner = FindScanner(console);
        var occupant = GetOccupant(console);
        var state = new DnaConsoleBoundUserInterfaceState
        {
            IsScannerConnected = scanner != null,
            ScannerLocked = scanner != null && CompOrNull<DnaScannerComponent>(scanner.Value)?.Locked == true,
            ScannerOpen = occupant == null,
            HasDelayedAction = comp.DelayedAction != null,
            IsScrambleReady = now >= comp.NextScramble,
            ScrambleSeconds = Seconds(comp.NextScramble - now),
            IsJokerReady = now >= comp.NextJoker,
            JokerSeconds = Seconds(comp.NextJoker - now),
            IsInjectorReady = now >= comp.NextInjector,
            InjectorSeconds = Seconds(comp.NextInjector - now),
            IsPulsing = comp.PulseIndex > 0,
            TimeToPulse = Seconds(comp.PulseAt - now),
            GeneticMakeupCooldown = Seconds(comp.NextMakeup - now),
            CrisprCharges = comp.CrisprCharges,
            Chromosomes = new(comp.Chromosomes),
            MaxAdvInjectors = comp.MaxAdvInjectors,
            PulseStrength = comp.PulseStrength,
            PulseDuration = comp.PulseDuration,
            StdDevStr = comp.PulseStrength * StrengthMultiplier,
            StdDevAcc = (AccuracyMultiplier / (float) comp.PulseDuration) switch
            {
                <= 0.25f => ">95 %",
                <= 0.5f => "68-95 %",
                <= 0.75f => "55-68 %",
                _ => "<38 %",
            },
        };

        if (occupant is { } subject && _genetics.TryGetGenome(subject, out var genome))
        {
            state.IsViableSubject = true;
            state.SubjectName = Name(subject);
            state.SubjectStatus = GetStatus(subject);
            state.SubjectHealth = GetHealth(subject);
            state.SubjectDamage = MathF.Round(genome.Comp.GeneticDamage / GeneticsSystem.GeneticDamageToxThreshold * 100f, 1);
            state.IsMonkey = HasComp<GeneticMonkeyComponent>(subject);

            var makeup = _makeup.Capture(subject);
            state.SubjectUniqueIdentity = _makeup.GetUniqueIdentity(makeup);
            state.SubjectUniqueFeatures = _makeup.GetUniqueFeatures(makeup);
            state.Occupant = BuildOccupant(genome);
        }

        for (var i = 0; i < comp.Storage.Count; i++)
        {
            state.Console.Add(BuildStored(comp.Storage[i], new DnaMutationRef(DnaMutationSource.Console, i)));
        }

        for (var j = 0; j < comp.AdvancedInjectors.Count; j++)
        {
            var injector = comp.AdvancedInjectors[j];
            var entry = new DnaAdvancedInjectorState { Name = injector.Name };
            for (var i = 0; i < injector.Mutations.Count; i++)
            {
                entry.Mutations.Add(BuildStored(injector.Mutations[i], new DnaMutationRef(DnaMutationSource.Injector, i, j)));
            }

            state.Injectors.Add(entry);
        }

        if (GetDisk(console) is { } disk)
        {
            state.HasDisk = true;
            state.DiskReadOnly = disk.Comp.ReadOnly;
            state.DiskCapacity = disk.Comp.MaxMutations - disk.Comp.Mutations.Count;
            state.DiskMakeup = disk.Comp.Makeup is { } diskMakeup ? BuildMakeup(diskMakeup) : null;
            for (var i = 0; i < disk.Comp.Mutations.Count; i++)
            {
                state.Disk.Add(BuildStored(disk.Comp.Mutations[i], new DnaMutationRef(DnaMutationSource.Disk, i)));
            }
        }

        for (var i = 0; i < comp.Makeups.Length; i++)
        {
            state.MakeupStorage[i] = comp.Makeups[i] is { } buffer ? BuildMakeup(buffer) : null;
        }

        _ui.SetUiState(console.Owner, DnaConsoleUiKey.Key, state);
    }

    private DnaMakeupState BuildMakeup(GeneticMakeupData data)
    {
        return new DnaMakeupState
        {
            Name = data.Name,
            BloodType = data.BloodType,
            UniqueEnzymes = GeneticMakeupSystem.GetUniqueEnzymes(data),
            UniqueIdentity = _makeup.GetUniqueIdentity(data),
            UniqueFeatures = _makeup.GetUniqueFeatures(data),
        };
    }

    private DnaSubjectStatus GetStatus(EntityUid uid)
    {
        if (TryComp<MobStateComponent>(uid, out var mob))
        {
            if (mob.CurrentState == MobState.Dead)
                return DnaSubjectStatus.Dead;
            if (mob.CurrentState == MobState.Critical)
                return DnaSubjectStatus.SoftCrit;
        }

        return HasComp<SleepingComponent>(uid) ? DnaSubjectStatus.Unconscious : DnaSubjectStatus.Conscious;
    }

    /// <summary>Здоровье в процентах до критического состояния, как mob.health в SS13.</summary>
    private float GetHealth(EntityUid uid)
    {
        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit)
            || !HasComp<Content.Shared.Damage.Components.DamageableComponent>(uid))
        {
            return 100f;
        }

        var total = _damageable.GetTotalDamage(uid);
        return MathF.Round((crit.Value - total).Float() / crit.Value.Float() * 100f);
    }

    private List<DnaMutationState> BuildOccupant(Entity<GenomeComponent> genome)
    {
        var list = new List<DnaMutationState>();
        for (var i = 0; i < genome.Comp.Blocks.Count; i++)
        {
            var gene = genome.Comp.Blocks[i];
            var active = genome.Comp.Active.TryGetValue(gene.Mutation, out var activeMutation);
            var state = BuildMutation(gene.Mutation, new DnaMutationRef(DnaMutationSource.Occupant, i),
                active ? activeMutation!.Chromosome : ChromosomeKind.None, active);
            state.Sequence = gene.Sequence;
            state.DefaultSequence = gene.Default;
            state.Class = DnaMutationClass.Normal;
            if (active)
                state.CanChromo = activeMutation!.Chromosome != ChromosomeKind.None ? DnaChromosomeState.Used : state.CanChromo;
            list.Add(state);
        }

        // Мутации поверх генома (MUT_EXTRA): от мутатора или других источников.
        var index = genome.Comp.Blocks.Count;
        foreach (var (id, active) in genome.Comp.Active)
        {
            if (genome.Comp.Blocks.Any(b => b.Mutation == id))
                continue;

            var state = BuildMutation(id, new DnaMutationRef(DnaMutationSource.Occupant, index++), active.Chromosome, true);
            state.Class = (active.Sources & MutationSource.Mutator) != 0 ? DnaMutationClass.Extra : DnaMutationClass.Other;
            state.Sequence = _genetics.GetFullSequence(id);
            state.DefaultSequence = state.Sequence;
            if (active.Chromosome != ChromosomeKind.None)
                state.CanChromo = DnaChromosomeState.Used;
            list.Add(state);
        }

        return list;
    }

    private DnaMutationState BuildStored(StoredMutation stored, DnaMutationRef reference)
    {
        var state = BuildMutation(stored.Mutation, reference, stored.Chromosome, true);
        state.Discovered = true;
        if (stored.Chromosome != ChromosomeKind.None)
            state.CanChromo = DnaChromosomeState.Used;
        return state;
    }

    private DnaMutationState BuildMutation(ProtoId<GeneticMutationPrototype> id, DnaMutationRef reference, ChromosomeKind chromosome, bool active)
    {
        var proto = _proto.Index(id);
        return new DnaMutationState
        {
            Ref = reference,
            Mutation = id,
            Alias = Loc.GetString("genetics-mutation-alias", ("number", _genetics.GetAlias(id))),
            Name = Loc.GetString(proto.Name),
            Description = proto.Description is { } desc ? Loc.GetString(desc) : string.Empty,
            Quality = proto.Quality,
            Instability = proto.Instability,
            Discovered = _genetics.IsDiscovered(id),
            Active = active,
            AppliedChromo = chromosome,
            ValidChromos = proto.Chromosomes,
            CanChromo = proto.Chromosomes == ChromosomeKind.None ? DnaChromosomeState.Never : DnaChromosomeState.None,
        };
    }

    private static int Seconds(TimeSpan span)
    {
        return Math.Max(0, (int) Math.Ceiling(span.TotalSeconds));
    }

    #endregion

    #region Поиск мутаций по ссылке

    private StoredMutation? GetStored(Entity<DnaConsoleComponent> console, DnaMutationRef reference)
    {
        switch (reference.Source)
        {
            case DnaMutationSource.Occupant:
                if (!TryGetModifiable(console, out var genome))
                    return null;

                var mutations = OccupantMutationIds(genome);
                if (reference.Index < 0 || reference.Index >= mutations.Count)
                    return null;

                var id = mutations[reference.Index];
                return genome.Comp.Active.TryGetValue(id, out var active)
                    ? new StoredMutation { Mutation = id, Chromosome = active.Chromosome }
                    : null;
            case DnaMutationSource.Console:
                return reference.Index >= 0 && reference.Index < console.Comp.Storage.Count ? console.Comp.Storage[reference.Index] : null;
            case DnaMutationSource.Disk:
                return GetDisk(console) is { } disk && reference.Index >= 0 && reference.Index < disk.Comp.Mutations.Count
                    ? disk.Comp.Mutations[reference.Index]
                    : null;
            case DnaMutationSource.Injector:
                return reference.Injector >= 0 && reference.Injector < console.Comp.AdvancedInjectors.Count
                    && reference.Index >= 0 && reference.Index < console.Comp.AdvancedInjectors[reference.Injector].Mutations.Count
                    ? console.Comp.AdvancedInjectors[reference.Injector].Mutations[reference.Index]
                    : null;
        }

        return null;
    }

    /// <summary>Порядок мутаций пациента в окне: блоки генома, затем мутации поверх него.</summary>
    private static List<ProtoId<GeneticMutationPrototype>> OccupantMutationIds(Entity<GenomeComponent> genome)
    {
        var ids = genome.Comp.Blocks.Select(b => b.Mutation).ToList();
        ids.AddRange(genome.Comp.Active.Keys.Where(k => !ids.Contains(k)));
        return ids;
    }

    private static StoredMutation Copy(StoredMutation stored)
    {
        return new StoredMutation { Mutation = stored.Mutation, Chromosome = stored.Chromosome };
    }

    #endregion

    #region Секвенсор

    /// <summary>pulse_gene: следующее/предыдущее основание или X; «Джокер» открывает верное.</summary>
    private void OnPulseGene(Entity<DnaConsoleComponent> console, ref DnaConsolePulseGeneMessage args)
    {
        if (!TryGetModifiable(console, out var genome)
            || HasComp<GeneticMonkeyComponent>(genome) && args.Mutation != GeneticsSystem.RaceMutation)
        {
            return;
        }

        var mutation = args.Mutation;
        var gene = genome.Comp.Blocks.FirstOrDefault(b => b.Mutation == mutation);
        if (gene == null || args.Position < 0 || args.Position >= gene.Sequence.Length)
            return;

        var current = gene.Sequence[args.Position];
        char next;
        switch (args.Action)
        {
            case DnaGeneAction.Clear:
                next = GeneticsSystem.Unknown;
                var defaults = gene.Default.ToCharArray();
                if (args.Position < defaults.Length)
                {
                    defaults[args.Position] = GeneticsSystem.Unknown;
                    gene.Default = new string(defaults);
                }

                break;
            case DnaGeneAction.Next when args.Joker && _timing.CurTime >= console.Comp.NextJoker:
                next = _genetics.GetFullSequence(gene.Mutation)[args.Position];
                console.Comp.NextJoker = _timing.CurTime + console.Comp.JokerCooldown;
                break;
            case DnaGeneAction.Next:
                var nextIndex = Array.IndexOf(GeneLetters, current);
                next = GeneLetters[nextIndex < 0 || nextIndex == GeneLetters.Length - 1 ? 0 : nextIndex + 1];
                break;
            default:
                var prevIndex = Array.IndexOf(GeneLetters, current);
                next = GeneLetters[prevIndex <= 0 ? GeneLetters.Length - 1 : prevIndex - 1];
                break;
        }

        var chars = gene.Sequence.ToCharArray();
        chars[args.Position] = next;
        gene.Sequence = new string(chars);
        _genetics.AddGeneticDamage(genome, StrengthMultiplier);
        _genetics.UpdateBlockActivation(genome, gene);
        UpdateUi(console);
    }

    private void OnNullify(Entity<DnaConsoleComponent> console, ref DnaConsoleNullifyMessage args)
    {
        var mutation = args.Mutation;
        if (!TryGetModifiable(console, out var genome) || genome.Comp.Blocks.Any(b => b.Mutation == mutation))
            return;

        _genetics.Deactivate(genome, args.Mutation);
        UpdateUi(console);
    }

    #endregion

    #region Хромосомы

    private void OnApplyChromo(Entity<DnaConsoleComponent> console, ref DnaConsoleApplyChromoMessage args)
    {
        if (args.Mutation.Source != DnaMutationSource.Occupant
            || !console.Comp.Chromosomes.TryGetValue(args.Kind, out var count) || count <= 0
            || !TryGetModifiable(console, out var genome)
            || GetStored(console, args.Mutation) is not { } stored
            || !_genetics.ApplyChromosome(genome, stored.Mutation, args.Kind))
        {
            Deny(console);
            return;
        }

        console.Comp.Chromosomes[args.Kind] = count - 1;
        UpdateUi(console);
    }

    private void OnEjectChromo(Entity<DnaConsoleComponent> console, ref DnaConsoleEjectChromoMessage args)
    {
        if (!console.Comp.Chromosomes.TryGetValue(args.Kind, out var count) || count <= 0
            || !ChromosomeProtos.TryGetValue(args.Kind, out var proto))
        {
            return;
        }

        console.Comp.Chromosomes[args.Kind] = count - 1;
        Spawn(proto, Transform(console).Coordinates);
        UpdateUi(console);
    }

    private ChromosomeKind PickChromosome()
    {
        var roll = _random.NextFloat() * ChromosomeWeights.Sum(c => c.Weight);
        foreach (var (kind, weight) in ChromosomeWeights)
        {
            roll -= weight;
            if (roll <= 0)
                return kind;
        }

        return ChromosomeKind.Synchronizer;
    }

    /// <summary>Хромосомы и использованные инжекторы принимаются консолью.</summary>
    private void OnInteractUsing(Entity<DnaConsoleComponent> console, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ChromosomeComponent>(args.Used, out var chromosome))
        {
            args.Handled = true;
            console.Comp.Chromosomes[chromosome.Kind] = console.Comp.Chromosomes.GetValueOrDefault(chromosome.Kind) + 1;
            _popup.PopupEntity(Loc.GetString("dna-console-chromosome-added", ("chromosome", args.Used)), console, args.User);
            QueueDel(args.Used);
            UpdateUi(console);
            return;
        }

        if (!TryComp<DnaInjectorComponent>(args.Used, out var injector))
            return;

        args.Handled = true;

        // Использованный активатор-исследование переплавляется в хромосому, остальные — просто перерабатываются.
        if (injector.Mode == DnaInjectorMode.Activator && injector.Used && injector.Research && _random.Prob(ChromosomeChance))
        {
            var kind = PickChromosome();
            console.Comp.Chromosomes[kind] = console.Comp.Chromosomes.GetValueOrDefault(kind) + 1;
            _popup.PopupEntity(Loc.GetString("dna-console-chromosome-extracted",
                ("chromosome", Loc.GetString($"dna-chromosome-{kind.ToString().ToLowerInvariant()}"))), console, args.User);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString(injector.Used && injector.Research
                ? "dna-console-chromosome-none"
                : "dna-console-injector-recycled"), console, args.User);
        }

        QueueDel(args.Used);
        UpdateUi(console);
    }

    #endregion

    #region Хранилища и комбинирование

    private void OnSaveConsole(Entity<DnaConsoleComponent> console, ref DnaConsoleSaveConsoleMessage args)
    {
        if (args.Mutation.Source == DnaMutationSource.Console
            || GetStored(console, args.Mutation) is not { } stored
            || !_genetics.IsDiscovered(stored.Mutation)
            || console.Comp.Storage.Any(s => s.Mutation == stored.Mutation && s.Chromosome == stored.Chromosome))
        {
            Deny(console);
            return;
        }

        console.Comp.Storage.Add(Copy(stored));
        UpdateUi(console);
    }

    private void OnSaveDisk(Entity<DnaConsoleComponent> console, ref DnaConsoleSaveDiskMessage args)
    {
        if (args.Mutation.Source == DnaMutationSource.Disk
            || GetDisk(console) is not { } disk
            || disk.Comp.ReadOnly
            || disk.Comp.Mutations.Count >= disk.Comp.MaxMutations
            || GetStored(console, args.Mutation) is not { } stored
            || !_genetics.IsDiscovered(stored.Mutation)
            || disk.Comp.Mutations.Any(s => s.Mutation == stored.Mutation && s.Chromosome == stored.Chromosome))
        {
            Deny(console);
            return;
        }

        disk.Comp.Mutations.Add(Copy(stored));
        UpdateUi(console);
    }

    private void OnDeleteMutation(Entity<DnaConsoleComponent> console, ref DnaConsoleDeleteMutationMessage args)
    {
        var reference = args.Mutation;
        switch (reference.Source)
        {
            case DnaMutationSource.Console when reference.Index >= 0 && reference.Index < console.Comp.Storage.Count:
                console.Comp.Storage.RemoveAt(reference.Index);
                break;
            case DnaMutationSource.Disk when GetDisk(console) is { } disk && !disk.Comp.ReadOnly
                && reference.Index >= 0 && reference.Index < disk.Comp.Mutations.Count:
                disk.Comp.Mutations.RemoveAt(reference.Index);
                break;
            case DnaMutationSource.Injector when reference.Injector >= 0 && reference.Injector < console.Comp.AdvancedInjectors.Count:
                var mutations = console.Comp.AdvancedInjectors[reference.Injector].Mutations;
                if (reference.Index >= 0 && reference.Index < mutations.Count)
                    mutations.RemoveAt(reference.Index);
                break;
        }

        UpdateUi(console);
    }

    /// <summary>Комбинирование по рецепту (generecipe): результат идёт туда же, откуда вторая мутация.</summary>
    private void OnCombine(Entity<DnaConsoleComponent> console, ref DnaConsoleCombineMessage args)
    {
        if (GetStored(console, args.First) is not { } first || GetStored(console, args.Second) is not { } second)
            return;

        var recipe = _proto.EnumeratePrototypes<GeneticRecipePrototype>()
            .FirstOrDefault(r => r.First == first.Mutation && r.Second == second.Mutation
                || r.First == second.Mutation && r.Second == first.Mutation);

        if (recipe == null)
        {
            _popup.PopupEntity(Loc.GetString("dna-console-combine-failed"), console, PopupType.SmallCaution);
            Deny(console);
            return;
        }

        var result = new StoredMutation { Mutation = recipe.Result };
        if (args.Second.Source == DnaMutationSource.Disk && GetDisk(console) is { } disk)
        {
            if (disk.Comp.ReadOnly || disk.Comp.Mutations.Count >= disk.Comp.MaxMutations)
            {
                Deny(console);
                return;
            }

            disk.Comp.Mutations.Add(result);
        }
        else
        {
            console.Comp.Storage.Add(result);
        }

        _genetics.Discover(recipe.Result);
        _popup.PopupEntity(Loc.GetString("dna-console-combine-success",
            ("mutation", Loc.GetString(_proto.Index(recipe.Result).Name))), console);
        UpdateUi(console);
    }

    #endregion

    #region Инжекторы

    private bool CheckInjectorReady(Entity<DnaConsoleComponent> console)
    {
        if (_timing.CurTime >= console.Comp.NextInjector)
            return true;

        Deny(console);
        return false;
    }

    /// <summary>
    /// Активатор включает этот спящий ген у носителя, мутатор добавляет мутацию сверху (print_injector).
    /// Откат растёт с нестабильностью, как в SS13.
    /// </summary>
    private void OnPrintInjector(Entity<DnaConsoleComponent> console, ref DnaConsolePrintInjectorMessage args)
    {
        if (!CheckInjectorReady(console) || GetStored(console, args.Mutation) is not { } stored)
            return;

        var instability = Math.Abs(_proto.Index(stored.Mutation).Instability);
        var cooldown = args.Activator
            ? Math.Max(5f, instability * 0.25f)
            : Math.Max(10f, instability * 0.15f * 10f);

        Print(console, args.Activator ? DnaInjectorMode.Activator : DnaInjectorMode.Mutator, new List<StoredMutation> { Copy(stored) });
        console.Comp.NextInjector = _timing.CurTime + TimeSpan.FromSeconds(cooldown);
        UpdateUi(console);
    }

    private AdvancedInjector? FindAdvInj(Entity<DnaConsoleComponent> console, string name)
    {
        return console.Comp.AdvancedInjectors.FirstOrDefault(i => i.Name == name);
    }

    private void OnNewAdvInj(Entity<DnaConsoleComponent> console, ref DnaConsoleNewAdvInjMessage args)
    {
        var name = args.Name.Trim();
        if (name.Length == 0 || name.Length > 32
            || console.Comp.AdvancedInjectors.Count >= console.Comp.MaxAdvInjectors
            || FindAdvInj(console, name) != null)
        {
            Deny(console);
            return;
        }

        console.Comp.AdvancedInjectors.Add(new AdvancedInjector { Name = name });
        UpdateUi(console);
    }

    private void OnDeleteAdvInj(Entity<DnaConsoleComponent> console, ref DnaConsoleDeleteAdvInjMessage args)
    {
        if (FindAdvInj(console, args.Name) is { } injector)
            console.Comp.AdvancedInjectors.Remove(injector);

        UpdateUi(console);
    }

    /// <summary>Добавить мутацию в продвинутый инжектор: до 10 мутаций и до 50 нестабильности.</summary>
    private void OnAddAdvInj(Entity<DnaConsoleComponent> console, ref DnaConsoleAddAdvInjMessage args)
    {
        if (FindAdvInj(console, args.Injector) is not { } injector
            || GetStored(console, args.Mutation) is not { } stored
            || injector.Mutations.Count >= console.Comp.MaxInjectorMutations)
        {
            Deny(console);
            return;
        }

        var total = injector.Mutations.Sum(m => _proto.Index(m.Mutation).Instability) + _proto.Index(stored.Mutation).Instability;
        if (total > console.Comp.MaxInjectorInstability)
        {
            _popup.PopupEntity(Loc.GetString("dna-console-advinj-unstable"), console, PopupType.SmallCaution);
            Deny(console);
            return;
        }

        injector.Mutations.Add(Copy(stored));
        UpdateUi(console);
    }

    private void OnPrintAdvInj(Entity<DnaConsoleComponent> console, ref DnaConsolePrintAdvInjMessage args)
    {
        if (FindAdvInj(console, args.Name) is not { } injector || injector.Mutations.Count == 0 || !CheckInjectorReady(console))
            return;

        var instability = injector.Mutations.Sum(m => Math.Abs(_proto.Index(m.Mutation).Instability));
        var cooldown = Math.Min(90f, Math.Max(15f, instability * 0.1f * 10f));
        var printed = Print(console, DnaInjectorMode.Mutator, injector.Mutations.Select(Copy).ToList());
        _metaData.SetEntityName(printed, Loc.GetString("dna-injector-advanced-name", ("name", injector.Name)));
        console.Comp.NextInjector = _timing.CurTime + TimeSpan.FromSeconds(cooldown);
        UpdateUi(console);
    }

    private EntityUid Print(Entity<DnaConsoleComponent> console, DnaInjectorMode mode, List<StoredMutation> mutations)
    {
        var injector = Spawn(console.Comp.InjectorProto, Transform(console).Coordinates);
        var comp = EnsureComp<DnaInjectorComponent>(injector);
        comp.Mode = mode;
        comp.Mutations = mutations;
        Dirty(injector, comp);

        var names = string.Join(", ", mutations.Select(m => Loc.GetString(_proto.Index(m.Mutation).Name)));
        _metaData.SetEntityName(injector, Loc.GetString(mode == DnaInjectorMode.Activator
            ? "dna-injector-activator-name"
            : "dna-injector-mutator-name", ("mutations", names)));

        _audio.PlayPvs(PrintSound, console);
        return injector;
    }

    #endregion

    #region Ферменты и генетический облик

    private void OnSetPulseStrength(Entity<DnaConsoleComponent> console, ref DnaConsoleSetPulseStrengthMessage args)
    {
        console.Comp.PulseStrength = Math.Clamp(args.Value, 1, PulseStrengthMax);
        UpdateUi(console);
    }

    private void OnSetPulseDuration(Entity<DnaConsoleComponent> console, ref DnaConsoleSetPulseDurationMessage args)
    {
        console.Comp.PulseDuration = Math.Clamp(args.Value, 1, PulseDurationMax);
        UpdateUi(console);
    }

    /// <summary>makeup_pulse: импульс по символу фермента длится PulseDuration секунд.</summary>
    private void OnMakeupPulse(Entity<DnaConsoleComponent> console, ref DnaConsoleMakeupPulseMessage args)
    {
        if (console.Comp.PulseIndex > 0 || !TryGetModifiable(console, out _))
            return;

        console.Comp.PulseFeatures = args.Features;
        console.Comp.PulseIndex = args.Index + 1;
        console.Comp.PulseAt = _timing.CurTime + TimeSpan.FromSeconds(console.Comp.PulseDuration);
        UpdateUi(console);
    }

    /// <summary>
    /// genetic_damage_pulse: точность попадания по символу — гаусс с σ = 3/длительность,
    /// сдвиг значения — гаусс с σ = сила.
    /// </summary>
    private void FinishPulse(Entity<DnaConsoleComponent> console)
    {
        var index = console.Comp.PulseIndex - 1;
        var features = console.Comp.PulseFeatures;
        console.Comp.PulseIndex = 0;

        if (!TryGetModifiable(console, out var genome))
            return;

        var makeup = _makeup.Capture(genome);
        var block = features ? _makeup.GetUniqueFeatures(makeup) : _makeup.GetUniqueIdentity(makeup);
        if (block.Length == 0)
            return;

        var target = (int) Math.Round(Gaussian(AccuracyMultiplier / (double) console.Comp.PulseDuration) + index);
        target = ((target % block.Length) + block.Length) % block.Length;

        var shift = Gaussian(console.Comp.PulseStrength * StrengthMultiplier);
        var delta = shift == 0 ? _random.Pick(new[] { -1, 1 }) : shift < 0 ? (int) Math.Floor(shift) : (int) Math.Ceiling(shift);
        var value = int.Parse(block[target].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        value = ((value + delta) % 16 + 16) % 16;
        block = block[..target] + value.ToString("X") + block[(target + 1)..];

        if (features)
            _makeup.ApplyUniqueFeatures(genome, block);
        else
            _makeup.ApplyUniqueIdentity(genome, block);

        UpdateUi(console);
    }

    private double Gaussian(double sigma)
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = _random.NextDouble();
        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private void OnSaveMakeup(Entity<DnaConsoleComponent> console, ref DnaConsoleSaveMakeupMessage args)
    {
        if (args.Index < 0 || args.Index >= console.Comp.Makeups.Length || GetOccupant(console) is not { } occupant || !IsViable(occupant))
            return;

        console.Comp.Makeups[args.Index] = _makeup.Capture(occupant);
        UpdateUi(console);
    }

    private void OnDeleteMakeup(Entity<DnaConsoleComponent> console, ref DnaConsoleDeleteMakeupMessage args)
    {
        if (args.Index >= 0 && args.Index < console.Comp.Makeups.Length)
            console.Comp.Makeups[args.Index] = null;

        UpdateUi(console);
    }

    /// <summary>Перенос облика на пациента; если пациента нет — отложенно (makeup_delay).</summary>
    private void OnApplyMakeup(Entity<DnaConsoleComponent> console, ref DnaConsoleApplyMakeupMessage args)
    {
        if (args.Index < 0 || args.Index >= console.Comp.Makeups.Length || console.Comp.Makeups[args.Index] == null)
            return;

        if (!IsViable(GetOccupant(console)))
        {
            console.Comp.DelayedAction = (args.Index, args.Type);
            UpdateUi(console);
            return;
        }

        ApplyMakeup(console, args.Index, args.Type);
    }

    private void ApplyMakeup(Entity<DnaConsoleComponent> console, int index, DnaMakeupType type)
    {
        if (_timing.CurTime < console.Comp.NextMakeup
            || console.Comp.Makeups[index] is not { } makeup
            || !TryGetModifiable(console, out var genome))
        {
            Deny(console);
            return;
        }

        _makeup.Apply(genome, makeup, type);
        _genetics.AddGeneticDamage(genome, _random.Next(100, 251));
        console.Comp.NextMakeup = _timing.CurTime + console.Comp.MakeupCooldown;
        UpdateUi(console);
    }

    private void OnMakeupInjector(Entity<DnaConsoleComponent> console, ref DnaConsoleMakeupInjectorMessage args)
    {
        if (args.Index < 0 || args.Index >= console.Comp.Makeups.Length
            || console.Comp.Makeups[args.Index] is not { } makeup
            || !CheckInjectorReady(console))
        {
            return;
        }

        var injector = Spawn(console.Comp.InjectorProto, Transform(console).Coordinates);
        var comp = EnsureComp<DnaInjectorComponent>(injector);
        comp.Mode = DnaInjectorMode.Makeup;
        comp.Makeup = makeup.Clone();
        comp.MakeupType = args.Type;
        Dirty(injector, comp);
        _metaData.SetEntityName(injector, Loc.GetString($"dna-injector-makeup-{args.Type.ToString().ToLowerInvariant()}",
            ("name", makeup.Name)));
        _audio.PlayPvs(PrintSound, console);

        console.Comp.NextInjector = _timing.CurTime + TimeSpan.FromSeconds(10);
        UpdateUi(console);
    }

    private void OnSaveMakeupDisk(Entity<DnaConsoleComponent> console, ref DnaConsoleSaveMakeupDiskMessage args)
    {
        if (args.Index < 0 || args.Index >= console.Comp.Makeups.Length
            || console.Comp.Makeups[args.Index] is not { } makeup
            || GetDisk(console) is not { } disk || disk.Comp.ReadOnly)
        {
            return;
        }

        disk.Comp.Makeup = makeup.Clone();
        UpdateUi(console);
    }

    private void OnLoadMakeupDisk(Entity<DnaConsoleComponent> console, ref DnaConsoleLoadMakeupDiskMessage args)
    {
        if (args.Index < 0 || args.Index >= console.Comp.Makeups.Length || GetDisk(console)?.Comp.Makeup is not { } makeup)
            return;

        console.Comp.Makeups[args.Index] = makeup.Clone();
        UpdateUi(console);
    }

    private void OnDeleteDiskMakeup(Entity<DnaConsoleComponent> console, ref DnaConsoleDeleteDiskMakeupMessage args)
    {
        if (GetDisk(console) is { } disk && !disk.Comp.ReadOnly)
            disk.Comp.Makeup = null;

        UpdateUi(console);
    }

    #endregion

    private void Deny(EntityUid console)
    {
        _audio.PlayPvs(DenySound, console);
    }
}
