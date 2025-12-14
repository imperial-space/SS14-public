using Content.Server.MedievalDigger.Components;
using Content.Shared.Damage;
using Robust.Shared.Map;
using Content.Shared.Wieldable.Components;
using Content.Shared.Inventory;
using Content.Server.SpikeTrap.Components;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.Imperial.Medieval.GameTicking.Rules;

namespace Content.Server.MedievalDigger
{
    public sealed partial class MedievalDiggerSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedievalDiggAbleComponent, DamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MedievalDiggerInstrumentComponent, MeleeHitEvent>(OnAttack);
        }

        public void OnAttack(EntityUid uid, MedievalDiggerInstrumentComponent component, MeleeHitEvent args)
        {
            if (TryComp<AffectRoundStatsComponent>(args.User, out var player))
            {
                player.Diggs++;
                foreach (var barrier in EntityManager.EntityQuery<RoundStatCounterRuleComponent>())
                {
                    barrier.TotalDiggs++;
                }
            }
        }
        private void OnDamage(EntityUid uid, MedievalDiggAbleComponent component, DamageChangedEvent args)
        {
            var xform = Transform(uid);
            var coords = xform.Coordinates;

            if (CheckDiggersNearby(coords) && TryComp<DamageableComponent>(uid, out var damageable) && damageable.TotalDamage < 400f && !component.Digged)
            {
                component.Digged = true;
                _damageableSystem.TryChangeDamage(uid, component.Damage, true);
            }
        }

        public bool CheckDiggersNearby(EntityCoordinates coords)
        {
            foreach (var entity in _lookup.GetEntitiesInRange(coords, 2.5f))
            {
                if (TryComp<MedievalDiggerComponent>(entity, out var digger))
                {
                    foreach (var pickaxe in _inventory.GetHandOrInventoryEntities(entity, SlotFlags.NONE))
                    {
                        if (HasComp<MedievalDiggerInstrumentComponent>(pickaxe) && TryComp<WieldableComponent>(pickaxe, out var wield) && wield.Wielded)
                        {
                            return true;
                        }
                    }

                }
            }
            return false;

        }

    }
}
