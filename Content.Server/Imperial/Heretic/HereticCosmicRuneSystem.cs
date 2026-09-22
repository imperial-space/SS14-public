using Content.Server.Popups;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCosmicRuneSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem           _popup  = default!;
    [Dependency] private readonly SharedAudioSystem     _audio  = default!;
    [Dependency] private readonly SharedTransformSystem _xform  = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticCosmicRuneActionEvent>(OnCosmicRuneAction);
        SubscribeLocalEvent<HereticCosmicRuneComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnCosmicRuneAction(EntityUid uid, HereticComponent comp, HereticCosmicRuneActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var existingRunes = new List<(EntityUid Uid, HereticCosmicRuneComponent Comp)>();
        var query = EntityQueryEnumerator<HereticCosmicRuneComponent>();
        while (query.MoveNext(out var runeUid, out var runeComp))
        {
            if (runeComp.HereticUid == uid)
                existingRunes.Add((runeUid, runeComp));
        }

        if (existingRunes.Count >= 2)
        {
            QueueDel(existingRunes[0].Uid);
            existingRunes.RemoveAt(0);
        }

        var rune = Spawn("HereticCosmicRune", args.Target);
        var newComp = EnsureComp<HereticCosmicRuneComponent>(rune);
        newComp.HereticUid = uid;

        if (existingRunes.Count == 1)
        {
            var (partnerUid, partnerComp) = existingRunes[0];
            newComp.LinkedRune = partnerUid;
            partnerComp.LinkedRune = rune;
            Dirty(partnerUid, partnerComp);
        }

        Dirty(rune, newComp);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-cosmic-rune-placed"), uid, uid, PopupType.Medium);
    }

    private void OnInteractHand(Entity<HereticCosmicRuneComponent> ent, ref InteractHandEvent args)
    {
        if (args.User != ent.Comp.HereticUid)
            return;

        args.Handled = true;

        if (ent.Comp.LinkedRune == null || !Exists(ent.Comp.LinkedRune.Value))
        {
            _popup.PopupEntity(Loc.GetString("heretic-cosmic-rune-no-link"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var destination = Transform(ent.Comp.LinkedRune.Value).Coordinates;

        _xform.SetCoordinates(args.User, destination);

        if (TryComp<PullerComponent>(args.User, out var puller) && puller.Pulling.HasValue)
            _xform.SetCoordinates(puller.Pulling.Value, destination);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), args.User);

        _popup.PopupEntity(Loc.GetString("heretic-cosmic-rune-triggered"), args.User, args.User, PopupType.Medium);
    }
}
