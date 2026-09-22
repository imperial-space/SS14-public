using Content.Server.Traits.Assorted;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Server.Damage.Systems;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Content.Shared.Damage.Components;
using Content.Shared.Jittering;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMaskOfMadnessSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem    _lookup    = default!;
    [Dependency] private readonly StaminaSystem         _stamina   = default!;
    [Dependency] private readonly ParacusiaSystem       _paracusia = default!;
    [Dependency] private readonly PopupSystem           _popup     = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem       _inventory = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter    = default!;
    [Dependency] private readonly IRobustRandom         _random    = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMaskOfMadnessComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<HereticMaskOfMadnessComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticMaskOfMadnessComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticMaskOfMadnessComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, HereticMaskOfMadnessComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null) return;
        if (!HasComp<HereticComponent>(args.User)) return;
        if (args.Target == args.User) return;
        if (!HasComp<MobStateComponent>(args.Target.Value)) return;

        _inventory.TryEquip(args.User, args.Target.Value, uid, "mask", force: true);
        args.Handled = true;
    }

    private void OnEquipped(EntityUid uid, HereticMaskOfMadnessComponent comp, GotEquippedEvent args)
    {
        if (!HasComp<HereticComponent>(args.EquipTarget))
            comp.Locked = true;
    }

    private void OnUnequipped(EntityUid uid, HereticMaskOfMadnessComponent comp, GotUnequippedEvent args)
    {
        comp.Locked = false;
    }

    private void OnUnequipAttempt(EntityUid uid, HereticMaskOfMadnessComponent comp, BeingUnequippedAttemptEvent args)
    {
        if (!comp.Locked) return;
        if (HasComp<HereticComponent>(args.UnEquipTarget)) return;

        args.Cancel();
        if (args.User == args.UnEquipTarget)
            _popup.PopupEntity(Loc.GetString("heretic-mask-of-madness-cant-remove"), args.UnEquipTarget, args.UnEquipTarget, PopupType.SmallCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticMaskOfMadnessComponent>();
        while (query.MoveNext(out var uid, out var mask))
        {
            mask.TickAccumulator += frameTime;
            if (mask.TickAccumulator < mask.TickInterval) continue;
            mask.TickAccumulator = 0f;

            if (!_container.TryGetContainingContainer(uid, out var container)) continue;
            var wearer = container.Owner;
            if (!HasComp<MobStateComponent>(wearer)) continue;

            foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(wearer).Coordinates, mask.AoeRadius))
            {
                // Skip heretics — they are immune to the mask's aura
                if (HasComp<HereticComponent>(ent.Owner)) continue;

                if (!TryComp<MobStateComponent>(ent.Owner, out var ms)) continue;
                if (ms.CurrentState == MobState.Dead) continue;

                // 60%: auditory hallucinations
                if (_random.Prob(0.6f))
                {
                    var paracusia = EnsureComp<ParacusiaComponent>(ent.Owner);
                    _paracusia.SetSounds(ent.Owner, new SoundCollectionSpecifier("Paracusia"), paracusia);
                    _paracusia.SetTime(ent.Owner, 3f, 10f, paracusia);
                    _paracusia.SetDistance(ent.Owner, 5f);
                }

                // 40%: jitter (10s)
                if (_random.Prob(0.4f))
                    _jitter.DoJitter(ent.Owner, TimeSpan.FromSeconds(10), true);

                // 30%: stamina damage, only while below soft threshold
                if (_random.Prob(0.3f)
                    && TryComp<StaminaComponent>(ent.Owner, out var stamina)
                    && stamina.StaminaDamage <= 85f)
                {
                    _stamina.TakeStaminaDamage(ent.Owner, mask.StaminaDamagePerTick, visual: true);
                }
            }
        }
    }
}
