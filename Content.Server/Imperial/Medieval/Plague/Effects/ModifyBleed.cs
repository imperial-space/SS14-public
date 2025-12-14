
using Content.Server.Body.Systems;
using Content.Shared.EntityEffects.Effects;
using Content.Server.Jittering;
using Content.Shared.Imperial.Medieval.Plague;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class ModifyBleed : BasePlagueEffect
{
    [DataField(required: true)]
    public float Bleed;

    public override ModifyBleed CreateInstance()
    {
        return new ModifyBleed()
        {
            Delay = this.Delay,
            Other = this.Other,
            Bleed = this.Bleed
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var bloodstream = entMan.System<BloodstreamSystem>();
        bloodstream.TryModifyBleedAmount(uid, Bleed);
    }
}
