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
using Microsoft.CodeAnalysis;
using Robust.Server.Audio;
using Robust.Shared.Random;
using Content.Shared.Item;
using Content.Shared.IdentityManagement;
using Robust.Server.GameObjects;
using Content.Server.Imperial.Medieval.RandomSteal;
using Content.Shared.Imperial.Medieval.Additions;

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
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IEntitySystemManager _sys = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomStealComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<RandomStealComponent, StealDoAfterArgs>(OnDoAfterSteal);
    }
    private void OnGetAlternativeVerbs(EntityUid uid, RandomStealComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || !TryComp<InventoryComponent>(ev.Target, out var inventoryComponent))
            return;

        if (ev.User == ev.Target)
            return;

        List<EntityUid> targetEntities = new();

        foreach (var item in comp.Slots)
        {
            if (_inventorySystem.TryGetSlotEntity(ev.Target, item, out var targetEntity, inventoryComponent))
            {
                if (TryComp<StorageComponent>(targetEntity, out var storage) && storage.Container.ContainedEntities.Any())
                {
                    targetEntities.Add(_random.Pick(storage.Container.ContainedEntities.Where(x => comp.Sizes.Contains(CompOrNull<ItemComponent>(x)?.Size ?? "")).ToList()));
                    continue;
                }

                targetEntities.Add(targetEntity.Value);
            }
        }

        if (!targetEntities.Any())
            return;

        ev.Verbs.Add(new AlternativeVerb
        {
            Act = () => TrySteal(ev.User, ev.Target, comp, targetEntities),
            Text = Loc.GetString("stealActionSpellward")
        });
    }
    private void TrySteal(EntityUid first, EntityUid second, RandomStealComponent comp, List<EntityUid> entities)
    {
        if (!_sys.GetEntitySystem<AntiStealAfkSystem>().TryStrip(first, second))
            return;
        if (TryComp<StealChanceIncreaserComponent>(first, out var increaser))
            comp.Chance += increaser.Bonus;

        if (TryComp<StealRaceChanceIncreaserComponent>(first, out var raceIncreaser))
            comp.Chance += raceIncreaser.Bonus;

        var ev = new TryGetAdditionalStealTargetEvent();
        RaiseLocalEvent(first, ref ev);

        _random.Shuffle(entities);

        var doAfterSteal = new DoAfterArgs(EntityManager, first, comp.TimeNeed, new StealDoAfterArgs(entities.Take(ev.Success ? 2 : 1).ToList()), target: second, eventTarget: first)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            Hidden = true,
            CancelDuplicate = true
        };
        _doAfterSystem.TryStartDoAfter(doAfterSteal);
    }

    private void OnDoAfterSteal(EntityUid uid, RandomStealComponent comp, StealDoAfterArgs ev)
    {
        if (ev.Cancelled)
            return;

        if (ev.Target is not { Valid: true } target)
            return;
        if (!_sys.GetEntitySystem<AntiStealAfkSystem>().TryStrip(uid, target))
            return;

        var nameStealer = Identity.Name(uid, EntityManager);
        var nameFrom = Identity.Name(target, EntityManager);

        var modEv = new GetStealChanceModifiersEvent(1f);
        RaiseLocalEvent(ev.User, ref modEv);

        if (_random.Next(100) > comp.Chance * modEv.Modifier)
        {
            _popupSystem.PopupEntity(Loc.GetString("stealFailedSpellward", ("entity1", nameStealer)), uid, target, Popups.PopupType.LargeCaution);
            _popupSystem.PopupEntity(Loc.GetString("stealFailedSpellwardUser"), target, uid, Popups.PopupType.LargeCaution);

            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(target):user} was trying to steal from {ToPrettyString(uid):target}.");
            _audio.PlayPvs(comp.FailedSound, ev.User, comp.FailedSound.Params);
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("stealSuccessSpellward", ("entity1", nameFrom)), uid, uid);

        foreach (var item in ev.Entities)
        {
            _transform.SetCoordinates(item, Transform(uid).Coordinates);
            _hands.TryForcePickupAnyHand(uid, item);
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(target):user} steal from {ToPrettyString(uid):target} item: {ToPrettyString(item):item}.");
        }
    }
}
