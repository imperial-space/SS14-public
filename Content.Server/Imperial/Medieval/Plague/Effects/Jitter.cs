
using Content.Server.Jittering;
using Content.Shared.Imperial.Medieval.Plague;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class Jitter : BasePlagueEffect
{
    [DataField(required: true)]
    public float Duration;

    public override Jitter CreateInstance()
    {
        return new Jitter()
        {
            Delay = this.Delay,
            Other = this.Other,
            Duration = this.Duration
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var jitter = entMan.System<JitteringSystem>();
        jitter.DoJitter(uid, TimeSpan.FromSeconds(Duration), true);
    }
}
