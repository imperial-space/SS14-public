using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.CustomChaplain.Components;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.Server.Imperial.CustomChaplain.Systems;

/// <summary>
/// System for handling Bible recall functionality.
/// </summary>
public sealed class ImperialBibleRecallSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialBibleRecallActionEvent>(OnRecallAction);
    }

    private void OnRecallAction(ImperialBibleRecallActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;

        // Find the bound bible for this performer
        EntityUid bible = default;
        ImperialBibleComponent? bibleComp = null;
        var enumerator = EntityQueryEnumerator<ImperialBibleComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.IsBound && comp.Owner == performer)
            {
                bible = uid;
                bibleComp = comp;
                break;
            }
        }

        if (bibleComp == null)
        {
            _popupSystem.PopupEntity(Loc.GetString("bible-recall-not-bound"), performer, performer);
            return;
        }

        // Check if the user has hands
        if (!TryComp<HandsComponent>(performer, out var hands))
            return;

        // Try to pick up the bible
        if (_hands.TryForcePickupAnyHand(performer, bible))
        {
            _popupSystem.PopupEntity(Loc.GetString("bible-recall-success"), bible, performer);
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("bible-recall-hands-full"), bible, performer);
        }
    }
}
