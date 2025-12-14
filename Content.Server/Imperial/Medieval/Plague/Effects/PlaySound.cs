
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class PlaySound : BasePlagueEffect
{
    [DataField(required: true)]
    public SoundSpecifier Sound;

    [DataField]
    public bool Global = true;

    public override PlaySound CreateInstance()
    {
        return new PlaySound()
        {
            Delay = this.Delay,
            Other = this.Other,
            Sound = this.Sound,
            Global = this.Global
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var audio = entMan.System<AudioSystem>();
        if (Global)
            audio.PlayGlobal(Sound, uid);
        else
            audio.PlayPvs(Sound, uid);
    }
}
