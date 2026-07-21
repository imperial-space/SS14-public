using System.Linq;
using Content.Shared._CP14.Workbench;
using Content.Shared.Destructible;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Inventory.Events;
using Content.Shared.Hands;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Events;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Achievements;

public sealed partial class AchievementSystem
{
    private void InitializeConditions()
    {
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<StackComponent, StackCountChangedEvent>(OnStackCountChanged);
        SubscribeLocalEvent<StorageComponent, StorageItemInsertedEvent>(OnStorageItemInserted);
        SubscribeLocalEvent<StorageComponent, StorageItemRemovedEvent>(OnStorageItemRemoved);
        SubscribeLocalEvent<AchievementOwnerComponent, DidEquipEvent>(OnInventoryEquipped);
        SubscribeLocalEvent<AchievementOwnerComponent, DidEquipHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<AchievementOwnerComponent, DidUnequipHandEvent>(OnHandUnequipped);
        SubscribeLocalEvent<AchievementTargetComponent, MobStateChangedEvent>(OnTargetDied);
        SubscribeLocalEvent<AchievementOwnerComponent, MobStateChangedEvent>(OnOwnerDied);
        SubscribeLocalEvent<AchievementTargetComponent, BreakageEventArgs>(OnStructureBroken);
        SubscribeLocalEvent<AchievementTargetComponent, DestructionEventArgs>(OnStructureDestroyed);
        SubscribeLocalEvent<AchievementOwnerComponent, CP14WorkbenchCraftedEvent>(OnPlayerCrafted);
        SubscribeLocalEvent<AchievementLocationComponent, StartCollideEvent>(OnCollide);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        TryUpdateProgressAndGrant(ev.Entity, ev.Entity,
            ach => ach.Conditions.Any(c => c is BecomeEntityCondition));
    }

    private void OnPlayerCrafted(Entity<AchievementOwnerComponent> ent, ref CP14WorkbenchCraftedEvent args)
    {
        TryUpdateProgressAndGrant(ent.Owner, args,
            ach => ach.Conditions.Any(c => c is CraftItemCondition));
    }

    private void OnHandEquipped(EntityUid uid, AchievementOwnerComponent component, DidEquipHandEvent args)
    {
        UpdatePickupProgress(uid);
    }

    private void OnHandUnequipped(EntityUid uid, AchievementOwnerComponent component, DidUnequipHandEvent args)
    {
        UpdatePickupProgress(uid);
    }

    private void OnStorageItemInserted(EntityUid uid, StorageComponent component, ref StorageItemInsertedEvent args)
    {
        if (FindOwnerPlayer(uid) is not { } player)
            return;

        UpdatePickupProgress(player);
    }

    private void OnStorageItemRemoved(EntityUid uid, StorageComponent component, ref StorageItemRemovedEvent args)
    {
        if (FindOwnerPlayer(uid) is not { } player)
            return;

        UpdatePickupProgress(player);
    }

    private void OnStackCountChanged(EntityUid uid, StackComponent component, StackCountChangedEvent args)
    {
        if (args.NewCount == args.OldCount)
            return;

        if (FindOwnerPlayer(uid) is not { } player)
            return;

        UpdatePickupProgress(player);
    }

    private void OnInventoryEquipped(EntityUid uid, AchievementOwnerComponent component, DidEquipEvent args)
    {
        TryUpdateProgressAndGrant(uid, args,
            ach => ach.Conditions.Any(c => c is EquipSetCondition));
    }

    private void OnTargetDied(EntityUid targetUid, AchievementTargetComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var killer = GetPlayerFromOrigin(args.Origin);
        if (killer == null || !HasComp<AchievementOwnerComponent>(killer.Value))
            return;

        TryUpdateProgressAndGrant(killer.Value, targetUid,
            ach => ach.Conditions.Any(c => c is KillMobCondition));
    }

    private void OnOwnerDied(EntityUid victimUid, AchievementOwnerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var killer = GetPlayerFromOrigin(args.Origin);
        if (killer == null || !HasComp<AchievementOwnerComponent>(killer.Value))
            return;

        if (killer.Value == victimUid)
            return;

        TryUpdateProgressAndGrant(killer.Value, victimUid,
            ach => ach.Conditions.Any(c => c is KillFactionPlayerCondition));
    }

    private void OnCollide(EntityUid uid, AchievementLocationComponent component, ref StartCollideEvent args)
    {
        var player = args.OtherEntity;

        if (!HasComp<AchievementOwnerComponent>(player) || GetPlayerGuid(player) == null)
            return;

        TryUpdateProgressAndGrant(player, component.LocationId,
            ach => ach.Conditions.Any(c => c is LocationCondition));
    }

    private void OnStructureBroken(Entity<AchievementTargetComponent> ent, ref BreakageEventArgs args)
    {
        if (args.Performer == null)
            return;

        var performer = GetPlayerFromOrigin(args.Performer.Value);
        if (performer == null)
            return;

        TryUpdateProgressAndGrant(performer.Value, ent.Owner,
            ach => ach.Conditions.Any(c => c is BreakStructureCondition));
    }

