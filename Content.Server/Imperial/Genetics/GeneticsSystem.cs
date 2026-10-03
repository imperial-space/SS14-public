using System.Linq;
using System.Text;
using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// Генетика (datum/dna и datum/mutation из SS13): геном из 8 блоков со спящими генами, полные последовательности
/// генов на раунд, активация и снятие мутаций, генетическая стабильность и распад ДНК.
/// </summary>
public sealed class GeneticsSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>Блоков генов в геноме (DNA_MUTATION_BLOCKS).</summary>
    public const int BlockCount = 8;

    /// <summary>Пар оснований в последовательности гена (4 блока по DNA_SEQUENCE_LENGTH).</summary>
    public const int SequencePairs = 16;

    public const char Unknown = 'X';

    /// <summary>Генетический урон, с которого начинается урон токсинами.</summary>
    public const float GeneticDamageToxThreshold = 500f;

    /// <summary>Обезьяний ген: всегда первый блок генома.</summary>
    public static readonly ProtoId<GeneticMutationPrototype> RaceMutation = "MutationMonkified";

    /// <summary>Сколько ждать срыва после падения стабильности до нуля (dna_melt).</summary>
    public static readonly TimeSpan MeltdownDelay = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private static readonly string[] Pairs = { "AT", "TA", "GC", "CG" };

    private readonly Dictionary<ProtoId<GeneticMutationPrototype>, string> _fullSequences = new();
    private readonly Dictionary<ProtoId<GeneticMutationPrototype>, int> _aliases = new();
    private readonly HashSet<ProtoId<GeneticMutationPrototype>> _discovered = new();
    private TimeSpan _nextTick;

    /// <summary>Мутации, открытые за раунд: их можно печатать в инжекторы.</summary>
    public IReadOnlySet<ProtoId<GeneticMutationPrototype>> Discovered => _discovered;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<GenomeComponent, ComponentShutdown>(OnGenomeShutdown);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _fullSequences.Clear();
        _aliases.Clear();
        _discovered.Clear();
    }

    private void OnGenomeShutdown(Entity<GenomeComponent> ent, ref ComponentShutdown args)
    {
        foreach (var active in ent.Comp.Active.Values)
        {
            foreach (var action in active.ActionEntities)
            {
                _actions.RemoveAction(action);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var tick = now >= _nextTick;
        if (tick)
            _nextTick = now + TickInterval;

        var query = EntityQueryEnumerator<GenomeComponent>();
        while (query.MoveNext(out var uid, out var genome))
        {
            if (tick)
            {
                ProcessTicks((uid, genome));
                ProcessGeneticDamage((uid, genome));
            }

            if (genome.MeltdownAt is not { } at || now < at)
                continue;

            genome.MeltdownAt = null;
            var ev = new GenomeMeltdownEvent(-genome.Stability);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    /// <summary>Генетический урон: спадает на 1/3 в секунду, от 500 и выше бьёт токсинами (genetic_damage).</summary>
    private void ProcessGeneticDamage(Entity<GenomeComponent> genome)
    {
        if (genome.Comp.GeneticDamage <= 0)
            return;

        if (genome.Comp.GeneticDamage >= GeneticDamageToxThreshold)
            _damageable.TryChangeDamage(genome.Owner, new DamageSpecifier { DamageDict = { ["Poison"] = 1f / 3f } }, true);

        genome.Comp.GeneticDamage = Math.Max(0f, genome.Comp.GeneticDamage - 1f / 3f);
    }

    public void AddGeneticDamage(EntityUid uid, float amount)
    {
        if (TryComp<GenomeComponent>(uid, out var genome))
            genome.GeneticDamage += amount;
    }

    /// <summary>Периодические эффекты активных мутаций (on_life).</summary>
    private void ProcessTicks(Entity<GenomeComponent> genome)
    {
        if (genome.Comp.Active.Count == 0)
            return;

        var conscious = _mobState.IsAlive(genome) && !HasComp<SleepingComponent>(genome);
        foreach (var (id, active) in genome.Comp.Active)
        {
            if (!_proto.TryIndex(id, out var proto) || proto.Ticks.Count == 0)
                continue;

            var synchronizer = (active.Chromosome & ChromosomeKind.Synchronizer) != 0 ? 0.5f : 1f;
            var power = (active.Chromosome & ChromosomeKind.Power) != 0 ? 1.5f : 1f;

            foreach (var tick in proto.Ticks)
            {
                if (tick.RequiresConscious && !conscious)
                    continue;

                var chance = tick.Chance;
                if (tick.ScalesWithInstability)
                    chance += (100 - genome.Comp.Stability) / 1950f;

                if (!_random.Prob(Math.Clamp(chance * synchronizer, 0f, 1f)))
                    continue;

                ApplyTick(genome, tick, power);
            }
        }
    }

    private void ApplyTick(EntityUid uid, MutationTick tick, float power)
    {
        if (tick.DropHeld)
        {
            foreach (var hand in _hands.EnumerateHands(uid))
            {
                _hands.TryDrop(uid, hand, checkActionBlocker: false);
            }
        }

        if (tick.Say.Count > 0)
            _chat.TrySendInGameICMessage(uid, Loc.GetString(_random.Pick(tick.Say)), InGameICChatType.Speak, false, ignoreActionBlocker: true);

        if (tick.Popup is { } popup)
            _popup.PopupEntity(Loc.GetString(popup), uid, uid, PopupType.MediumCaution);

        if (tick.Effects.Length > 0)
            _effects.ApplyEffects(uid, tick.Effects, power);
    }

    #region Последовательности

    /// <summary>Полная последовательность гена на этот раунд (GET_SEQUENCE): одна на всех.</summary>
    public string GetFullSequence(ProtoId<GeneticMutationPrototype> mutation)
    {
        if (_fullSequences.TryGetValue(mutation, out var sequence))
            return sequence;

        var builder = new StringBuilder(SequencePairs * 2);
        for (var i = 0; i < SequencePairs; i++)
        {
            builder.Append(_random.Pick(Pairs));
        }

        sequence = builder.ToString();
        _fullSequences[mutation] = sequence;
        return sequence;
    }

    /// <summary>«Мутация №49» — имя ещё не открытой мутации.</summary>
    public int GetAlias(ProtoId<GeneticMutationPrototype> mutation)
    {
        if (_aliases.TryGetValue(mutation, out var alias))
            return alias;

        do
        {
            alias = _random.Next(1, 1000);
        }
        while (_aliases.ContainsValue(alias));

        _aliases[mutation] = alias;
        return alias;
    }

    public bool IsDiscovered(ProtoId<GeneticMutationPrototype> mutation)
    {
        return _discovered.Contains(mutation);
    }

    public void Discover(ProtoId<GeneticMutationPrototype> mutation)
    {
        _discovered.Add(mutation);
    }

    /// <summary>Сколами (create_sequence): часть позиций полной последовательности заменена на «X».</summary>
    public string CreateChippedSequence(ProtoId<GeneticMutationPrototype> mutation)
    {
        var full = GetFullSequence(mutation).ToCharArray();
        var difficulty = _proto.Index(mutation).Difficulty + _random.Next(-2, 5);
        for (var i = 0; i < difficulty; i++)
        {
            full[_random.Next(full.Length)] = Unknown;
        }

        return new string(full);
    }

    public bool IsComplete(GeneBlock block)
    {
        return block.Sequence == GetFullSequence(block.Mutation);
    }

    #endregion

    #region Геном

    public bool CanHaveGenome(EntityUid uid)
    {
        return HasComp<HumanoidProfileComponent>(uid);
    }

    /// <summary>Геном существа: заводится при первом обращении (generate_dna_blocks).</summary>
    public bool TryGetGenome(EntityUid uid, out Entity<GenomeComponent> genome)
    {
        genome = default;
        if (TryComp<GenomeComponent>(uid, out var existing))
        {
            genome = (uid, existing);
            return true;
        }

        if (!CanHaveGenome(uid))
            return false;

        var comp = AddComp<GenomeComponent>(uid);
        comp.Blocks.Add(NewBlock(RaceMutation));

        var pool = _proto.EnumeratePrototypes<GeneticMutationPrototype>()
            .Where(p => p.Natural && !p.Locked)
            .ToList();

        while (comp.Blocks.Count < BlockCount && pool.Count > 0)
        {
            var picked = PickWeighted(pool);
            pool.Remove(picked);
            comp.Blocks.Add(NewBlock(picked.ID));
        }

        // Обезьяний ген тоже перемешивается, как в SS13 (shuffle_inplace).
        _random.Shuffle(comp.Blocks);
        genome = (uid, comp);
        return true;
    }

    private GeneBlock NewBlock(ProtoId<GeneticMutationPrototype> mutation)
    {
        var sequence = CreateChippedSequence(mutation);
        return new GeneBlock { Mutation = mutation, Sequence = sequence, Default = sequence };
    }

    private GeneticMutationPrototype PickWeighted(List<GeneticMutationPrototype> pool)
    {
        var total = pool.Sum(p => p.Weight);
        var roll = _random.NextFloat() * total;
        foreach (var proto in pool)
        {
            roll -= proto.Weight;
            if (roll <= 0)
                return proto;
        }

        return pool[^1];
    }

    public bool HasMutation(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation)
    {
        return TryComp<GenomeComponent>(uid, out var genome) && genome.Active.ContainsKey(mutation);
    }

    /// <summary>
    /// Включает мутацию (on_acquiring): проверяет конфликты, выдаёт компоненты и действия, пересчитывает стабильность.
    /// </summary>
    public bool TryActivate(EntityUid uid,
        ProtoId<GeneticMutationPrototype> mutation,
        MutationSource source,
        ChromosomeKind chromosome = ChromosomeKind.None,
        bool silent = false)
    {
        if (!TryGetGenome(uid, out var genome) || _mobState.IsDead(uid))
            return false;

        if (!_proto.TryIndex(mutation, out var proto))
            return false;

        if (genome.Comp.Active.TryGetValue(mutation, out var existing))
        {
            existing.Sources |= source;
            RecalculateStability(genome);
            return true;
        }

        foreach (var activeId in genome.Comp.Active.Keys)
        {
            if (proto.Conflicts.Contains(activeId)
                || _proto.TryIndex(activeId, out var activeProto) && activeProto.Conflicts.Contains(mutation))
            {
                return false;
            }
        }

        var active = new ActiveMutation { Sources = source, Chromosome = chromosome };
        genome.Comp.Active[mutation] = active;

        var missing = new ComponentRegistry();
        foreach (var (name, entry) in proto.Components)
        {
            if (HasComp(uid, _compFactory.GetRegistration(name).Type))
                continue;

            missing[name] = entry;
            active.AddedComponents.Add(name);
        }

        EntityManager.AddComponents(uid, missing, removeExisting: false);

        foreach (var action in proto.Actions)
        {
            if (_actions.AddAction(uid, action) is not { } actionEnt)
                continue;

            active.ActionEntities.Add(actionEnt);

            // Энергетическая хромосома вдвое сокращает откат способности.
            if ((chromosome & ChromosomeKind.Energy) != 0
                && TryComp<ActionComponent>(actionEnt, out var actionComp)
                && actionComp.UseDelay is { } delay)
            {
                _actions.SetUseDelay((actionEnt, actionComp), delay / 2);
            }
        }

        Discover(mutation);

        if (!silent && proto.GainText is { } gain)
            _popup.PopupEntity(Loc.GetString(gain), uid, uid, PopupType.Medium);

        var ev = new MutationActivatedEvent(mutation, chromosome);
        RaiseLocalEvent(uid, ref ev);

        RecalculateStability(genome);
        return true;
    }

    /// <summary>Вставляет хромосому в активную мутацию (apply_chromo). Мутация переактивируется с новыми свойствами.</summary>
    public bool ApplyChromosome(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation, ChromosomeKind kind)
    {
        if (!TryComp<GenomeComponent>(uid, out var genome)
            || !genome.Active.TryGetValue(mutation, out var active)
            || active.Chromosome != ChromosomeKind.None
            || !_proto.TryIndex(mutation, out var proto)
            || (proto.Chromosomes & kind) == 0)
        {
            return false;
        }

        var sources = active.Sources;
        Deactivate(uid, mutation, silent: true);
        TryActivate(uid, mutation, sources, kind, silent: true);
        return true;
    }

    /// <summary>Снимает мутацию: забирает только то, что она сама дала.</summary>
    public bool Deactivate(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation, bool silent = false)
    {
        if (!TryComp<GenomeComponent>(uid, out var genome)
            || !genome.Active.Remove(mutation, out var active))
        {
            return false;
        }

        foreach (var name in active.AddedComponents)
        {
            RemComp(uid, _compFactory.GetRegistration(name).Type);
        }

        foreach (var action in active.ActionEntities)
        {
            _actions.RemoveAction(action);
        }

        if (!silent && _proto.TryIndex(mutation, out var proto) && proto.LoseText is { } lose)
            _popup.PopupEntity(Loc.GetString(lose), uid, uid, PopupType.Medium);

        var ev = new MutationDeactivatedEvent(mutation);
        RaiseLocalEvent(uid, ref ev);

        RecalculateStability((uid, genome));
        return true;
    }

    /// <summary>Снимает все мутации (мутадон, админская команда).</summary>
    public void DeactivateAll(EntityUid uid, bool keepNatural = false, bool includeVault = false)
    {
        if (!TryComp<GenomeComponent>(uid, out var genome))
            return;

        foreach (var (id, active) in genome.Active.ToArray())
        {
            if (!includeVault && (active.Sources & MutationSource.Vault) != 0)
                continue;

            if (keepNatural && (active.Sources & MutationSource.Mutator) == 0)
                continue;

            Deactivate(uid, id);
        }
    }

    /// <summary>
    /// Ставит основание в позицию гена. Если ген собран полностью — мутация включается, если сломан — выключается.
    /// </summary>
    public bool SetBase(EntityUid uid, int block, int position, char nucleotide)
    {
        if (!TryGetGenome(uid, out var genome)
            || block < 0 || block >= genome.Comp.Blocks.Count
            || position < 0 || position >= SequencePairs * 2
            || nucleotide is not ('A' or 'T' or 'C' or 'G' or Unknown))
        {
            return false;
        }

        var gene = genome.Comp.Blocks[block];
        var chars = gene.Sequence.ToCharArray();
        chars[position] = nucleotide;
        gene.Sequence = new string(chars);
        UpdateBlockActivation(genome, gene);
        return true;
    }

    /// <summary>Ген собран — мутация активна, не собран — снята (если её не держит мутатор).</summary>
    public void UpdateBlockActivation(Entity<GenomeComponent> genome, GeneBlock gene)
    {
        var complete = IsComplete(gene);
        var active = genome.Comp.Active.TryGetValue(gene.Mutation, out var existing);

        if (complete && !active)
        {
            TryActivate(genome, gene.Mutation, MutationSource.Activated);
        }
        else if (!complete && active && (existing!.Sources & MutationSource.Mutator) == 0)
        {
            Deactivate(genome, gene.Mutation);
        }
    }

    /// <summary>Переписывает ген полной последовательностью (активатор).</summary>
    public bool CompleteBlock(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation)
    {
        if (!TryGetGenome(uid, out var genome))
            return false;

        foreach (var gene in genome.Comp.Blocks)
        {
            if (gene.Mutation != mutation)
                continue;

            gene.Sequence = GetFullSequence(mutation);
            UpdateBlockActivation(genome, gene);
            return true;
        }

        return false;
    }

    /// <summary>Перемешивает геном заново (scramble_dna): новые спящие гены, все мутации кроме мутаторных сняты.</summary>
    public void Scramble(EntityUid uid)
    {
        if (!TryGetGenome(uid, out var genome))
            return;

        DeactivateAll(uid, keepNatural: false);
        RemComp<GenomeComponent>(uid);
        TryGetGenome(uid, out _);
    }

    #endregion

    #region Стабильность

    /// <summary>
    /// stability = 100 − нестабильность мутаций от мутатора и негативных мутаций (update_instability).
    /// </summary>
    public void RecalculateStability(Entity<GenomeComponent> genome)
    {
        var old = genome.Comp.Stability;
        var stability = 100f;

        foreach (var (id, active) in genome.Comp.Active)
        {
            if (!_proto.TryIndex(id, out var proto))
                continue;

            if ((active.Sources & MutationSource.Mutator) == 0 && proto.Instability >= 0)
                continue;

            var stabilizer = (active.Chromosome & ChromosomeKind.Stabilizer) != 0 ? 0.8f : 1f;
            stability -= proto.Instability * stabilizer;
        }

        genome.Comp.Stability = (int) MathF.Round(stability);

        if (genome.Comp.Stability <= 0)
            genome.Comp.MeltdownAt ??= _timing.CurTime + MeltdownDelay;
        else
            genome.Comp.MeltdownAt = null;

        if (genome.Comp.Stability >= old)
            return;

        var message = genome.Comp.Stability switch
        {
            >= 70 and <= 90 => "genetics-stability-shiver",
            >= 60 and < 70 => "genetics-stability-cold",
            >= 40 and < 60 => "genetics-stability-sick",
            >= 20 and < 40 => "genetics-stability-skin",
            >= 1 and < 20 => "genetics-stability-burning",
            <= 0 => "genetics-stability-exploding",
            _ => null,
        };

        if (message != null)
        {
            _popup.PopupEntity(Loc.GetString(message), genome, genome,
                genome.Comp.Stability <= 0 ? PopupType.LargeCaution : PopupType.MediumCaution);
        }
    }

    #endregion
}
