using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Shared.Clothing;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.VoiceMask;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Heretic.Items;

public sealed class HereticShiftingGuiseSystem : EntitySystem
{
    [Dependency] private readonly IdentitySystem _identity = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticShiftingGuiseComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticShiftingGuiseComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticShiftingGuiseComponent, InventoryRelayedEvent<SeeIdentityAttemptEvent>>(OnSeeIdentity);
    }

    private void OnEquipped(Entity<HereticShiftingGuiseComponent> ent, ref ClothingGotEquippedEvent args)
    {
        var footstep = EnsureComp<FootstepModifierComponent>(args.Wearer);
        footstep.FootstepSoundCollection = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg", AudioParams.Default.WithVolume(-100f));
        Dirty(args.Wearer, footstep);
        _identity.QueueIdentityUpdate(args.Wearer);
    }

    private void OnUnequipped(Entity<HereticShiftingGuiseComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemComp<FootstepModifierComponent>(args.Wearer);
        _identity.QueueIdentityUpdate(args.Wearer);
    }

    private void OnSeeIdentity(Entity<HereticShiftingGuiseComponent> ent, ref InventoryRelayedEvent<SeeIdentityAttemptEvent> args)
    {
        if (!TryComp<VoiceMaskComponent>(ent, out var voiceMask))
            return;
        if (!voiceMask.Active || !voiceMask.OverrideIdentity)
            return;
        if (voiceMask.VoiceMaskName is not { } name)
            return;
        args.Args.NameOverride = name;
    }
}
