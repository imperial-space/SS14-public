using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server.Body.Systems;

public sealed class CritEmotesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SoftCritEmotesComponent, MobStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var crit, out var mob, out var damageable))
        {
            if (_gameTiming.CurTime >= crit.NextEmoteUpdate && crit.Emote)
            {
                crit.NextEmoteUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(_random.NextFloat(4f, 7f));

                if (damageable.Damage.GetTotal() < crit.MinDamage || mob.CurrentState == Shared.Mobs.MobState.Dead)
                    continue;

                _chat.TryEmoteWithChat(uid, _random.Pick(crit.Emotes), ChatTransmitRange.HideChat, ignoreActionBlocker: true);
            }
            if (_gameTiming.CurTime >= crit.NextHeartbeatUpdate && crit.Heartbeat)
            {
                crit.NextHeartbeatUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(_random.NextFloat(1.4f, 2.1f));

                if (damageable.Damage.GetTotal() < crit.MinDamage || mob.CurrentState == Shared.Mobs.MobState.Dead)
                    continue;

                _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/heartbeat.ogg"), uid);
            }
        }
    }
}
