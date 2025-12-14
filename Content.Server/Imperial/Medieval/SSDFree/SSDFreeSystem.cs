using Content.Server.SSDFree.Components;
using Content.Shared.SSDFree.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Server.Player;
using Robust.Shared.Network;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Polymorph.Systems;
using Content.Shared.SSDIndicator;
using Content.Shared.Mobs.Systems;
using Content.Shared.Interaction;
using Content.Shared.Examine;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Content.Shared.Inventory;

namespace Content.Server.SSDFree
{
    public sealed partial class SSDFreeSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly PolymorphSystem _polymorphSystem = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly StationJobsSystem _stationJobs = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<HolySaltComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<SSDFreeComponent, ExaminedEvent>(OnExamine);


        }

        public void OnUseInHand(EntityUid uid, HolySaltComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used);
        }

        private void OnExamine(EntityUid uid, SSDFreeComponent component, ExaminedEvent args)
        {
            if (TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD || _mobState.IsDead(uid))
            {
                if (component.GoSkeleton)
                    args.PushMarkup("[color=red]Тело не освящено[/color]");
                else
                    args.PushMarkup("[color=green]Тело освящено[/color]");

            }

        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used)
        {
            if (target == null)
                return;
            if (TryComp<SSDFreeComponent>(target, out var ssdfree) && ssdfree != null)
            {
                if (TryComp<SSDIndicatorComponent>(ssdfree.Owner, out var ssd) && ssd.IsSSD || _mobState.IsDead(ssdfree.Owner))
                {
                    ssdfree.GoSkeleton = false;
                    QueueDel(used);
                }
            }
        }

        TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        TimeSpan ReloadTime = TimeSpan.FromSeconds(60f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + ReloadTime;

                foreach (var comp in EntityManager.EntityQuery<SSDFreeComponent>())
                {
                    if (comp.Enabled)
                    {
                        if (TryComp<SSDIndicatorComponent>(comp.Owner, out var ssd) && ssd.IsSSD || _mobState.IsDead(comp.Owner))
                        {
                            if (comp.UnholyValue < comp.UnholyMaxValue)
                                comp.UnholyValue += comp.UnholySpeed;
                        }

                        if (comp.CommonSession != null && comp.UnholyValue >= comp.UnholyMaxValue)
                        {
                            var targetUser = comp.CommonSession.UserId;

                            GoToSSD(comp.Owner, targetUser, true, comp);

                        }

                        if (comp.CommonSession != null &&
                        comp.UnholyValue < comp.UnholyMaxValue &&
                        !_mobState.IsDead(comp.Owner) &&
                        TryComp<SSDIndicatorComponent>(comp.Owner, out var ssdind) &&
                        !ssdind.IsSSD)
                        {
                            if (comp.UnholyValue > 0f)
                                comp.UnholyValue -= comp.UnholySpeed;
                        }

                        if (_timing.CurTime > comp.EndTimeSes && comp.Enabled)
                        {
                            comp.StartTimeSes = _timing.CurTime;
                            comp.EndTimeSes = comp.StartTimeSes + comp.ReloadTimeSes;
                            if (_playerManager.TryGetSessionByEntity(comp.Owner, out var session))
                                comp.CommonSession = session;
                        }
                    }
                }
            }
        }
        public void GoToSSD(EntityUid uid, NetUserId? userId, bool skelet, SSDFreeComponent comp)
        {
            comp.Enabled = false;
            // if we have a session, we use that to add back in all the job slots the player had.
            if (userId != null)
            {
                foreach (var uniqueStation in _station.GetStationsSet())
                {
                    if (!TryComp<StationJobsComponent>(uniqueStation, out var stationJobs))
                        continue;

                    if (!_stationJobs.TryGetPlayerJobs(uniqueStation, userId.Value, out var jobs, stationJobs))
                        continue;

                    foreach (var job in jobs)
                    {
                        _stationJobs.TryAdjustJobSlot(uniqueStation, job, 1, clamp: true);
                    }

                    _stationJobs.TryRemovePlayerJobs(uniqueStation, userId.Value, stationJobs);
                }
            }
            if (TryComp<SSDFreeComponent>(uid, out var ssdfree) && skelet)
            {
                var xform = Transform(uid);
                var coords = xform.Coordinates;
                var items = _inventory.GetHandOrInventoryEntities(uid);
                Spawn("MedievalSkeletDespawnEffect", coords);
                foreach (var item in items)
                {
                    Transform(item).Coordinates = coords;
                }
                if (!comp.DragonEaten)
                {
                    if (ssdfree.GoSkeleton && !HasComp<ActorComponent>(uid) && !CheckSpawnArea(Transform(uid).Coordinates))
                    {
                        Spawn("MedievalMobSkeletMeat", coords);
                        QueueDel(uid);
                    }
                    else
                    {
                        QueueDel(uid);
                    }
                }
                else
                {

                }

            }

        }

        public bool CheckSpawnArea(EntityCoordinates coords)
        {
            foreach (var entity in _lookup.GetEntitiesInRange(coords, 9, flags: LookupFlags.Uncontained))
            {
                if (HasComp<AntiSSDFreeAreaComponent>(entity))
                    return true;
            }
            return false;
        }

    }
}
