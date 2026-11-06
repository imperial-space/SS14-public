using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Disease;

namespace Content.Server.Imperial.Disease;

/// <summary>
/// chemical_reaction/mix_virus/on_reaction(): вирусная еда мутирует культуру в ёмкости.
/// </summary>
public sealed class DiseaseMutateEffectSystem : EntityEffectSystem<DiseaseSampleComponent, DiseaseMutate>
{
    [Dependency] private readonly DiseaseSystem _disease = default!;

    protected override void Effect(Entity<DiseaseSampleComponent> entity, ref EntityEffectEvent<DiseaseMutate> args)
    {
        foreach (var disease in entity.Comp.Diseases)
        {
            if (!disease.Advanced)
                continue;

            switch (args.Effect.Mode)
            {
                case DiseaseMutateMode.Evolve:
                    _disease.Evolve(disease, args.Effect.LevelMin, args.Effect.LevelMax);
                    break;
                case DiseaseMutateMode.Devolve:
                    _disease.Devolve(disease);
                    break;
                case DiseaseMutateMode.Neuter:
                    _disease.Neuter(disease);
                    break;
            }

            return;
        }
    }
}
