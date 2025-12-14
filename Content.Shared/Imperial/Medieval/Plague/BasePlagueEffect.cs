using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Plague;

[ImplicitDataDefinitionForInheritors]
public abstract partial class BasePlagueEffect
{
    [DataField]
    protected MinMax Delay = new(0, 0);

    [DataField]
    protected BasePlagueEffect[] Other = Array.Empty<BasePlagueEffect>();

    [DataField]
    public int Priority = 1;

    [DataField]
    public float PerformChance = 1f;

    public TimeSpan NextEffect = TimeSpan.Zero;

    public abstract BasePlagueEffect CreateInstance();

    public void DoEffects(EntityUid uid, IEntityManager entManager)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var timing = IoCManager.Resolve<IGameTiming>();
        NextEffect = timing.CurTime + TimeSpan.FromSeconds(random.Next(Delay.Min, Delay.Max));

        if (!random.Prob(PerformChance))
            return;

        foreach (var item in Other)
            item.DoEffects(uid, entManager);

        Effect(uid, entManager);
    }

    protected abstract void Effect(EntityUid uid, IEntityManager entMan);

    public bool CanPerform(IGameTiming timing)
    {
        return NextEffect <= timing.CurTime;
    }
}
