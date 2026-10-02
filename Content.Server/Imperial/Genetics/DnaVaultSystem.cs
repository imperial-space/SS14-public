using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// ДНК-хранилище (dna_vault из SS13): цель станции — собрать образцы животных, растений и разумных гуманоидов.
/// После заполнения каждый член экипажа может один раз получить одно из двух случайных улучшений.
/// </summary>
[RegisterComponent]
public sealed partial class DnaVaultComponent : Component
{
    [ViewVariables]
    public int AnimalsMax = 100;

    [ViewVariables]
    public int PlantsMax = 100;

    [ViewVariables]
    public int DnaMax = 100;

    [ViewVariables]
    public HashSet<string> Animals = new();

    [ViewVariables]
    public HashSet<string> Plants = new();

    [ViewVariables]
    public HashSet<string> Dna = new();

    [ViewVariables]
    public bool Completed;

    /// <summary>Пары улучшений, выпавшие каждому, кто открыл хранилище.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, List<ProtoId<GeneticMutationPrototype>>> PowerLottery = new();

    /// <summary>Улучшения, из которых выбирается пара (vault_mutation.dm).</summary>
    [DataField]
    public List<ProtoId<GeneticMutationPrototype>> Powers = new()
    {
        "MutationBreathless",
        "MutationDextrous",
        "MutationQuick",
        "MutationFireImmunity",
        "MutationPlasmocile",
        "MutationQuickRecovery",
        "MutationTough",
    };

    [DataField]
    public SoundSpecifier UploadSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
}

/// <summary>ДНК-сэмплер: собирает образцы и выгружает их в привязанное хранилище.</summary>
[RegisterComponent]
public sealed partial class DnaProbeComponent : Component
{
    [ViewVariables]
    public EntityUid? Vault;

    [ViewVariables]
    public HashSet<string> Animals = new();

    [ViewVariables]
    public HashSet<string> Plants = new();

    [ViewVariables]
    public HashSet<string> Dna = new();

    [DataField]
    public SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
}

/// <summary>Метка: существо уже получило улучшение ДНК-хранилища (TRAIT_USED_DNA_VAULT).</summary>
[RegisterComponent]
public sealed partial class DnaVaultUsedComponent : Component;

