using Content.Shared.Actions;
using Content.Shared.Bed;
using Content.Shared.Bed.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs.Systems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Utility;
using Content.Server.Armor.Components;

namespace Content.Server.Bed
{
    public sealed class BedSystem : SharedBedSystem
    {
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

        private EntityQuery<SleepingComponent> _sleepingQuery;

        public override void Initialize()
        {
            SubscribeLocalEvent<HealOnBuckleComponent, StrappedEvent>(OnStrapped);
            base.Initialize();

            _sleepingQuery = GetEntityQuery<SleepingComponent>();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<HealOnBuckleHealingComponent, HealOnBuckleComponent, StrapComponent>();
            while (query.MoveNext(out var uid, out _, out var bedComponent, out var strapComponent))
            {
                if (Timing.CurTime < bedComponent.NextHealTime)
                    continue;

                bedComponent.NextHealTime += TimeSpan.FromSeconds(bedComponent.HealTime);

                if (strapComponent.BuckledEntities.Count == 0)
                    continue;

                foreach (var healedEntity in strapComponent.BuckledEntities)
                {
                    if (_mobStateSystem.IsDead(healedEntity))
                        continue;

                    var damage = bedComponent.Damage;

                    if (_sleepingQuery.HasComp(healedEntity))
                        damage *= bedComponent.SleepMultiplier;

                    _damageableSystem.TryChangeDamage(healedEntity, damage, true, origin: uid);
                }
            }
        }
        // imperial medieval sleep start
        private void OnStrapped(Entity<HealOnBuckleComponent> bed, ref StrappedEvent args)
        {
            if (!Timing.IsFirstTimePredicted)
                return;
            if (HasComp<HealOnBuckleHealingComponent>(args.Buckle))
                return;
            EnsureComp<HealOnBuckleHealingComponent>(bed);
            if (_inventorySystem.TryGetSlotEntity(args.Buckle.Owner, "outerClothing", out var existingOutfit) && !HasComp<AllowArmorSleepComponent>(existingOutfit.Value))
            {
                var meta = EntityManager.GetComponent<MetaDataComponent>(existingOutfit.Value);
                _popup.PopupEntity(Loc.GetString("Как неудобно спать в " + meta.EntityName), args.Buckle.Owner);
                return;
            }

            if (_inventorySystem.TryGetSlotEntity(args.Buckle.Owner, "head", out var existingHead) && !HasComp<AllowArmorSleepComponent>(existingHead.Value))
            {
                var meta = EntityManager.GetComponent<MetaDataComponent>(existingHead.Value);
                _popup.PopupEntity(Loc.GetString("Как неудобно спать в " + meta.EntityName), args.Buckle.Owner);
                return;
            }
            bed.Comp.NextHealTime = Timing.CurTime + TimeSpan.FromSeconds(bed.Comp.HealTime);
            _actionsSystem.AddAction(args.Buckle, ref bed.Comp.SleepAction, SleepingSystem.SleepActionId, bed);
            if (TryComp<SkillsComponent>(args.Buckle, out var skills))
                _actionsSystem.SetCooldown(bed.Comp.SleepAction, TimeSpan.FromSeconds(10 - (skills.Levels[SharedSkillsSystem.VitalityId]-10) * 0.5));
            Dirty(bed);

            // Single action entity, cannot strap multiple entities to the same bed.
            //DebugTools.AssertEqual(args.Strap.Comp.BuckledEntities.Count, 1)
        }
        // imperial medieval sleep end
    }
}
