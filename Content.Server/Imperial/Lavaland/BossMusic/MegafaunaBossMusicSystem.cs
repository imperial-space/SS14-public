using System.Linq;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Lavaland.BossMusic;

public sealed class MegafaunaBossMusicSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private const float UpdateInterval = 1f;
    private const float MusicVolume = -14f;
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegafaunaBossMusicComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<MegafaunaBossMusicComponent> ent, ref ComponentShutdown args)
    {
        StopAll(ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;
        _accumulator = 0f;

        var query = EntityQueryEnumerator<MegafaunaBossMusicComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out var music, out var mobState, out var xform))
        {
            if (mobState.CurrentState == MobState.Dead)
            {
                StopAll(music);
                continue;
            }

            var nearbyEntities = _lookup.GetEntitiesInRange<ActorComponent>(xform.Coordinates, music.Range);

            var nearbyPlayers = new HashSet<EntityUid>();
            foreach (var e in nearbyEntities)
                nearbyPlayers.Add(e.Owner);

            foreach (var (player, stream) in music.ActiveStreams.ToList())
            {
                if (nearbyPlayers.Contains(player))
                    continue;
                _audio.Stop(stream);
                music.ActiveStreams.Remove(player);
            }

            foreach (var entity in nearbyEntities)
            {
                if (music.ActiveStreams.ContainsKey(entity.Owner))
                    continue;

                if (TryComp<MobStateComponent>(entity.Owner, out var playerMob) && playerMob.CurrentState == MobState.Dead)
                    continue;

                var stream = _audio.PlayGlobal(
                    music.Music,
                    entity.Comp.PlayerSession,
                    AudioParams.Default.WithVolume(MusicVolume).WithLoop(true))?.Entity;

                if (stream != null)
                    music.ActiveStreams[entity.Owner] = stream.Value;
            }
        }
    }

    private void StopAll(MegafaunaBossMusicComponent comp)
    {
        foreach (var stream in comp.ActiveStreams.Values)
            _audio.Stop(stream);
        comp.ActiveStreams.Clear();
    }
}