public sealed class DnaVaultSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string BiologicalContainer = "Biological";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DnaVaultComponent, MapInitEvent>(OnVaultMapInit);
        SubscribeLocalEvent<DnaVaultComponent, BoundUIOpenedEvent>(OnVaultUiOpened);
        SubscribeLocalEvent<DnaVaultComponent, DnaVaultChooseMessage>(OnVaultChoose);
        SubscribeLocalEvent<DnaVaultComponent, ExaminedEvent>(OnVaultExamined);

        SubscribeLocalEvent<DnaProbeComponent, AfterInteractEvent>(OnProbeAfterInteract);
        SubscribeLocalEvent<DnaProbeComponent, ExaminedEvent>(OnProbeExamined);
    }

    #region Хранилище

    /// <summary>Требования цели: 16–20 животных, 8–12 растений, 75–100 % экипажа на момент постройки.</summary>
    private void OnVaultMapInit(Entity<DnaVaultComponent> ent, ref MapInitEvent args)
    {
        var crew = 0;
        var query = EntityQueryEnumerator<ActorComponent, HumanoidProfileComponent>();
        while (query.MoveNext(out _, out _, out _))
        {
            crew++;
        }

        crew = Math.Max(1, crew);
        ent.Comp.AnimalsMax = _random.Next(16, 21);
        ent.Comp.PlantsMax = _random.Next(8, 13);
        ent.Comp.DnaMax = Math.Max(1, _random.Next((int) Math.Round(crew * 0.75), crew + 1));
    }

    private void OnVaultExamined(Entity<DnaVaultComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dna-vault-examine",
            ("dna", ent.Comp.Dna.Count), ("dnaMax", ent.Comp.DnaMax),
            ("plants", ent.Comp.Plants.Count), ("plantsMax", ent.Comp.PlantsMax),
            ("animals", ent.Comp.Animals.Count), ("animalsMax", ent.Comp.AnimalsMax)));
    }

    private void OnVaultUiOpened(Entity<DnaVaultComponent> ent, ref BoundUIOpenedEvent args)
    {
        RollPowers(ent, args.Actor);
        UpdateUi(ent);
    }

    /// <summary>roll_powers: каждому — своя пара улучшений.</summary>
    private void RollPowers(Entity<DnaVaultComponent> ent, EntityUid user)
    {
        if (ent.Comp.PowerLottery.ContainsKey(user) || _mobState.IsDead(user) || HasComp<DnaVaultUsedComponent>(user))
            return;

        var pool = new List<ProtoId<GeneticMutationPrototype>>(ent.Comp.Powers);
        var gained = new List<ProtoId<GeneticMutationPrototype>>();
        for (var i = 0; i < 2 && pool.Count > 0; i++)
        {
            gained.Add(_random.PickAndTake(pool));
        }

        ent.Comp.PowerLottery[user] = gained;
    }

    private void OnVaultChoose(Entity<DnaVaultComponent> ent, ref DnaVaultChooseMessage args)
    {
        var user = args.Actor;
        if (!ent.Comp.Completed
            || HasComp<DnaVaultUsedComponent>(user)
            || !ent.Comp.PowerLottery.TryGetValue(user, out var choices)
            || !choices.Contains(args.Mutation))
        {
            return;
        }

        if (!_genetics.TryActivate(user, args.Mutation, MutationSource.Vault))
        {
            _popup.PopupEntity(Loc.GetString("dna-vault-upgrade-failed"), ent, user);
            return;
        }

        EnsureComp<DnaVaultUsedComponent>(user);
        ent.Comp.PowerLottery[user] = new();
        UpdateUi(ent);
    }

    private void CheckGoal(Entity<DnaVaultComponent> ent)
    {
        if (ent.Comp.Completed)
            return;

        if (ent.Comp.Plants.Count >= ent.Comp.PlantsMax
            && ent.Comp.Animals.Count >= ent.Comp.AnimalsMax
            && ent.Comp.Dna.Count >= ent.Comp.DnaMax)
        {
            ent.Comp.Completed = true;
            _popup.PopupEntity(Loc.GetString("dna-vault-completed"), ent, PopupType.Large);
        }
    }

    private void UpdateUi(Entity<DnaVaultComponent> ent)
    {
        var state = new DnaVaultBoundUserInterfaceState
        {
            Plants = ent.Comp.Plants.Count,
            PlantsMax = ent.Comp.PlantsMax,
            Animals = ent.Comp.Animals.Count,
            AnimalsMax = ent.Comp.AnimalsMax,
            Dna = ent.Comp.Dna.Count,
            DnaMax = ent.Comp.DnaMax,
            Completed = ent.Comp.Completed,
        };

        foreach (var (user, choices) in ent.Comp.PowerLottery)
        {
            if (choices.Count > 0 && !TerminatingOrDeleted(user))
                state.Choices[GetNetEntity(user)] = new(choices);
        }

        _ui.SetUiState(ent.Owner, DnaVaultUiKey.Key, state);
    }

    #endregion

    #region Сэмплер

    private void OnProbeExamined(Entity<DnaProbeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Vault == null)
        {
            args.PushMarkup(Loc.GetString("dna-probe-examine-unlinked"));
            return;
        }

        args.PushMarkup(Loc.GetString("dna-probe-examine",
            ("dna", ent.Comp.Dna.Count), ("plants", ent.Comp.Plants.Count), ("animals", ent.Comp.Animals.Count)));
    }

    private void OnProbeAfterInteract(Entity<DnaProbeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<DnaVaultComponent>(target, out var vault))
        {
            args.Handled = true;
            if (ent.Comp.Vault is { } linked && !TerminatingOrDeleted(linked))
                Upload(ent, (target, vault), args.User);
            else
                Link(ent, target, args.User);
            return;
        }

        if (!IsValidTarget(target))
            return;

        args.Handled = true;
        Scan(ent, target, args.User);
    }

    private bool IsValidTarget(EntityUid target)
    {
        return HasComp<PlantHolderComponent>(target)
            || HasComp<HumanoidProfileComponent>(target)
            || HasComp<MobStateComponent>(target);
    }

    private void Link(Entity<DnaProbeComponent> ent, EntityUid vault, EntityUid user)
    {
        ent.Comp.Vault = vault;
        _audio.PlayPvs(ent.Comp.ScanSound, ent);
        _popup.PopupEntity(Loc.GetString("dna-probe-linked"), ent, user);
    }

    private void Upload(Entity<DnaProbeComponent> ent, Entity<DnaVaultComponent> vault, EntityUid user)
    {
        var uploaded = 0;
        uploaded += Transfer(ent.Comp.Plants, vault.Comp.Plants);
        uploaded += Transfer(ent.Comp.Dna, vault.Comp.Dna);
        uploaded += Transfer(ent.Comp.Animals, vault.Comp.Animals);

        CheckGoal(vault);
        UpdateUi(vault);
        _audio.PlayPvs(vault.Comp.UploadSound, vault);
        _popup.PopupEntity(Loc.GetString("dna-probe-uploaded", ("count", uploaded)), vault, user);
    }

    private static int Transfer(HashSet<string> from, HashSet<string> to)
    {
        var count = 0;
        foreach (var sample in from)
        {
            if (to.Add(sample))
                count++;
        }

        from.Clear();
        return count;
    }

    private void Scan(Entity<DnaProbeComponent> ent, EntityUid target, EntityUid user)
    {
        if (ent.Comp.Vault is not { } vaultUid || !TryComp<DnaVaultComponent>(vaultUid, out var vault))
        {
            Deny(ent, user, "dna-probe-need-database");
            return;
        }

        string key;
        HashSet<string> stored;
        HashSet<string> inVault;

        if (TryComp<PlantHolderComponent>(target, out var plant))
        {
            if (plant.Seed == null)
                return;

            if (!plant.Harvest || plant.Dead)
            {
                Deny(ent, user, "dna-probe-not-harvestable");
                return;
            }

            key = plant.Seed.Name;
            stored = ent.Comp.Plants;
            inVault = vault.Plants;
        }
        else if (HasComp<HumanoidProfileComponent>(target))
        {
            if (!IsOrganic(target) || !TryComp<DnaComponent>(target, out var dna) || string.IsNullOrEmpty(dna.DNA))
            {
                Deny(ent, user, "dna-probe-no-dna");
                return;
            }

            key = dna.DNA;
            stored = ent.Comp.Dna;
            inVault = vault.Dna;
        }
        else
        {
            if (!IsOrganic(target) || HasComp<BorgChassisComponent>(target) || MetaData(target).EntityPrototype is not { } proto)
            {
                Deny(ent, user, "dna-probe-no-dna");
                return;
            }

            key = proto.ID;
            stored = ent.Comp.Animals;
            inVault = vault.Animals;
        }

        if (inVault.Contains(key))
        {
            Deny(ent, user, "dna-probe-already-vault");
            return;
        }

        if (!stored.Add(key))
        {
            Deny(ent, user, "dna-probe-already-scanner");
            return;
        }

        _audio.PlayPvs(ent.Comp.ScanSound, ent);
        _popup.PopupEntity(Loc.GetString("dna-probe-added"), ent, user);
    }

    private bool IsOrganic(EntityUid uid)
    {
        return TryComp<DamageableComponent>(uid, out var damageable) && damageable.DamageContainerID == BiologicalContainer;
    }

    private void Deny(Entity<DnaProbeComponent> ent, EntityUid user, string message)
    {
        _audio.PlayPvs(ent.Comp.DenySound, ent);
        _popup.PopupEntity(Loc.GetString(message), ent, user);
    }

    #endregion
}
