using Content.Server.Chat.Systems;
using Content.Server.Damage.ForceSay;
using Content.Shared.Damage;
using Content.Shared.Damage.ForceSay;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class ForceSay : BasePlagueEffect
{
    public override ForceSay CreateInstance()
    {
        return new ForceSay()
        {
            Delay = this.Delay,
            Other = this.Other
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<DamageForceSayComponent>(uid, out var comp))
            return;

        var forceSay = entMan.System<DamageForceSaySystem>();
        forceSay.TryForceSay(uid, comp);
        forceSay.AllowNextSpeech(uid);
    }
}
