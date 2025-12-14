
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class Popup : BasePlagueEffect
{
    [DataField(required: true)]
    public string Text;

    [DataField]
    public PopupType Type = PopupType.Small;

    [DataField]
    public bool Local = true;

    public override Popup CreateInstance()
    {
        return new Popup()
        {
            Delay = this.Delay,
            Other = this.Other,
            Text = this.Text,
            Type = this.Type,
            Local = this.Local
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var popup = entMan.System<PopupSystem>();
        if (Local)
            popup.PopupEntity(Loc.GetString(Text), uid, uid, Type);
        else
            popup.PopupEntity(Loc.GetString(Text), uid, Type);
    }
}
