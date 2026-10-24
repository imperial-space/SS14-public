using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>Сундук с сокровищами (/obj/structure/mystery_box/fishing).</summary>
public sealed class ImperialMysteryBoxSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    private static readonly TimeSpan Choosing = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Presenting = TimeSpan.FromSeconds(3.5);
    private static readonly TimeSpan Expiring = TimeSpan.FromSeconds(4.5);
    private static readonly TimeSpan Standby = TimeSpan.FromSeconds(2.7);

    private const string OpenSound = "/Audio/Imperial/Fishing/mbox_full.ogg";
    private const string GrantSound = "/Audio/Imperial/Fishing/mbox_end.ogg";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialMysteryBoxComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(Entity<ImperialMysteryBoxComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        switch (ent.Comp.State)
        {
            case ImperialMysteryBoxState.Standby:
                args.Handled = true;
                Activate(ent, args.User);
                break;
            case ImperialMysteryBoxState.Presenting when ent.Comp.Claimable:
                args.Handled = true;
                Grant(ent, args.User);
                break;
        }
    }

    /// <summary>activate(): разум может открыть сундук не больше трёх раз.</summary>
    private void Activate(Entity<ImperialMysteryBoxComponent> ent, EntityUid user)
    {
        if (ent.Comp.Prizes.Count == 0)
            return;

        if (_mind.TryGetMind(user, out var mind, out _))
        {
            var opens = ent.Comp.MindOpens.GetValueOrDefault(mind);
            if (opens >= ent.Comp.MaxOpensPerMind)
            {
                _popup.PopupEntity(Loc.GetString("imperial-mystery-box-refuses"), ent, user, PopupType.SmallCaution);
                return;
            }

            ent.Comp.MindOpens[mind] = opens + 1;
        }

        ent.Comp.UsesLeft--;
        ent.Comp.State = ImperialMysteryBoxState.Choosing;
        ent.Comp.Claimable = false;
        _audio.PlayPvs(OpenSound, ent);

        // loop_icon_changes(): смена картинки всё медленнее.
        var now = _timing.CurTime;
        ent.Comp.Changes.Clear();
        var delay = 0.1;
        var counter = 0.0;
        while (counter + delay + 0.1 < Choosing.TotalSeconds)
        {
            delay += 0.1;
            counter += delay;
            ent.Comp.Changes.Enqueue(now + TimeSpan.FromSeconds(counter));
        }

        ent.Comp.Selected = _random.Pick(ent.Comp.Prizes);
        ent.Comp.NextStep = now + Choosing;
        _appearance.SetData(ent, ImperialMysteryBoxVisuals.Open, true);
        _appearance.SetData(ent, ImperialMysteryBoxVisuals.Prize, ent.Comp.Selected.Value.Id);
        _appearance.SetData(ent, ImperialMysteryBoxVisuals.Claimable, false);
    }

    /// <summary>grant_weapon().</summary>
    private void Grant(Entity<ImperialMysteryBoxComponent> ent, EntityUid user)
    {
        if (ent.Comp.Selected is not { } selected)
            return;

        var prize = Spawn(selected, Transform(ent).Coordinates);
        var name = _proto.Index(selected).Name;
        _popup.PopupEntity(Loc.GetString("imperial-mystery-box-taken", ("user", user), ("item", name)), ent);
        _audio.PlayPvs(GrantSound, ent);
        _hands.TryPickupAnyHand(user, prize);
        Close(ent);
    }

    /// <summary>close_box().</summary>
    private void Close(Entity<ImperialMysteryBoxComponent> ent)
    {
        ent.Comp.State = ImperialMysteryBoxState.CoolingDown;
        ent.Comp.Claimable = false;
        ent.Comp.Selected = null;
        ent.Comp.NextStep = _timing.CurTime + Standby;
        _appearance.SetData(ent, ImperialMysteryBoxVisuals.Open, false);
        _appearance.SetData(ent, ImperialMysteryBoxVisuals.Prize, string.Empty);
        _appearance.SetData(ent, ImperialMysteryBoxVisuals.Claimable, false);
        _audio.PlayPvs("/Audio/Effects/closetclose.ogg", ent);

        if (ent.Comp.UsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mystery-box-breaks"), ent);
            Spawn("MaterialWoodPlank", Transform(ent).Coordinates);
            QueueDel(ent);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialMysteryBoxComponent>();
        while (query.MoveNext(out var uid, out var box))
        {
            var ent = (uid, box);
            switch (box.State)
            {
                case ImperialMysteryBoxState.Choosing:
                    while (box.Changes.Count > 0 && box.Changes.Peek() <= now)
                    {
                        box.Changes.Dequeue();
                        box.Selected = _random.Pick(box.Prizes);
                        _appearance.SetData(uid, ImperialMysteryBoxVisuals.Prize, box.Selected.Value.Id);
                    }

                    if (now < box.NextStep)
                        break;

                    // present_weapon().
                    box.State = ImperialMysteryBoxState.Presenting;
                    box.Claimable = true;
                    box.NextStep = now + Presenting + Expiring;
                    _appearance.SetData(uid, ImperialMysteryBoxVisuals.Claimable, true);
                    if (box.Selected is { } selected)
                        _popup.PopupEntity(Loc.GetString("imperial-mystery-box-presents", ("item", _proto.Index(selected).Name)), uid);
                    break;

                case ImperialMysteryBoxState.Presenting:
                    if (now >= box.NextStep)
                        Close(ent);
                    break;

                case ImperialMysteryBoxState.CoolingDown:
                    if (now >= box.NextStep)
                        box.State = ImperialMysteryBoxState.Standby;
                    break;
            }
        }
    }
}
