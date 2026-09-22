using Content.Shared.Clothing;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.VoiceMask;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Heretic;

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
        footstep.FootstepSoundCollection = new SoundPathSpecifier("/Audio/Effects/thud.ogg", AudioParams.Default.WithVolume(-100f));
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
        if (voiceMask.VoiceMaskName is not {} name)
            return;
        args.Args.NameOverride = name;
    }
}
