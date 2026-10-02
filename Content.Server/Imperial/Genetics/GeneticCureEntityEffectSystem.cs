using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;

namespace Content.Server.Imperial.Genetics;

/// <inheritdoc cref="GeneticCure"/>
public sealed partial class GeneticCureEntityEffectSystem : EntityEffectSystem<GenomeComponent, GeneticCure>
{
    [Dependency] private readonly GeneticsSystem _genetics = default!;

    protected override void Effect(Entity<GenomeComponent> entity, ref EntityEffectEvent<GeneticCure> args)
    {
        _genetics.DeactivateAll(entity);

        // Собранные гены снова «спят» (default_mutation_genes).
        foreach (var gene in entity.Comp.Blocks)
        {
            gene.Sequence = gene.Default.Length > 0 ? gene.Default : _genetics.CreateChippedSequence(gene.Mutation);
        }

        entity.Comp.MeltdownAt = null;
        RemComp<GeneticDecloningComponent>(entity);
    }
}
