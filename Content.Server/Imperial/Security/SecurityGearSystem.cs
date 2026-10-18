using System.Linq;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Blocking;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Server.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>shield/riot/flash: щит со встроенной вспышкой.</summary>
[RegisterComponent]
public sealed partial class StrobeShieldComponent : Component
{
    public const string FlashContainer = "strobe-flash";

    [DataField]
    public string FlashProto = "Flash";

    [ViewVariables]
    public TimeSpan? FlashingUntil;
}

/// <summary>Метка на том, кто держит щит со вспышкой.</summary>
[RegisterComponent]
public sealed partial class StrobeShieldHolderComponent : Component;

/// <summary>clothing/accessory/holobadge.</summary>
[RegisterComponent]
public sealed partial class HolobadgeComponent : Component
{
    [ViewVariables]
    public string? StoredName;

    [ViewVariables]
    public bool Emagged;
}

/// <summary>
/// Щит со вспышкой (tgstation), пакет для улик (tgstation) и голобейдж (Paradise),
/// а также их коробки в шкафах смотрителя и детектива.
/// </summary>
public sealed class SecurityGearSystem : EntitySystem
{
    private static readonly string[] WardenLockers = ["LockerWardenFilled", "LockerWardenFilledHardsuit"];
    private static readonly string[] DetectiveLockers = ["LockerDetectiveFilled"];

