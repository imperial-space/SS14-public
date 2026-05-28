using Content.Server.Imperial.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Blob;

namespace Content.Server.Imperial.Blob;

public sealed class BlobChemistrySystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlobChemicalEffectComponent>();
        while (query.MoveNext(out var uid, out var effect))
        {
            var activeTime = System.MathF.Min(frameTime, System.MathF.Max(effect.TimeRemaining, 0f));
            effect.TimeRemaining -= frameTime;
            effect.TickAccumulator += activeTime;

            while (effect.TickAccumulator >= effect.TickInterval)
            {
                effect.TickAccumulator -= effect.TickInterval;
                var damage = new DamageSpecifier();

                switch (effect.Chemical)
                {
                    case BlobChemicalType.Incendiary:
                        damage.DamageDict.Add("Heat", 2);
                        break;
                    case BlobChemicalType.Electromagnetic:
                        damage.DamageDict.Add("Heat", 1);
                        break;
                    case BlobChemicalType.DistributedNeurons:
                        damage.DamageDict.Add("Poison", 1);
                        break;
                    case BlobChemicalType.Toxin:
                        damage.DamageDict.Add("Poison", 2);
                        break;
                    case BlobChemicalType.LexorinJelly:
                        damage.DamageDict.Add("Asphyxiation", 2);
                        break;
                    case BlobChemicalType.CryogenicLiquid:
                        damage.DamageDict.Add("Cold", 2);
                        break;
                    case BlobChemicalType.RadioactiveGel:
                        damage.DamageDict.Add("Poison", 1);
                        damage.DamageDict.Add("Radiation", 1);
                        break;
                    case BlobChemicalType.EnvenomedFilaments:
                        damage.DamageDict.Add("Poison", 2);
                        break;
                    case BlobChemicalType.ParalyticToxins:
                        damage.DamageDict.Add("Poison", 1);
                        break;
                    default:
                        break;
                }

                if (!damage.Empty)
                    _damage.TryChangeDamage(uid, damage, true);
            }

            if (effect.TimeRemaining > 0f)
                continue;

            RemCompDeferred<BlobChemicalEffectComponent>(uid);
        }
    }

    public void ApplyChemicalEffect(EntityUid uid, BlobChemicalType chemical, float duration)
    {
        var effect = EnsureComp<BlobChemicalEffectComponent>(uid);
        var chemicalChanged = effect.Chemical != chemical;
        effect.TimeRemaining = chemicalChanged
            ? duration
            : System.MathF.Max(effect.TimeRemaining, duration);
        effect.Chemical = chemical;
        if (chemicalChanged)
            effect.TickAccumulator = 0f;
        Dirty(uid, effect);
    }
}