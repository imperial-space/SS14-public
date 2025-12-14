using Content.Shared.Summoning;
using Robust.Shared.Map;
using Content.Shared.Interaction;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Robust.Shared.Physics.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Summoning;

public sealed class MedievalSummonOnUseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly List<SummonRequest> _pendingSummons = new();
    private const float CheckRadius = 3f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalSummonOnUseComponent, AfterInteractEvent>(OnAfterInteract);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var currentTime = _gameTiming.CurTime;

        for (var i = _pendingSummons.Count - 1; i >= 0; i--)
        {
            var request = _pendingSummons[i];
            if (currentTime >= request.SpawnTime)
            {
                Spawn(request.EntityToSummon, request.Coordinates);
                _pendingSummons.RemoveAt(i);
            }
        }
    }

    private void OnAfterInteract(EntityUid uid, MedievalSummonOnUseComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target != null || args.Handled || args.Used != uid)
            return;

        var coordinates = args.ClickLocation;

        if (!HasEnoughSpace(coordinates, args.User))
        {
            _popup.PopupEntity("Нужно больше свободного места для призыва!", args.User, args.User);
            return;
        }

        StartSummoning(uid, component, coordinates);
        args.Handled = true;
    }

    private bool HasEnoughSpace(EntityCoordinates coordinates, EntityUid user)
    {
        try
        {
            var mapCoords = _transform.ToMapCoordinates(coordinates);
            var physicsQuery = GetEntityQuery<PhysicsComponent>();
            var occluderQuery = GetEntityQuery<OccluderComponent>();

            var entities = _lookup.GetEntitiesInRange(mapCoords.MapId, mapCoords.Position, CheckRadius);

            foreach (var entity in entities)
            {
                if (entity == user) continue;

                if (occluderQuery.HasComponent(entity))// ||
                    //physicsQuery.TryGetComponent(entity, out var physics) && physics.CanCollide)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return true;
        }
    }

    private void StartSummoning(EntityUid item, MedievalSummonOnUseComponent component, EntityCoordinates coordinates)
    {
        if (component.UseSound != null)
        {
            _audio.PlayPvs(component.UseSound, coordinates);
        }

        Spawn(component.SmokeEffect, coordinates);
        QueueDel(item);

        _pendingSummons.Add(new SummonRequest
        {
            EntityToSummon = component.EntityToSummon,
            Coordinates = coordinates,
            SpawnTime = _gameTiming.CurTime + component.SummonDelay
        });
    }

    private struct SummonRequest
    {
        public EntProtoId EntityToSummon;
        public EntityCoordinates Coordinates;
        public TimeSpan SpawnTime;
    }
}