    private void OnStructureDestroyed(Entity<AchievementTargetComponent> ent, ref DestructionEventArgs args)
    {
        if (args.Performer == null)
            return;

        var performer = GetPlayerFromOrigin(args.Performer.Value);
        if (performer == null)
            return;

        TryUpdateProgressAndGrant(performer.Value, ent.Owner,
            ach => ach.Conditions.Any(c => c is DestroyStructureCondition));
    }

    private void UpdatePickupProgress(EntityUid player)
    {
        if (GetPlayerGuid(player) is not { } guid)
            return;

        if (!_playerAchievements.TryGetValue(guid, out var unlocked))
            return;

        var itemCounts = CountInventoryItems(player);

        foreach (var ach in _protoManager.EnumeratePrototypes<AchievementPrototype>())
        {
            if (unlocked.Contains(ach.ID))
                continue;

            if (!ach.Conditions.Any(c => c is CollectAllItemsCondition or CollectAnyItemsCondition))
                continue;

            if (!_roundProgression.TryGetValue(guid, out var playerProg))
            {
                playerProg = new Dictionary<string, Dictionary<string, int>>();
                _roundProgression[guid] = playerProg;
            }

            if (!playerProg.TryGetValue(ach.ID, out var achievementProg))
            {
                achievementProg = new Dictionary<string, int>();
                playerProg[ach.ID] = achievementProg;
            }

            foreach (var condition in ach.Conditions)
            {
                switch (condition)
                {
                    case CollectAllItemsCondition collectAll:
                    {
                        foreach (var (proto, _) in collectAll.ItemPrototypes)
                        {
                            var key = $"{collectAll.ProgressKey}:{proto}";
                            var count = itemCounts.GetValueOrDefault(proto, 0);
                            achievementProg[key] = Math.Max(achievementProg.GetValueOrDefault(key, 0), count);
                        }
                        break;
                    }

                    case CollectAnyItemsCondition collectAny:
                    {
                        var total = collectAny.ItemPrototypes.Sum(p => itemCounts.GetValueOrDefault(p, 0));
                        achievementProg[collectAny.ProgressKey] = Math.Max(
                            achievementProg.GetValueOrDefault(collectAny.ProgressKey, 0), total);
                        break;
                    }
                }
            }

            TryGrantAchievement(player, ach.ID);
        }
    }

    private Dictionary<string, int> CountInventoryItems(EntityUid player)
    {
        var counts = new Dictionary<string, int>();
        var visited = new HashSet<EntityUid>();
        CountItemsRecursive(player, counts, visited);
        return counts;
    }

    private void CountItemsRecursive(EntityUid uid, Dictionary<string, int> counts, HashSet<EntityUid> visited)
    {
        if (!visited.Add(uid))
            return;

        if (!EntityManager.TryGetComponent<ContainerManagerComponent>(uid, out var containerManager))
            return;

        foreach (var container in containerManager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                var meta = EntityManager.GetComponent<MetaDataComponent>(item);
                if (meta.EntityPrototype != null)
                {
                    var protoId = meta.EntityPrototype.ID;
                    var count = EntityManager.TryGetComponent<StackComponent>(item, out var stack)
                        ? stack.Count
                        : 1;
                    counts[protoId] = counts.GetValueOrDefault(protoId, 0) + count;
                }

                CountItemsRecursive(item, counts, visited);
            }
        }
    }

    private int GetItemCount(EntityUid item)
    {
        return EntityManager.TryGetComponent<StackComponent>(item, out var stack)
            ? stack.Count
            : 1;
    }

    private EntityUid? FindOwnerPlayer(EntityUid uid)
    {
        var current = uid;
        while (_containerSystem.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;
            if (HasComp<AchievementOwnerComponent>(current))
                return current;
        }

        if (HasComp<AchievementOwnerComponent>(uid))
            return uid;

        return null;
    }

    public EntityUid? GetPlayerFromOrigin(EntityUid? origin)
    {
        if (origin == null)
            return null;

        if (HasComp<AchievementOwnerComponent>(origin.Value))
            return origin.Value;

        var parent = Transform(origin.Value).ParentUid;
        if (parent.IsValid() && HasComp<AchievementOwnerComponent>(parent))
            return parent;

        if (TryComp<ProjectileComponent>(origin.Value, out var projectile) && projectile.Shooter.HasValue)
        {
            if (HasComp<AchievementOwnerComponent>(projectile.Shooter.Value))
                return projectile.Shooter.Value;
        }

        return null;
    }

    private static bool ArePrerequisitesMet(AchievementPrototype prototype, HashSet<string> unlocked)
    {
        foreach (var prereq in prototype.Prerequisites)
        {
            if (!unlocked.Contains(prereq.Id))
                return false;
        }

        return true;
    }
}
