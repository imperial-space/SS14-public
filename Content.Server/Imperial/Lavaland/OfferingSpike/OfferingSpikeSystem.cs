using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.OfferingSpike;

/// <summary>
/// Обслуживает <see cref="OfferingSpikeComponent"/>: раз в <see cref="OfferingSpikeComponent.CheckInterval"/>
/// секунд сканирует мёртвые тела в радиусе, поглощает их и при накоплении нужного количества
/// создаёт яйцо пеплоходца.
/// </summary>
public sealed class OfferingSpikeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<EntityUid> _toAbsorb = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<OfferingSpikeComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (now < comp.NextCheckTime)
                continue;

            comp.NextCheckTime = now + TimeSpan.FromSeconds(comp.CheckInterval);

            _toAbsorb.Clear();
            var coords = xform.Coordinates;

            foreach (var (mobUid, mobState) in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.CorpseRadius))
            {
                if (mobState.CurrentState != MobState.Dead)
                    continue;

                // Не поглощаем тела с активным разумом (игрок ещё в теле)
                if (TryComp<MindContainerComponent>(mobUid, out var mind) && mind.HasMind)
                    continue;

                // Не поглощаем саму структуру, если у неё вдруг есть MobState
                if (mobUid == uid)
                    continue;

                _toAbsorb.Add(mobUid);
            }

            foreach (var corpse in _toAbsorb)
            {
                QueueDel(corpse);
                comp.AccumulatedCorpses++;

                _audio.PlayPvs(comp.AbsorbSound, uid);
                _popup.PopupCoordinates(
                    Loc.GetString("offering-spike-absorb"),
                    coords,
                    PopupType.Medium);

                if (comp.AccumulatedCorpses >= comp.CorpsesPerEgg)
                {
                    comp.AccumulatedCorpses -= comp.CorpsesPerEgg;
                    var proto = _random.Pick(comp.EggPrototypes);
                    Spawn(proto, coords);
                    _audio.PlayPvs(comp.EggSpawnSound, uid);

                    _popup.PopupCoordinates(
                        Loc.GetString("offering-spike-egg-spawned"),
                        coords,
                        PopupType.LargeCaution);
                }
            }
        }
    }
}
