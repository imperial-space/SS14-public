using System.Linq;
using Content.Server.Hands.Systems;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.ItemShow;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.ItemShow;

public sealed class ItemDisplaySystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<ItemDisplayRequest>(OnItemShowRequest);
        SubscribeLocalEvent<ItemDisplayComponent, DidUnequipHandEvent>(OnUnequipped);
    }

    private void OnUnequipped(Entity<ItemDisplayComponent> ent, ref DidUnequipHandEvent args)
    {
        if (ent.Comp.ItemUid != args.Unequipped)
        {
            return;
        }

        RemComp<ItemDisplayComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ItemDisplayComponent>();

        while (query.MoveNext(out var uid, out var itemShowComponent))
        {
            if (itemShowComponent.DespawnAt >= _gameTiming.CurTime)
            {
                continue;
            }

            RemComp<ItemDisplayComponent>(uid);
        }
    }

    private void OnItemShowRequest(ItemDisplayRequest msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (player == null)
        {
            Log.Error($"Attempt to show an item failed: no valid player entity found for {args.SenderSession.UserId}.");
            return;
        }

        if (!TryGetEntity(msg.ItemUid, out var itemUid))
        {
            Log.Error($"Attempt to show an item failed: provided entity UID is invalid {msg.ItemUid}.");
            return;
        }

        if (_handsSystem.EnumerateHeld(player.Value).All(x => x != itemUid))
        {
            Log.Error($"Attempt to show an item failed: player does not hold the specified item {itemUid}.");
            return;
        }

        var comp = EnsureComp<ItemDisplayComponent>(player.Value);

        comp.ItemUid = itemUid.Value;
        comp.DespawnAt = _gameTiming.CurTime + comp.DespawnDelay;

        var pvs = Filter.Pvs(player.Value).RemovePlayer(args.SenderSession);

        var playerName = Identity.Entity(player.Value, EntityManager);
        var pointedName = Identity.Entity(comp.ItemUid, EntityManager);

        var loc = Loc.GetString("pointing-system-point-at-other-others", ("otherName", playerName), ("other", pointedName));
        _popupSystem.PopupEntity(loc, player.Value, pvs, false);

        Dirty(player.Value, comp);
    }
}
