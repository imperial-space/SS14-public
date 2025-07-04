using Content.Shared.Imperial.DarkMage.Components;
using Robust.Shared.Timing;
using Robust.Server.GameObjects;
using Content.Shared.Mobs.Components;
using System.Linq;
using Content.Shared.Mobs;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Containers;
using Robust.Server.Containers;
using Content.Server.NPC.HTN;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Server.Imperial.DarkMage.Follower;
using Content.Client.Imperial.Medieval.DarkMage;
using Content.Shared.Humanoid;

namespace Content.Server.Imperial.DarkMage.Systems;

public sealed class DarkMageSystem : EntitySystem
{
    private EntityUid? _lastClosest;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    private void ForceDie(DarkMageComponent darkMageComponent)
    {
        darkMageComponent.IsDied = true;
        if (darkMageComponent.Target == null || darkMageComponent.Container == null) return;

        _mindSystem.TransferTo(darkMageComponent.Mind, darkMageComponent.Target);

        _container.RemoveEntity(darkMageComponent.Target.Value, darkMageComponent.Object);
        _container.Remove(darkMageComponent.Target.Value, darkMageComponent.Container);

        _npcFactionSystem.ClearFactions(darkMageComponent.Target.Value);
        _npcFactionSystem.AddFactions(darkMageComponent.Target.Value, darkMageComponent.Faction);
        return;
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<DarkMageComponent>();

        // Monkey sees code
        // Neuron activated
        // Monkey copy code

        while (query.MoveNext(out var uid, out var darkMageComponent))
        {
            if (darkMageComponent.IsFirst)
            {
                darkMageComponent.Flame = Spawn(darkMageComponent.PrototypeFlame);
                darkMageComponent.LastTiming = _gameTiming.CurTime;
                darkMageComponent.IsFirst = false;
            }
            if (darkMageComponent.IsDied) continue;

            if (!TryComp<MobStateComponent>(uid, out var mbst) || mbst.CurrentState != MobState.Alive)
            {
                ForceDie(darkMageComponent);
                continue;
            }

            if (darkMageComponent.IsMoved)
            {
                if (darkMageComponent.LastTiming == TimeSpan.Zero) darkMageComponent.LastTiming = _gameTiming.CurTime;
                if (darkMageComponent.Target == null || darkMageComponent.Container == null) continue;
                if (darkMageComponent.LastTiming + darkMageComponent.TimeToStop > _gameTiming.CurTime) continue;
                _mindSystem.TransferTo(darkMageComponent.Mind, darkMageComponent.Target);

                _container.RemoveEntity(darkMageComponent.Target.Value, darkMageComponent.Object);
                _container.Remove(darkMageComponent.Target.Value, darkMageComponent.Container);

                _npcFactionSystem.ClearFactions(darkMageComponent.Target.Value);
                _npcFactionSystem.AddFactions(darkMageComponent.Target.Value, darkMageComponent.Faction);
                darkMageComponent.Target = null;
                darkMageComponent.IsMoved = false;
                darkMageComponent.IsCaptured = false;
                darkMageComponent.IsFirst = true;
            }

            if (darkMageComponent.IsCaptured && darkMageComponent.Target != null && !darkMageComponent.IsMoved && TryComp<MindContainerComponent>(darkMageComponent.Target, out var mindContainerComponent) && mindContainerComponent.Mind != null)
            {
                var target = darkMageComponent.Target.Value;
                var oobject = Spawn(darkMageComponent.Prototype);

                var mind = mindContainerComponent.Mind.Value;
                var internalContainer = _container.EnsureContainer<ContainerSlot>(target, darkMageComponent.ContainerId);

                if (_container.Insert(oobject, internalContainer))
                    _mindSystem.TransferTo(mind, oobject);

                darkMageComponent.Mind = mind;
                darkMageComponent.Object = oobject;
                darkMageComponent.Container = internalContainer;
                darkMageComponent.IsMoved = true;
                darkMageComponent.LastTiming = TimeSpan.Zero;

                var component = EnsureComp<HTNComponent>(target);
                component.RootTask = new HTNCompoundTask()
                {
                    Task = "SimpleHumanoidHostileCompound"
                };
                var factioncomp = EnsureComp<NpcFactionMemberComponent>(target);

                _npcFactionSystem.ClearFactions(target);
                _npcFactionSystem.AddFaction(target, "Syndicate");
                continue;
            }

            var position = _transform.GetMapCoordinates(uid);
            var entitiesNearby = _lookup.GetEntitiesInRange(position, darkMageComponent.SearchRadius)
                .Where(e => e != uid && TryComp<MindContainerComponent>(e, out var mindE) && mindE.HasMind && TryComp<MobStateComponent>(e, out var eComp) && eComp.CurrentState == MobState.Alive && HasComp<HumanoidAppearanceComponent>(e)) // Много
                .ToList();

            if (entitiesNearby.Count >= 2 && !darkMageComponent.IsMoved)
            {
                if (HasComp<HTNComponent>(uid))
                    RemComp<HTNComponent>(uid);
                var closest = entitiesNearby
                    .OrderBy(e => (_transform.GetMapCoordinates(e).Position - position.Position).LengthSquared()) // Квадрат расстояния
                    .FirstOrDefault();

                darkMageComponent.Target = closest;

                EnsureComp<MedievalFollowerComponent>(darkMageComponent.Flame).Target = closest;

                if (_lastClosest != darkMageComponent.Target && _lastClosest != null)
                {
                    if (HasComp<DarkMageAddOverlayComponent>(_lastClosest.Value)) RemComp<DarkMageAddOverlayComponent>(_lastClosest.Value);
                    _lastClosest = darkMageComponent.Target;
                }

                EnsureComp<DarkMageAddOverlayComponent>(darkMageComponent.Target.Value);
                if (_gameTiming.CurTime > darkMageComponent.LastTiming + darkMageComponent.Timing) // 9 секунд по умолчанию
                {
                    if (darkMageComponent.Target != null && HasComp<DarkMageAddOverlayComponent>(darkMageComponent.Target.Value)) RemComp<DarkMageAddOverlayComponent>(darkMageComponent.Target.Value);
                    QueueDel(darkMageComponent.Flame);
                    darkMageComponent.IsCaptured = true;
                }
            }
            else
            {
                if (darkMageComponent.Target != null && HasComp<DarkMageAddOverlayComponent>(darkMageComponent.Target.Value)) RemComp<DarkMageAddOverlayComponent>(darkMageComponent.Target.Value);
                darkMageComponent.LastTiming = _gameTiming.CurTime;
                if (darkMageComponent.IsCaptured || darkMageComponent.IsDied) continue;
                var comp = EnsureComp<HTNComponent>(uid);
                comp.RootTask = new HTNCompoundTask()
                {
                    Task = "SimpleRangedHostileCompound"
                };
                EnsureComp<MedievalFollowerComponent>(darkMageComponent.Flame).Target = uid;
            }
        }
    }
}
