
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class AddComponents : BasePlagueEffect
{
    [DataField(required: true)]
    public ComponentRegistry Components;

    public override AddComponents CreateInstance()
    {
        return new AddComponents()
        {
            Delay = this.Delay,
            Other = this.Other,
            Components = this.Components
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        foreach (var item in Components)
        {
            if (entMan.HasComponent(uid, item.Value.Component.GetType()))
                return;
        }

        entMan.AddComponents(uid, Components);
    }
}
