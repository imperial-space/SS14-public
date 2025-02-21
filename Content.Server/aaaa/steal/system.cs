#pragma warning disable CS0618
#pragma warning disable CS4014
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
using Robust.Server.GameObjects;
using Microsoft.CodeAnalysis;
using FastAccessors;

namespace Content.Shared.Imperial.RandomSteal.Systems;

public sealed partial class RandomStealEvents : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomStealComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<RandomStealComponent, StealDoAfterArgs>(OnDoAfterSteal);
    }
    private void OnGetAlternativeVerbs(EntityUid uid, RandomStealComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || !TryComp<InventoryComponent>(ev.Target, out var inventoryComponent)) return;

        var check = _inventorySystem.TryGetSlotEntity(ev.Target, "pocket1", out var slot1, inventoryComponent);
        var check1 = _inventorySystem.TryGetSlotEntity(ev.Target, "pocket2", out var slot2, inventoryComponent);
        _inventorySystem.TryGetSlotEntity(ev.Target, "back", out var back, inventoryComponent);
        var check2 = TryComp<StorageComponent>(back, out var storageComponent) && storageComponent.Container.ContainedEntities.Any();
        if (!check && !check1 && !check2) return;
        // if (!TryComp(uid, out MetaDataComponent? metaDataComponent) || metaDataComponent.EntityPrototype is null || !comp.ListProto.Contains(metaDataComponent.EntityPrototype.ToString())) return; // IT'S CURSED HELP PLS!!!
        ev.Verbs.Add(new AlternativeVerb
        {
            Act = () =>
            {
                TrySteal(ev.User, ev.Target, comp, slot1, slot2, back);
            },
            Text = "Украсть что-либо"
        });
    }
    private void TrySteal(EntityUid first, EntityUid second, RandomStealComponent comp, EntityUid? pocket1 = null, EntityUid? pocket2 = null, EntityUid? back = null)
    {
        var rnd = new System.Random();

        if (!TryComp(first, out MetaDataComponent? metaDataComponent)) return;
        var nameStealer = metaDataComponent.EntityName;

        if (rnd.Next(1, 100) > comp.Chance)
        {
            _popupSystem.PopupEntity($"{nameStealer} попытался украсть у вас что-то!", first);
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(second):user} was trying to steal from {ToPrettyString(first):target}.");
            return;
        }
        EntityUid?[] slots = { pocket1, pocket2, back };
        var validEntities = slots.Where(e => e != null).ToList();
        var chosen = validEntities[rnd.Next(validEntities.Count - 1)];
        var item = chosen;
        if (chosen == back)
        {
            if (!TryComp<StorageComponent>(back, out var storageComponent)) return;
            item = storageComponent.Container.ContainedEntities[rnd.Next(storageComponent.Container.ContainedEntities.Count - 1)];
        }
        if (item == null) return;
        var doAfterSteal = new DoAfterArgs(EntityManager, first, TimeSpan.FromSeconds(2f), new StealDoAfterArgs(), target: first, eventTarget: second)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };
        _doAfterSystem.WaitDoAfter(doAfterSteal);
        comp.Item = item.Value;
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(second):user} steal from {ToPrettyString(first):target} item: {ToPrettyString(item):item}.");
    }
    private void OnDoAfterSteal(EntityUid uid, RandomStealComponent comp, StealDoAfterArgs ev)
    {
        if (!HasComp<HandsComponent>(uid) || ev.Cancelled) return;
        var entityManager = IoCManager.Resolve<IEntityManager>();

        var xformSystem = entityManager.System<TransformSystem>();

        var mapPosition = xformSystem.GetWorldPosition(uid);

        xformSystem.SetWorldPosition(
            comp.Item,
            mapPosition
        );
        if (ev.Target is null) return;
        _hands.TryForcePickupAnyHand(ev.Target.Value, comp.Item);
    }
}
