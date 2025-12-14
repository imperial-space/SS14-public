using Content.Shared.Inventory;

namespace Content.Shared.Imperial.Medieval.MobRiding
{
    public sealed partial class HorseArmorSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<HorseArmorComponent, InventoryRelayedEvent<GetHorseDamageModifier>>(OnDamageModify);
        }

        public void OnDamageModify(EntityUid uid, HorseArmorComponent comp, ref InventoryRelayedEvent<GetHorseDamageModifier> args)
        {
            args.Args.Modifier *= comp.BluntModifier;
        }

    }
}
