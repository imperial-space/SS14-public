using System.Linq;
using Content.Server.Access.Systems;
using Content.Shared.Access.Systems;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Imperial.Heretic.KeyRing;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic.KeyRing;

public sealed class HereticMysticCardSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMysticCardComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HereticMysticCardComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HereticMysticCardComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<HereticMysticCardComponent, HereticMysticCardSelectMessage>(OnSelectAppearance);
    }

    private void OnAfterInteract(EntityUid uid, HereticMysticCardComponent comp, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || args.Handled) return;
        var target = args.Target.Value;

        // Поглощение ID карты
        if (HasComp<IdCardComponent>(target) && TryComp<AccessComponent>(target, out var targetAccess))
        {
            AbsorbCard(uid, comp, target, targetAccess, args.User);
            args.Handled = true;
            return;
        }

        // Создание / привязка портала к двери
        if (HasComp<DoorComponent>(target))
        {
            HandleDoorPortal(uid, comp, target, args.User);
            args.Handled = true;
        }
    }

    private void AbsorbCard(EntityUid uid, HereticMysticCardComponent comp, EntityUid card, AccessComponent cardAccess, EntityUid user)
    {
        var cardName = MetaData(card).EntityName;

        // Копируем доступ на нашу карту
        if (TryComp<AccessComponent>(uid, out var ourAccess))
        {
            ourAccess.Tags.UnionWith(cardAccess.Tags);
            Dirty(uid, ourAccess);
        }

        // Запоминаем поглощённую карту для BUI
        comp.AbsorbedCards[cardName] = cardAccess.Tags.Select(t => t.Id).ToList();
        Dirty(uid, comp);

        QueueDel(card);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/eating_1.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-mystic-card-absorb", ("card", cardName)), uid, user);
    }

    private void HandleDoorPortal(EntityUid uid, HereticMysticCardComponent comp, EntityUid door, EntityUid user)
    {
        if (comp.PendingDoor == null || !Exists(comp.PendingDoor.Value))
        {
            // Первый выбор — запоминаем
            comp.PendingDoor = door;
            _popup.PopupEntity(Loc.GetString("heretic-mystic-card-portal-first"), uid, user);
            return;
        }

        var door1 = comp.PendingDoor.Value;
        var door2 = door;
        comp.PendingDoor = null;

        // Удаляем старые порталы
        if (comp.PortalOne.HasValue && Exists(comp.PortalOne.Value))
            QueueDel(comp.PortalOne.Value);
        if (comp.PortalTwo.HasValue && Exists(comp.PortalTwo.Value))
            QueueDel(comp.PortalTwo.Value);

        // Создаём новые
        var p1 = Spawn("HereticLockPortal", Transform(door1).Coordinates);
        var p2 = Spawn("HereticLockPortal", Transform(door2).Coordinates);

        if (TryComp<HereticLockPortalComponent>(p1, out var pc1))
        {
            pc1.Partner = p2;
            pc1.Inverted = comp.Inverted;
        }
        if (TryComp<HereticLockPortalComponent>(p2, out var pc2))
        {
            pc2.Partner = p1;
            pc2.Inverted = comp.Inverted;
        }

        comp.PortalOne = p1;
        comp.PortalTwo = p2;
        Dirty(uid, comp);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg"), p1);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg"), p2);
        _popup.PopupEntity(Loc.GetString("heretic-mystic-card-portal-linked"), uid, user);
    }

    private void OnUseInHand(EntityUid uid, HereticMysticCardComponent comp, UseInHandEvent args)
    {
        if (comp.AbsorbedCards.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-mystic-card-no-cards"), uid, args.User);
            return;
        }

        var state = BuildState(comp);
        _ui.SetUiState(uid, HereticMysticCardUiKey.Key, state);
        _ui.TryOpenUi(uid, HereticMysticCardUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnAlternativeVerb(EntityUid uid, HereticMysticCardComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract) return;

        var captured = comp;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(comp.Inverted
                ? "heretic-mystic-card-invert-off"
                : "heretic-mystic-card-invert-on"),
            Act = () => ToggleInvert(uid, captured, args.User)
        });
    }

    private void ToggleInvert(EntityUid uid, HereticMysticCardComponent comp, EntityUid user)
    {
        comp.Inverted = !comp.Inverted;

        if (comp.PortalOne.HasValue && TryComp<HereticLockPortalComponent>(comp.PortalOne.Value, out var pc1))
            pc1.Inverted = comp.Inverted;
        if (comp.PortalTwo.HasValue && TryComp<HereticLockPortalComponent>(comp.PortalTwo.Value, out var pc2))
            pc2.Inverted = comp.Inverted;

        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString(comp.Inverted
            ? "heretic-mystic-card-inverted"
            : "heretic-mystic-card-normal"), uid, user);
    }

    private void OnSelectAppearance(EntityUid uid, HereticMysticCardComponent comp, HereticMysticCardSelectMessage args)
    {
        if (!comp.AbsorbedCards.ContainsKey(args.CardName)) return;

        _idCard.TryChangeFullName(uid, args.CardName);
        _idCard.TryChangeJobTitle(uid, Loc.GetString("heretic-mystic-card-job-title"));

        _popup.PopupEntity(Loc.GetString("heretic-mystic-card-appearance-changed", ("card", args.CardName)), uid, args.Actor);
    }

    private static HereticMysticCardBuiState BuildState(HereticMysticCardComponent comp)
    {
        var cards = comp.AbsorbedCards
            .Select(kv => new HereticAbsorbedCardInfo { Name = kv.Key, AccessTags = kv.Value })
            .ToList();
        return new HereticMysticCardBuiState(cards, comp.Inverted);
    }
}
