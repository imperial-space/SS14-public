using System.Linq;
using Content.Server.Polymorph.Systems;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>
/// Обезьяний ген (mutation/race из SS13). Собранный ген превращает гуманоида в обезьяну, а обезьяна получает
/// копию его генома с активным обезьяньим геном. Если у обезьяны этот ген сломать, превращение откатывается
/// и прогресс по генам возвращается человеку.
/// </summary>
public sealed class GeneticMonkeySystem : EntitySystem
{
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    private static readonly ProtoId<PolymorphPrototype> MonkeyPolymorph = "ImperialGeneticMonkey";

    /// <summary>Превращения откладываются до тика, чтобы не удалять сущность посреди работы с геномом.</summary>
    private readonly HashSet<EntityUid> _toMonkey = new();
    private readonly HashSet<EntityUid> _toHuman = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenomeComponent, MutationActivatedEvent>(OnActivated);
        SubscribeLocalEvent<GeneticMonkeyComponent, MutationDeactivatedEvent>(OnMonkeyDeactivated);
    }

    private void OnActivated(Entity<GenomeComponent> ent, ref MutationActivatedEvent args)
    {
        if (args.Mutation == GeneticsSystem.RaceMutation && !HasComp<GeneticMonkeyComponent>(ent))
            _toMonkey.Add(ent);
    }

    private void OnMonkeyDeactivated(Entity<GeneticMonkeyComponent> ent, ref MutationDeactivatedEvent args)
    {
        if (args.Mutation == GeneticsSystem.RaceMutation)
            _toHuman.Add(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var human in _toMonkey)
        {
            if (!TerminatingOrDeleted(human))
                Monkeyize(human);
        }

        foreach (var monkey in _toHuman)
        {
            if (!TerminatingOrDeleted(monkey))
                Humanize(monkey);
        }

        _toMonkey.Clear();
        _toHuman.Clear();
    }

    private void Monkeyize(EntityUid human)
    {
        if (!TryComp<GenomeComponent>(human, out var humanGenome)
            || !humanGenome.Active.ContainsKey(GeneticsSystem.RaceMutation))
        {
            return;
        }

        var blocks = CopyBlocks(humanGenome);
        if (_polymorph.PolymorphEntity(human, MonkeyPolymorph) is not { } monkey)
            return;

        var genome = EnsureComp<GenomeComponent>(monkey);
        genome.Blocks = blocks;
        genome.Active[GeneticsSystem.RaceMutation] = new ActiveMutation { Sources = MutationSource.Activated };
        _genetics.RecalculateStability((monkey, genome));
        EnsureComp<GeneticMonkeyComponent>(monkey);
    }

    /// <summary>Обезьяний ген у обезьяны сломан или снят — она снова становится человеком.</summary>
    private void Humanize(EntityUid monkey)
    {
        var blocks = TryComp<GenomeComponent>(monkey, out var monkeyGenome) ? CopyBlocks(monkeyGenome) : null;
        if (_polymorph.Revert(monkey) is not { } human || !TryComp<GenomeComponent>(human, out var genome))
            return;

        if (blocks != null)
            genome.Blocks = blocks;

        // Ген уже сломан: убираем отметку о нём, не повторяя превращение.
        genome.Active.Remove(GeneticsSystem.RaceMutation);
        _genetics.RecalculateStability((human, genome));
    }

    private static List<GeneBlock> CopyBlocks(GenomeComponent genome)
    {
        return genome.Blocks.Select(b => new GeneBlock { Mutation = b.Mutation, Sequence = b.Sequence, Default = b.Default }).ToList();
    }
}

/// <summary>Обезьяна, в которую превратился гуманоид из-за обезьяньего гена.</summary>
[RegisterComponent]
public sealed partial class GeneticMonkeyComponent : Component;
