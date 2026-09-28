using Content.Server.Popups;
using Content.Shared.Charges.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticLabyrinthHandbookSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem   _audio   = default!;
    [Dependency] private readonly PopupSystem         _popup   = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticLabyrinthHandbookComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<HereticLabyrinthHandbookComponent, GotEquippedHandEvent>(OnPickup);
    }

    private void OnPickup(Entity<HereticLabyrinthHandbookComponent> ent, ref GotEquippedHandEvent args)
    {
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_items_handling_book_pickup.ogg"), args.User);
    }

    private void OnInteract(Entity<HereticLabyrinthHandbookComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled) return;
        if (args.Target != null) return;

        if (!_charges.TryUseCharge(ent.Owner))
            return;

        args.Handled = true;

        Spawn("HereticLabyrinthBarrier", args.ClickLocation);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_smoke.ogg"), args.User);
        _popup.PopupEntity(Loc.GetString("heretic-labyrinth-handbook-placed"), args.User, args.User, PopupType.Medium);
    }
}
