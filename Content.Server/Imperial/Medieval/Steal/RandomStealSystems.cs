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
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.Toolshed.TypeParsers;
using Content.Shared.Stacks;
using Content.Server.Stack;

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
    [Dependency] private readonly StackSystem _stackSystem = default!;

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
                    var potentialTargets = storage.Container.ContainedEntities.Where(x => comp.Sizes.Contains(CompOrNull<ItemComponent>(x)?.Size ?? "")).ToList();

                    if (potentialTargets.Any())
                        targetEntities.Add(_random.Pick(potentialTargets));

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

        var nameStealer = Identity.Name(uid, EntityManager); // Имя вора
        var nameFrom = Identity.Name(target, EntityManager); // Имя жертвы

        var victimUid = target;
        var victimIntelligenceContribution = 0f; // Вклад интеллекта жертвы на шанс удачного воровства

        if (TryComp<SkillsComponent>(victimUid, out var victimSkillsComponent))
        {
            if (victimSkillsComponent.Levels.TryGetValue(SharedSkillsSystem.IntelligenceId, out var levelIntelligence))
            {
                if (levelIntelligence < 3) victimIntelligenceContribution = 0.1f; // Если интеллект жертвы меньше 3, то шанс успешно обворовать увеличивается на 10%
                else if (levelIntelligence < 12) victimIntelligenceContribution = 0; // Если интеллект жертвы от 3 до 12, то шанс успешно обворовать не изменяется
                else if (levelIntelligence < 15) victimIntelligenceContribution = -0.1f; // Если интеллект жертвы больше 12, то шанс успешно обворовать уменьшается на 10%
            }
        }

        var modEv = new GetStealChanceModifiersEvent(1f);
        RaiseLocalEvent(ev.User, ref modEv);
        var stealChance = (comp.Chance / 100) * modEv.Modifier;
        // Дополнительные проверки
        if (stealChance >= 0.85) stealChance = 0.85f;
        // Добавление влияния интеллекта жертвы
        stealChance += victimIntelligenceContribution;

        if (stealChance < 0) stealChance = 0;

        // _random.Next(100) изменено на _random.Prob, поскольку это математически более верный способ высчитывания случайностей.
        if (_random.Prob(stealChance))
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
            if (TryComp<StackComponent>(item, out var itemStackComp))
            {
                int maximalAmountToSteal = (int)Math.Round(itemStackComp.Count * 0.7);
                // Так как _random.Next(a) возвращает int От 0 до a, мы вынуждены сделать смещение на 1,
                // чтобы при удачной попытке воровства мы своровали хотя бы 1 штуку стакабельного предмета.
                int amountToSteal = _random.Next(maximalAmountToSteal - 1) + 1;
                var stolenItems = _stackSystem.Split(item, amountToSteal, Transform(uid).Coordinates);
                if (stolenItems != null) _hands.TryForcePickupAnyHand(uid, stolenItems.Value);
                else throw new Exception("Попытка своровать стакабельный предмет произошла с ошибкой");
                _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(target):user} steal from {ToPrettyString(uid):target} item: {ToPrettyString(item):item}, amount: {amountToSteal}.");
            }
            else
            {
                _transform.SetCoordinates(item, Transform(uid).Coordinates);
                _hands.TryForcePickupAnyHand(uid, item);
                _adminLogger.Add(LogType.Action, LogImpact.Medium, $"User {ToPrettyString(target):user} steal from {ToPrettyString(uid):target} item: {ToPrettyString(item):item}.");
            }
        }
    }
}
