using Content.Server.Chat.Systems;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class Emote : BasePlagueEffect
{
    [DataField(required: true)]
    public string[] Emotes;

    public override Emote CreateInstance()
    {
        return new Emote()
        {
            Delay = this.Delay,
            Other = this.Other,
            Emotes = this.Emotes
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var chat = entMan.System<ChatSystem>();
        var random = IoCManager.Resolve<IRobustRandom>();
        chat.TryEmoteWithChat(uid, random.Pick(Emotes));
    }
}
