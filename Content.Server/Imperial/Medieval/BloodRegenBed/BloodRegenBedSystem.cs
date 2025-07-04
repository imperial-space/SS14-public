using Content.Server.Body.Systems;
using Content.Server.Imperial.Medieval.BloodRegenBed;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;
using Robust.Shared.Collections;

namespace Content.Server.Imperial.Medieval.BloodRegenBed
{
    public sealed class BloodRegenBedSystem : EntitySystem
    {
        [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

        private static readonly TimeSpan RegenInterval = TimeSpan.FromSeconds(5);
        private TimeSpan _nextUpdate = TimeSpan.Zero;
        private readonly HashSet<EntityUid> _activeBeds = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BloodRegenBedComponent, StrappedEvent>(OnStrapped);
            SubscribeLocalEvent<BloodRegenBedComponent, UnstrappedEvent>(OnUnstrapped);
        }

        private void OnStrapped(Entity<BloodRegenBedComponent> bed, ref StrappedEvent args)
        {
            if (TryComp<StrapComponent>(bed.Owner, out var strap) && strap.BuckledEntities.Count > 0)
            {
                _activeBeds.Add(bed.Owner);
            }
        }

        private void OnUnstrapped(Entity<BloodRegenBedComponent> bed, ref UnstrappedEvent args)
        {
            if (TryComp<StrapComponent>(bed.Owner, out var strap) && strap.BuckledEntities.Count == 0)
            {
                _activeBeds.Remove(bed.Owner);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime < _nextUpdate)
                return;

            _nextUpdate = _timing.CurTime + RegenInterval;

            var toRemove = new List<EntityUid>();
            foreach (var bedUid in _activeBeds)
            {
                if (!TryComp<BloodRegenBedComponent>(bedUid, out var bloodRegen) ||
                    !TryComp<StrapComponent>(bedUid, out var strap))
                {
                    toRemove.Add(bedUid);
                    continue;
                }

                if (strap.BuckledEntities.Count == 0)
                {
                    toRemove.Add(bedUid);
                    continue;
                }

                foreach (var buckledEntity in strap.BuckledEntities)
                {
                    if (_mobStateSystem.IsDead(buckledEntity))
                    {
                        continue;
                    }

                    if (_bloodstreamSystem.GetBloodLevelPercentage(buckledEntity) >= 1.0f)
                    {
                        continue;
                    }

                    Entity<SolutionComponent>? solutionEntity = null;
                    if (_solutionContainerSystem.ResolveSolution(buckledEntity, "bloodstream", ref solutionEntity, out var bloodSolution) && bloodSolution != null)
                    {
                        var currentVolume = bloodSolution.Volume;
                        var volumeToAdd = FixedPoint2.New(bloodRegen.BloodRegenMultiplier);
                        _bloodstreamSystem.TryModifyBloodLevel(buckledEntity, volumeToAdd);

                    }
                    else
                    {
                        _bloodstreamSystem.TryModifyBloodLevel(buckledEntity, FixedPoint2.Zero); // Попытка инициализации
                    }
                }
            }

            foreach (var bedUid in toRemove)
            {
                _activeBeds.Remove(bedUid);
            }
        }
    }
}