    private static readonly SoundSpecifier BulbSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
    private static readonly SoundSpecifier UnzipSound = new SoundPathSpecifier("/Audio/Imperial/Security/EvidenceBag/evidence_bag_unzip.ogg");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrobeShieldComponent, MapInitEvent>(OnShieldMapInit);
        SubscribeLocalEvent<StrobeShieldComponent, UseInHandEvent>(OnShieldUse, before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<StrobeShieldComponent, AfterInteractEvent>(OnShieldAfterInteract);
        SubscribeLocalEvent<StrobeShieldComponent, InteractUsingEvent>(OnShieldInteractUsing);
        SubscribeLocalEvent<StrobeShieldComponent, StrobeShieldBulbDoAfterEvent>(OnBulbDoAfter);
        SubscribeLocalEvent<StrobeShieldComponent, GotEquippedHandEvent>(OnShieldEquipped);
        SubscribeLocalEvent<StrobeShieldComponent, GotUnequippedHandEvent>(OnShieldUnequipped);
        SubscribeLocalEvent<StrobeShieldHolderComponent, AttackedEvent>(OnHolderAttacked);

        SubscribeLocalEvent<EvidenceBagComponent, EntInsertedIntoContainerMessage>(OnBagInserted);
        SubscribeLocalEvent<EvidenceBagComponent, EntRemovedFromContainerMessage>(OnBagRemoved);
        SubscribeLocalEvent<EvidenceBagComponent, UseInHandEvent>(OnBagUse, before: [typeof(SharedStorageSystem)]);

        SubscribeLocalEvent<HolobadgeComponent, UseInHandEvent>(OnBadgeUse, before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<HolobadgeComponent, InteractUsingEvent>(OnBadgeInteractUsing);
        SubscribeLocalEvent<HolobadgeComponent, AfterInteractEvent>(OnBadgeAfterInteract);
        SubscribeLocalEvent<HolobadgeComponent, GotEmaggedEvent>(OnBadgeEmagged);

        SubscribeLocalEvent<PostGameMapLoad>(OnMapLoaded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StrobeShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (shield.FlashingUntil is not { } until || now < until)
                continue;

            shield.FlashingUntil = null;
            _appearance.SetData(uid, StrobeShieldVisuals.Flashing, false);
            _item.SetHeldPrefix(uid, "riot");
        }
    }

    #region Щит со вспышкой

    private void OnShieldMapInit(Entity<StrobeShieldComponent> ent, ref MapInitEvent args)
    {
        var slot = _container.EnsureContainer<ContainerSlot>(ent, StrobeShieldComponent.FlashContainer);
        if (slot.ContainedEntity == null)
            _container.Insert(Spawn(ent.Comp.FlashProto, Transform(ent).Coordinates), slot);
    }

    private EntityUid? GetFlash(EntityUid shield)
    {
        return _container.TryGetContainer(shield, StrobeShieldComponent.FlashContainer, out var slot) && slot.ContainedEntities.Count > 0
            ? slot.ContainedEntities[0]
            : null;
    }

    /// <summary>flash_away(): вспышка по цели или по площади, если цели нет.</summary>
    private bool FlashAway(Entity<StrobeShieldComponent> ent, EntityUid owner, EntityUid? target)
    {
        if (GetFlash(ent) is not { } flashUid || !TryComp<FlashComponent>(flashUid, out var flash))
            return false;

        if (!_flash.TryUseFlashItem((flashUid, flash), owner))
            return false;

        // TryUseFlashItem играет звук как предсказанный — без владельца, а клиент вспышку в щите не предсказывает.
        _audio.PlayEntity(flash.Sound, Filter.Entities(owner), ent, true);

        if (target is { } t)
            _flash.Flash(t, owner, flashUid, flash.MeleeDuration, flash.SlowTo, melee: true, stunDuration: flash.MeleeStunDuration);
        else
            _flash.FlashArea(ent, owner, flash.Range, flash.AoeFlashDuration, flash.SlowTo, true, flash.Probability);

        // flick("flashshield_flash"): полсекунды вспышки на спрайте и в руке.
        ent.Comp.FlashingUntil = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        _appearance.SetData(ent, StrobeShieldVisuals.Flashing, true);
        _item.SetHeldPrefix(ent, "flash");
        return true;
    }

    /// <summary>attack_self(): вспышка вокруг.</summary>
    private void OnShieldUse(Entity<StrobeShieldComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = FlashAway(ent, args.User, null);
    }

    /// <summary>attack() без боевого режима: ослепить цель.</summary>
    private void OnShieldAfterInteract(Entity<StrobeShieldComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = FlashAway(ent, args.User, target);
    }

    /// <summary>attackby(flash): заменить лампочку (2 секунды).</summary>
    private void OnShieldInteractUsing(Entity<StrobeShieldComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<FlashComponent>(args.Used))
            return;

        args.Handled = true;
        if (IsBurnt(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("strobe-shield-bulb-burnt"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("strobe-shield-bulb-start"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new StrobeShieldBulbDoAfterEvent(),
            ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private bool IsBurnt(EntityUid flash)
    {
        return TryComp<Content.Shared.Charges.Components.LimitedChargesComponent>(flash, out var charges) &&
               EntityManager.System<Content.Shared.Charges.Systems.SharedChargesSystem>().IsEmpty((flash, charges));
    }

    private void OnBulbDoAfter(Entity<StrobeShieldComponent> ent, ref StrobeShieldBulbDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } newFlash || IsBurnt(newFlash))
            return;

        args.Handled = true;
        var slot = _container.EnsureContainer<ContainerSlot>(ent, StrobeShieldComponent.FlashContainer);
        if (slot.ContainedEntity is { } old)
        {
            _container.Remove(old, slot);
            QueueDel(old);
        }

        _container.Insert(newFlash, slot);
        _audio.PlayPvs(BulbSound, ent);
    }

    private void OnShieldEquipped(Entity<StrobeShieldComponent> ent, ref GotEquippedHandEvent args)
    {
        EnsureComp<StrobeShieldHolderComponent>(args.User);
    }

    private void OnShieldUnequipped(Entity<StrobeShieldComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!_hands.EnumerateHeld(args.User).Any(h => h != ent.Owner && HasComp<StrobeShieldComponent>(h)))
            RemCompDeferred<StrobeShieldHolderComponent>(args.User);
    }

    /// <summary>hit_reaction(): удачный блок щитом — вспышка вокруг.</summary>
    private void OnHolderAttacked(Entity<StrobeShieldHolderComponent> ent, ref AttackedEvent args)
    {
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!TryComp<StrobeShieldComponent>(held, out var shield) ||
                !TryComp<BlockingComponent>(held, out var blocking) || !blocking.IsBlocking)
            {
                continue;
            }

            FlashAway((held, shield), ent, null);
            return;
        }
    }

    #endregion

    #region Пакет для улик

    /// <summary>on_insert(): пакет становится размером с улику, описание — про содержимое.</summary>
    private void OnBagInserted(Entity<EvidenceBagComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != EvidenceBagComponent.ContainerId)
            return;

        if (TryComp<ItemComponent>(args.Entity, out var item))
            _item.SetSize(ent, item.Size);

