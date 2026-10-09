using Content.Shared.Chat;
using Content.Shared.Imperial.Cult;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Cult;

public sealed partial class CultSystem
{
    public void SetState(EntityUid uid, string state) => _appearance.SetData(uid, CultVisuals.State, state);

    public void Message(ICommonSession session, string text)
    {
        _chatManager.ChatMessageToOne(ChatChannel.Server, text, text, EntityUid.Invalid, false, session.Channel, CultRed);
    }

    public void Stun(EntityUid uid, float seconds)
    {
        if (seconds > 0)
            _stun.TryAddStunDuration(uid, TimeSpan.FromSeconds(seconds));
    }
}

public sealed partial class CultSystem
{
    /// <summary>Суммарный урон сущности.</summary>
    public float TotalDamage(EntityUid uid) => _damageable.GetTotalDamage(uid).Float();
}
