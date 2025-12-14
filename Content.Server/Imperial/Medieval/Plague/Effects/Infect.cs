using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class Infect : BasePlagueEffect
{
    [DataField(required: true)]
    public float Radius;

    [DataField(required: true)]
    public float Chance;

    public override Infect CreateInstance()
    {
        return new Infect()
        {
            Delay = this.Delay,
            Other = this.Other,
            Radius = this.Radius,
            Chance = this.Chance
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var lookup = entMan.System<EntityLookupSystem>();
        var plague = entMan.System<MedievalPlagueSystem>();

        var xform = entMan.GetComponent<TransformComponent>(uid);
        foreach (var item in lookup.GetEntitiesInRange<MedievalPlagueInfectedComponent>(xform.Coordinates, Radius))
        {
            plague.TryInfect(item.Owner, Chance);
        }
    }
}
