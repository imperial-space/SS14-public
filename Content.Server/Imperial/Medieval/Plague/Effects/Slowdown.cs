
using Content.Server.Administration.Commands;
using Content.Server.Maps;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Chemistry.Components;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class Slowdown : BasePlagueEffect
{
    [DataField(required: true)]
    public float Duration;

    [DataField]
    public bool Refresh = true;

    public override Slowdown CreateInstance()
    {
        return new Slowdown()
        {
            Delay = this.Delay,
            Other = this.Other,
            Duration = this.Duration,
            Refresh = this.Refresh
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var comp = entMan.EnsureComponent<MovespeedModifierMetabolismComponent>(uid);
        comp.SprintSpeedModifier = 0.7f;
        comp.WalkSpeedModifier = 0.7f;
        comp.ModifierTimer = IoCManager.Resolve<IGameTiming>().CurTime + TimeSpan.FromSeconds(Duration);
        entMan.Dirty(uid, comp);
    }
}