        _meta.SetEntityDescription(ent, Loc.GetString("evidence-bag-desc-filled", ("item", args.Entity),
            ("desc", MetaData(args.Entity).EntityDescription)));
        _appearance.SetData(ent, EvidenceBagVisuals.Filled, true);
    }

    private void OnBagRemoved(Entity<EvidenceBagComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != EvidenceBagComponent.ContainerId || args.Container.ContainedEntities.Count > 0)
            return;

        _item.SetSize(ent, "Tiny");
        _meta.SetEntityDescription(ent, Loc.GetString("evidence-bag-desc-empty"));
        _appearance.SetData(ent, EvidenceBagVisuals.Filled, false);
    }

    /// <summary>attack_self(): вытряхнуть содержимое.</summary>
    private void OnBagUse(Entity<EvidenceBagComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_container.TryGetContainer(ent, EvidenceBagComponent.ContainerId, out var container))
            return;

        args.Handled = true;
        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("evidence-bag-empty", ("bag", ent.Owner)), args.User, args.User);
            return;
        }

        var coords = Transform(args.User).Coordinates;
        foreach (var item in container.ContainedEntities.ToList())
        {
            _container.Remove(item, container, destination: coords);
            _hands.TryPickupAnyHand(args.User, item);
        }

        _popup.PopupEntity(Loc.GetString("evidence-bag-emptied-others", ("user", args.User), ("bag", ent.Owner)), args.User,
            Filter.PvsExcept(args.User), true);
        _popup.PopupEntity(Loc.GetString("evidence-bag-emptied", ("bag", ent.Owner)), args.User, args.User);
        _audio.PlayPvs(UnzipSound, ent);
    }

    #endregion

    #region Голобейдж

    /// <summary>activate_self(): предъявить удостоверение.</summary>
    private void OnBadgeUse(Entity<HolobadgeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.StoredName is not { } name)
        {
            _popup.PopupEntity(Loc.GetString("holobadge-no-id"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("holobadge-display-others", ("user", args.User), ("name", name)), args.User,
            Filter.PvsExcept(args.User), true, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("holobadge-display-self", ("name", name)), args.User, args.User, PopupType.Medium);
    }

    /// <summary>item_interaction(): отпечатать ID с доступом СБ (после емага — любой ID).</summary>
    private void OnBadgeInteractUsing(Entity<HolobadgeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_idCard.TryGetIdCard(args.Used, out var card))
            return;

        args.Handled = true;
        var hasAccess = TryComp<AccessComponent>(card, out var access) && access.Tags.Contains("Security");
        if (!hasAccess && !ent.Comp.Emagged)
        {
            _popup.PopupEntity(Loc.GetString("holobadge-rejected", ("badge", ent.Owner)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var name = card.Comp.FullName ?? Name(card);
        ent.Comp.StoredName = name;
        _meta.SetEntityName(ent, Loc.GetString("holobadge-name", ("name", name)));
        _meta.SetEntityDescription(ent, Loc.GetString("holobadge-desc", ("name", name)));
        _popup.PopupEntity(Loc.GetString("holobadge-imprinted"), args.User, args.User);
    }

    /// <summary>interact_with_atom(): ткнуть значком в лицо.</summary>
    private void OnBadgeAfterInteract(Entity<HolobadgeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("holobadge-thrust-others", ("user", args.User), ("target", target), ("badge", ent.Owner)),
            target, Filter.PvsExcept(args.User), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("holobadge-thrust-self", ("target", target), ("badge", ent.Owner)), args.User, args.User,
            PopupType.MediumCaution);
    }

    private void OnBadgeEmagged(Entity<HolobadgeComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (ent.Comp.Emagged)
        {
            _popup.PopupEntity(Loc.GetString("holobadge-already-emagged", ("badge", ent.Owner)), args.UserUid, args.UserUid);
            return;
        }

        ent.Comp.Emagged = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("holobadge-emagged"), args.UserUid, args.UserUid, PopupType.SmallCaution);
    }

    #endregion

    #region Шкафы

    /// <summary>Коробка голобейджей у смотрителя (как в Paradise), коробки пакетов для улик — смотрителю и детективу.</summary>
    private void OnMapLoaded(PostGameMapLoad args)
    {
        var query = EntityQueryEnumerator<Content.Shared.Storage.Components.EntityStorageComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var storage, out var meta, out var xform))
        {
            if (xform.MapID != args.Map || meta.EntityPrototype?.ID is not { } id)
                continue;

            var fill = new List<string>();
            if (WardenLockers.Contains(id))
            {
                fill.Add("BoxHolobadge");
                fill.Add("BoxEvidenceBags");
            }

            if (DetectiveLockers.Contains(id))
                fill.Add("BoxEvidenceBags");

            foreach (var proto in fill)
            {
                var item = Spawn(proto, xform.Coordinates);
                if (!_entityStorage.Insert(item, uid, storage))
                    QueueDel(item);
            }
        }
    }

    #endregion
}
