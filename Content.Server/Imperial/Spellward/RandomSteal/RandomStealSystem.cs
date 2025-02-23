using System.Linq;
using Content.Server.Popups;
using Content.Shared.Database;
using Content.Shared.Imperial.RandomSteal.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Content.Server.Administration.Logs;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.RandomSteal.Events;
using Content.Server.DoAfter;
using Content.Server.Hands.Systems;
using Content.Shared.Hands.Components;
using Microsoft.CodeAnalysis;
using Robust.Server.Audio;
using Robust.Shared.Random;
using Content.Shared.Item;

namespace Content.Shared.Imperial.RandomSteal.Systems;

public sealed partial class RandomStealSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomStealComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<RandomStealComponent, StealDoAfterArgs>(OnDoAfterSteal);
    }
    private void OnGetAlternativeVerbs(EntityUid uid, RandomStealComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || !TryComp<InventoryComponent>(ev.Target, out var inventoryComponent)) return;
        if (ev.User == ev.Target) return;
        var check = _inventorySystem.TryGetSlotEntity(ev.Target, comp.Slots[0], out var slot1, inventoryComponent);
        var check1 = _inventorySystem.TryGetSlotEntity(ev.Target, comp.Slots[1], out var slot2, inventoryComponent);
        _inventorySystem.TryGetSlotEntity(ev.Target, comp.Slots[2], out var back, inventoryComponent);
        var check2 = TryComp<StorageComponent>(back, out var storageComponent) && storageComponent.Container.ContainedEntities.Any();
        if (!check && !check1 && !check2) return;
        ev.Verbs.Add(new AlternativeVerb
        {
            Act = () =>
            {
                TrySteal(ev.User, ev.Target, comp, slot1, slot2, back);
            },
            Text = Loc.GetString("stealActionSpellward")
        });
    }
    private void TrySteal(EntityUid first, EntityUid second, RandomStealComponent comp, EntityUid? pocket1 = null, EntityUid? pocket2 = null, EntityUid? back = null)
    {
        EntityUid?[] slots = { pocket1, pocket2, back };
        var validEntities = slots.Where(e => e != null).ToList();
        var chosen = validEntities[_random.Next(validEntities.Count)];
        var item = chosen;
        if (chosen == back)
        {
            if (!TryComp<StorageComponent>(back, out var storageComponent)) return;
            var items = storageComponent.Container.ContainedEntities;
            List<EntityUid> validItems = [];
            validItems = items.Where(e => comp.Sizes.Contains(EnsureComp<ItemComponent>(e).Size)).ToList(); // EnsureComp because HOW IT'S IN BACKPACK WITHOUT ITEMCOMP & except exceptions
            item = validItems[_random.Next(validItems.Count)];
        }
        if (HasComp<StealChanceIncreaserComponent>(first))
            comp.Chance = 60;
        else
            comp.Chance = 40;
        if (item == null) return;
        var doAfterSteal = new DoAfterArgs(EntityManager, first, comp.TimeNeed, new StealDoAfterArgs(), target: first, eventTarget: second)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };
        _doAfterSystem.TryStartDoAfter(doAfterSteal);
        comp.Item = item.Value;
    }
    private void OnDoAfterSteal(EntityUid uid, RandomStealComponent comp, StealDoAfterArgs ev)
    {
        if (!TryComp(ev.Target, out MetaDataComponent? metaDataComponent) || metaDataComponent == null || metaDataComponent.EntityName == null) return;
        var nameStealer = metaDataComponent.EntityName;

        if (!TryComp(uid, out MetaDataComponent? metaDataComponentFrom) || metaDataComponentFrom == null || metaDataComponentFrom.EntityName == null) return;
        var nameFrom = metaDataComponentFrom.EntityName;

        if (!TryComp(comp.Item, out MetaDataComponent? metaDataComponentItem) || metaDataComponentItem == null || metaDataComponentItem.EntityName == null) return;
        var nameItem = metaDataComponentFrom.EntityName;

        if (_random.Next(100) > comp.Chance)
        {
            _popupSystem.PopupEntity(Loc.GetString("stealFailedSpellward", ("entity1", nameStealer), ("entity2", nameFrom)), uid, Popups.PopupType.LargeCaution);
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(ev.Target):user} was trying to steal from {ToPrettyString(uid):target}.");
            _audio.PlayPvs(comp.FailedSound, uid: ev.User, audioParams: comp.Params);
            return;
        }
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(ev.Target):user} steal from {ToPrettyString(uid):target} item: {ToPrettyString(comp.Item):item}.");
        if (!HasComp<HandsComponent>(uid) || ev.Cancelled) return;
        _popupSystem.PopupClient(Loc.GetString("stealSuccessSpellward", ("entity1", nameItem)), ev.Target);
        if (ev.Target == null || comp.Item == null) return;
        _hands.TryForcePickupAnyHand(ev.Target.Value, comp.Item.Value);
    }
}
