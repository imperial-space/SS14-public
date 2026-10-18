using System.Linq;
using System.Numerics;
using Content.Server.Clothing.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.Uplink.Conditions;
using Content.Server.Storage.EntitySystems;
using Content.Server.Store.Conditions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Uplink.Badass;
using Content.Shared.Implants;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.NukeOps;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Store;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.Badass;

/// <summary>
/// Позиции аплинка TG «(Pointless) Badassery» и «Bundles» (uplink_items/badass.dm, bundle.dm):
/// колода карт, стикеры, крамольные плакаты, баллончик, сломанный хамелеон, ящики излишков и имплант вступления.
/// </summary>
public sealed class UplinkBadassSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly ChameleonClothingSystem _chameleon = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StepTriggerSystem _stepTrigger = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private static readonly SoundSpecifier CardSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/cardflip.ogg");
    private static readonly SoundSpecifier StickerSound = new SoundPathSpecifier("/Audio/Items/Paper/paper_scribble1.ogg");
    private static readonly SoundSpecifier PosterSound = new SoundPathSpecifier("/Audio/Effects/poster_being_set.ogg");
    private static readonly SoundSpecifier SpraySound = new SoundPathSpecifier("/Audio/Effects/spray.ogg");
    private static readonly SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/Machines/boltsup.ogg");

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> SoapTag = "Soap";
    private static readonly ProtoId<CurrencyPrototype> Telecrystal = "Telecrystal";
    private static readonly ProtoId<NpcFactionPrototype> SyndicateFaction = "Syndicate";
    private static readonly EntProtoId NukeopsMindRole = "MindRoleNukeops";

    /// <summary>Позиции с surplus = 0 в TG: в ящики излишков не попадают.</summary>
    private static readonly HashSet<string> SurplusExcludedCategories = new()
    {
        "ImperialUplinkBundles",
        "ImperialUplinkBadassery",
        "ImperialUplinkReinforcements",
        "ImperialUplinkMech",
        "ImperialUplinkContractor",
        "ImperialUplinkRoleRestricted",
        "ImperialUplinkSpeciesRestricted",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkCardDeckComponent, MapInitEvent>(OnDeckMapInit, after: [typeof(ChameleonClothingSystem)]);
        SubscribeLocalEvent<UplinkCardDeckComponent, UseInHandEvent>(OnDeckUse);
        SubscribeLocalEvent<UplinkCardDeckComponent, InteractUsingEvent>(OnDeckInteractUsing);
        SubscribeLocalEvent<UplinkCardDeckComponent, ExaminedEvent>(OnDeckExamined);
        SubscribeLocalEvent<UplinkCardComponent, UseInHandEvent>(OnCardUse);

        SubscribeLocalEvent<UplinkStickerComponent, AfterInteractEvent>(OnStickerAfterInteract);
        SubscribeLocalEvent<UplinkStickeredComponent, GetVerbsEvent<AlternativeVerb>>(OnStickeredVerbs);
        SubscribeLocalEvent<UplinkStickeredComponent, EntityTerminatingEvent>(OnStickeredTerminating);

        SubscribeLocalEvent<UplinkTraitorPosterComponent, AfterInteractEvent>(OnPosterAfterInteract);
        SubscribeLocalEvent<UplinkTraitorPosterComponent, UplinkPosterPlaceDoAfterEvent>(OnPosterPlaced);

        SubscribeLocalEvent<UplinkSpraycanComponent, AfterInteractEvent>(OnSprayAfterInteract);
        SubscribeLocalEvent<UplinkSpraycanComponent, UplinkSpraycanDoAfterEvent>(OnSprayDoAfter);
        SubscribeLocalEvent<UplinkSpraycanComponent, ExaminedEvent>(OnSprayExamined);
        SubscribeLocalEvent<UplinkTraitorRuneComponent, MapInitEvent>(OnRuneMapInit, after: [typeof(ChameleonClothingSystem)]);
        SubscribeLocalEvent<UplinkTraitorRuneComponent, InteractUsingEvent>(OnRuneInteractUsing);

        SubscribeLocalEvent<UplinkBrokenChameleonComponent, MapInitEvent>(OnBrokenChameleonMapInit, after: [typeof(ChameleonClothingSystem)]);

        SubscribeLocalEvent<UplinkSurplusCrateComponent, MapInitEvent>(OnSurplusMapInit, after: [typeof(ChameleonClothingSystem)]);
        SubscribeLocalEvent<UplinkSurplusCrateComponent, InteractUsingEvent>(OnSurplusInteractUsing);
        SubscribeLocalEvent<UplinkSurplusCrateComponent, DamageChangedEvent>(OnSurplusDamaged);

        SubscribeLocalEvent<UplinkInductionImplantComponent, ImplantImplantedEvent>(OnInductionImplanted);
        SubscribeLocalEvent<UplinkInductionImplantComponent, ImplantRemovedEvent>(OnInductionRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<UplinkTraitorRuneComponent>();
        while (query.MoveNext(out var uid, out var rune))
        {
            if (rune.RemovableAt is not { } at || now < at)
                continue;

            rune.RemovableAt = null;
            SetRuneStage((uid, rune), UplinkRuneStage.Removable);
        }
    }

    #region Карты

    private void OnDeckMapInit(Entity<UplinkCardDeckComponent> ent, ref MapInitEvent args)
    {
        UpdateDeck(ent);
    }

    /// <summary>attack_self(): вытянуть карту.</summary>
    private void OnDeckUse(Entity<UplinkCardDeckComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Cards <= 0)
        {
            _popup.PopupEntity(Loc.GetString("uplink-deck-empty"), args.User, args.User);
            return;
        }

        ent.Comp.Cards--;
        UpdateDeck(ent);

        var card = Spawn(ent.Comp.Card, Transform(args.User).Coordinates);
        var comp = EnsureComp<UplinkCardComponent>(card);
        comp.CardName = Loc.GetString("uplink-card-name",
            ("rank", Loc.GetString($"uplink-card-rank-{_random.Next(13)}")),
            ("suit", Loc.GetString($"uplink-card-suit-{_random.Next(4)}")));

        _hands.TryPickupAnyHand(args.User, card);
        _audio.PlayPvs(CardSound, ent);
    }

    /// <summary>Вернуть карту в колоду.</summary>
    private void OnDeckInteractUsing(Entity<UplinkCardDeckComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<UplinkCardComponent>(args.Used) || ent.Comp.Cards >= ent.Comp.MaxCards)
            return;

        args.Handled = true;
        ent.Comp.Cards++;
        UpdateDeck(ent);
        QueueDel(args.Used);
        _audio.PlayPvs(CardSound, ent);
    }

    private void OnDeckExamined(Entity<UplinkCardDeckComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-deck-examine", ("count", ent.Comp.Cards)));
    }

    /// <summary>Flip(): перевернуть карту.</summary>
    private void OnCardUse(Entity<UplinkCardComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.FaceUp = !ent.Comp.FaceUp;
        _metaData.SetEntityName(ent, ent.Comp.FaceUp ? ent.Comp.CardName : Loc.GetString("uplink-card-facedown"));
        _popup.PopupEntity(ent.Comp.FaceUp
                ? Loc.GetString("uplink-card-flip-up", ("card", ent.Comp.CardName))
                : Loc.GetString("uplink-card-flip-down"),
            args.User,
            args.User);
    }

    /// <summary>update_icon_state(): 27+ — полная, 11+ — половина, 1+ — мало.</summary>
    private void UpdateDeck(Entity<UplinkCardDeckComponent> ent)
    {
        var state = ent.Comp.Cards switch
        {
            >= 27 => "full",
            >= 11 => "half",
            >= 1 => "low",
            _ => "empty",
        };

        _appearance.SetData(ent, UplinkCardDeckVisuals.State, state);
    }

    #endregion

    #region Стикеры

    /// <summary>sticker/interact_with_atom(): наклеить стикер туда, куда кликнули.</summary>
    private void OnStickerAfterInteract(Entity<UplinkStickerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        args.Handled = true;

        var targetPos = _transform.GetWorldPosition(target);
        var clickPos = _transform.ToMapCoordinates(args.ClickLocation).Position;
        var offset = Vector2.Clamp(clickPos - targetPos, new Vector2(-0.4f), new Vector2(0.4f));
        var localOffset = (-_transform.GetWorldRotation(target)).RotateVec(offset);

        var sticker = Spawn(ent.Comp.Attached, new EntityCoordinates(target, localOffset));
        EnsureComp<UplinkStickeredComponent>(target).Stickers.Add(sticker);

        _popup.PopupEntity(Loc.GetString("uplink-sticker-applied", ("target", target)), args.User, args.User);
        _audio.PlayPvs(StickerSound, target);
        QueueDel(ent);
    }

    private void OnStickeredVerbs(Entity<UplinkStickeredComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Stickers.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-sticker-peel-verb"),
            Act = () => PeelSticker(ent, user),
        });
    }

    /// <summary>peel(): отклеить последний наклеенный стикер.</summary>
    private void PeelSticker(Entity<UplinkStickeredComponent> ent, EntityUid user)
    {
        while (ent.Comp.Stickers.Count > 0)
        {
            var sticker = ent.Comp.Stickers[^1];
            ent.Comp.Stickers.RemoveAt(ent.Comp.Stickers.Count - 1);
            if (!TryComp<UplinkAttachedStickerComponent>(sticker, out var attached) || TerminatingOrDeleted(sticker))
                continue;

            var item = Spawn(attached.Item, Transform(user).Coordinates);
            _hands.TryPickupAnyHand(user, item);
            QueueDel(sticker);
            _popup.PopupEntity(Loc.GetString("uplink-sticker-peeled"), user, user);
            break;
        }

        if (ent.Comp.Stickers.Count == 0)
            RemCompDeferred<UplinkStickeredComponent>(ent);
    }

    private void OnStickeredTerminating(Entity<UplinkStickeredComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var sticker in ent.Comp.Stickers)
        {
            if (!TerminatingOrDeleted(sticker))
                QueueDel(sticker);
        }
    }

    #endregion

    #region Плакаты

    private void OnPosterAfterInteract(Entity<UplinkTraitorPosterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } wall || !_tag.HasTag(wall, WallTag))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("uplink-poster-placing"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new UplinkPosterPlaceDoAfterEvent(), ent, wall, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    /// <summary>poster/traitor/random: случайный крамольный плакат.</summary>
    private void OnPosterPlaced(Entity<UplinkTraitorPosterComponent> ent, ref UplinkPosterPlaceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } wall || TerminatingOrDeleted(wall) || ent.Comp.Posters.Count == 0)
            return;

        args.Handled = true;
        Spawn(_random.Pick(ent.Comp.Posters), Transform(wall).Coordinates);
        _audio.PlayPvs(PosterSound, wall);
        QueueDel(ent);
    }

    #endregion

    #region Баллончик

    private void OnSprayExamined(Entity<UplinkSpraycanComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Expended)
            args.PushMarkup(Loc.GetString("uplink-spraycan-expended-examine"));
    }

    /// <summary>interact_with_atom(): начать или продолжить граффити.</summary>
    private void OnSprayAfterInteract(Entity<UplinkSpraycanComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        args.Handled = true;

        if (ent.Comp.Expended)
        {
            _popup.PopupEntity(Loc.GetString("uplink-spraycan-empty"), args.User, args.User);
            return;
        }

        if (ent.Comp.Drawing)
        {
            _popup.PopupEntity(Loc.GetString("uplink-spraycan-busy"), args.User, args.User);
            return;
        }

        if (args.Target is { } target && TryComp<UplinkTraitorRuneComponent>(target, out var rune))
        {
            ent.Comp.Rune = target;
            ContinueRune(ent, args.User, (target, rune));
            return;
        }

        if (!CanDrawRune(args.ClickLocation))
        {
            _popup.PopupEntity(Loc.GetString("uplink-spraycan-no-space"), args.User, args.User);
            return;
        }

        ent.Comp.Rune = null;
        ent.Comp.Spot = args.ClickLocation;
        StartSprayStep(ent, args.User, null, Loc.GetString("uplink-spraycan-outline"));
    }

    /// <summary>try_draw_new_rune(): нужна свободная площадка 3x3 без космоса.</summary>
    private bool CanDrawRune(EntityCoordinates coords)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var tile = _turf.GetTileRef(coords.Offset(new Vector2(x, y)));
                if (tile == null || _turf.IsSpace(tile.Value))
                    return false;
            }
        }

        return true;
    }

    private void ContinueRune(Entity<UplinkSpraycanComponent> ent, EntityUid user, Entity<UplinkTraitorRuneComponent> rune)
    {
        switch (rune.Comp.Stage)
        {
            case UplinkRuneStage.Outline:
                StartSprayStep(ent, user, rune, Loc.GetString("uplink-spraycan-finalising"));
                break;
            case UplinkRuneStage.Coloured:
                StartSprayStep(ent, user, rune, Loc.GetString("uplink-spraycan-coating"));
                break;
            default:
                _popup.PopupEntity(Loc.GetString("uplink-spraycan-done"), user, user);
                break;
        }
    }

    /// <summary>try_draw_step(): три секунды скрытого do_after рядом с целью.</summary>
    private void StartSprayStep(Entity<UplinkSpraycanComponent> ent, EntityUid user, EntityUid? rune, string message)
    {
        _popup.PopupEntity(message, user, user);
        ent.Comp.Drawing = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.DrawTime, new UplinkSpraycanDoAfterEvent(), ent, rune, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            Hidden = true,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnSprayDoAfter(Entity<UplinkSpraycanComponent> ent, ref UplinkSpraycanDoAfterEvent args)
    {
        ent.Comp.Drawing = false;
        var user = args.User;

        if (args.Cancelled || args.Handled)
        {
            _popup.PopupEntity(Loc.GetString("uplink-spraycan-interrupted"), user, user);
            return;
        }

        args.Handled = true;
        _audio.PlayPvs(SpraySound, ent);

        // draw_rune(): первый шаг создаёт контур на месте клика.
        if (ent.Comp.Rune == null)
        {
            if (ent.Comp.Spot is not { } spot)
                return;

            var created = Spawn(ent.Comp.RuneProto, _transform.ToMapCoordinates(spot.SnapToGrid(EntityManager, _mapManager)));
            var createdComp = EnsureComp<UplinkTraitorRuneComponent>(created);
            ent.Comp.Rune = created;
            ent.Comp.Spot = null;
            SetRuneStage((created, createdComp), UplinkRuneStage.Outline);
            ContinueRune(ent, user, (created, createdComp));
            return;
        }

        if (!TryComp<UplinkTraitorRuneComponent>(ent.Comp.Rune, out var rune) || TerminatingOrDeleted(ent.Comp.Rune.Value))
        {
            _popup.PopupEntity(Loc.GetString("uplink-spraycan-destroyed"), user, user);
            ent.Comp.Rune = null;
            return;
        }

        var runeUid = ent.Comp.Rune.Value;
        switch (rune.Stage)
        {
            case UplinkRuneStage.Outline:
                SetRuneStage((runeUid, rune), UplinkRuneStage.Coloured);
                ContinueRune(ent, user, (runeUid, rune));
                break;
            case UplinkRuneStage.Coloured:
                _popup.PopupEntity(Loc.GetString("uplink-spraycan-finished"), user, user);
                SetRuneStage((runeUid, rune), UplinkRuneStage.Complete);
                ent.Comp.Expended = true;
                ent.Comp.Rune = null;
                _metaData.SetEntityDescription(ent, Loc.GetString("uplink-spraycan-expended-desc"));
                break;
        }
    }

    private void OnRuneMapInit(Entity<UplinkTraitorRuneComponent> ent, ref MapInitEvent args)
    {
        _stepTrigger.SetActive(ent, false);
    }

    /// <summary>set_stage(): после завершения граффити скользкое и 5 минут не отмывается.</summary>
    private void SetRuneStage(Entity<UplinkTraitorRuneComponent> ent, UplinkRuneStage stage)
    {
        ent.Comp.Stage = stage;
        var state = stage switch
        {
            UplinkRuneStage.Outline => "outline",
            UplinkRuneStage.Complete => "sheen",
            _ => "done",
        };

        _appearance.SetData(ent, UplinkRuneVisuals.Stage, state);
        _metaData.SetEntityDescription(ent, Loc.GetString(stage switch
        {
            UplinkRuneStage.Outline => "uplink-rune-desc-outline",
            UplinkRuneStage.Complete => "uplink-rune-desc-complete",
            _ => "uplink-rune-desc-done",
        }));

        if (stage == UplinkRuneStage.Complete)
        {
            ent.Comp.RemovableAt = _timing.CurTime + TimeSpan.FromMinutes(5);
            _stepTrigger.SetActive(ent, true);
        }
    }

    /// <summary>wash(): отмыть мылом или шваброй, пока граффити не защищено.</summary>
    private void OnRuneInteractUsing(Entity<UplinkTraitorRuneComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, SoapTag) && !HasComp<AbsorbentComponent>(args.Used))
            return;

        args.Handled = true;
        if (ent.Comp.Stage == UplinkRuneStage.Complete)
        {
            _popup.PopupEntity(Loc.GetString("uplink-rune-clean-proof"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("uplink-rune-cleaned"), args.User, args.User);
        QueueDel(ent);
    }

    #endregion

    #region Сломанный хамелеон

    /// <summary>chameleon/broken: emp_randomise(INFINITY) — случайный облик навсегда.</summary>
    private void OnBrokenChameleonMapInit(Entity<UplinkBrokenChameleonComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<ChameleonClothingComponent>(ent, out var chameleon))
            return;

        var pick = _chameleon.GetRandomValidPrototype(chameleon.Slot, chameleon.RequireTag);
        _chameleon.SetSelectedPrototype(ent, pick, true);
    }

    #endregion

    #region Ящики излишков

    private void OnSurplusMapInit(Entity<UplinkSurplusCrateComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Contents = RollSurplus(ent.Comp.Value);
        if (ent.Comp.United)
            return;

        FillSurplus(ent);
    }

    /// <summary>generate_possible_items() + fill_crate(): случайные позиции на заданную сумму ТК.</summary>
    public List<EntProtoId> RollSurplus(int value)
    {
        var pool = new List<(EntProtoId Proto, int Cost)>();
        foreach (var listing in _proto.EnumeratePrototypes<ListingPrototype>())
        {
            if (listing.ProductEntity is not { } product
                || !listing.Cost.TryGetValue(Telecrystal, out var cost)
                || cost <= 0
                || !listing.Categories.Any(c => c.Id.StartsWith("ImperialUplink") && !SurplusExcludedCategories.Contains(c.Id))
                || !SurplusConditionsMet(listing))
                continue;

            pool.Add((product, cost.Int()));
        }

        var result = new List<EntProtoId>();
        var budget = value;
        while (budget > 0)
        {
            var candidates = pool.Where(p => p.Cost <= budget).ToList();
            if (candidates.Count == 0)
                break;

            var pick = _random.Pick(candidates);
            budget -= pick.Cost;
            result.Add(pick.Proto);
        }

        return result;
    }

    /// <summary>check_if_restricted() и not_enough_reputation(): только позиции предателя, доступные по репутации.</summary>
    private bool SurplusConditionsMet(ListingPrototype listing)
    {
        if (listing.Conditions == null)
            return true;

        var traitor = false;
        foreach (var condition in listing.Conditions)
        {
            switch (condition)
            {
                case ImperialUplinkPurchasableCondition purchasable:
                    if (!purchasable.Traitor)
                        return false;
                    traitor = true;
                    break;
                case ImperialUplinkProgressionCondition progression:
                    if (!progression.Condition(new ListingConditionArgs(EntityUid.Invalid, null, listing, EntityManager)))
                        return false;
                    break;
                case BuyerJobCondition or BuyerSpeciesCondition:
                    return false;
            }
        }

        return traitor;
    }

    private void FillSurplus(Entity<UplinkSurplusCrateComponent> ent)
    {
        var coords = Transform(ent).Coordinates;
        foreach (var proto in ent.Comp.Contents)
        {
            var item = Spawn(proto, coords);
            _entityStorage.Insert(item, ent);
        }

        ent.Comp.Contents.Clear();
    }

    /// <summary>syndicrate/attackby(): ключ открывает объединённый ящик и сгорает.</summary>
    private void OnSurplusInteractUsing(Entity<UplinkSurplusCrateComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.United || ent.Comp.Unlocked || !HasComp<UplinkSyndicrateKeyComponent>(args.Used))
            return;

        args.Handled = true;
        ent.Comp.Unlocked = true;
        Dirty(ent);
        FillSurplus(ent);
        QueueDel(args.Used);

        _popup.PopupEntity(Loc.GetString("uplink-syndicrate-unlocked"), args.User, args.User);
        _audio.PlayPvs(UnlockSound, ent);
    }

    /// <summary>syndicrate/take_damage(): закрытый ящик в 25% случаев взрывается от повреждений.</summary>
    private void OnSurplusDamaged(Entity<UplinkSurplusCrateComponent> ent, ref DamageChangedEvent args)
    {
        if (!ent.Comp.United || ent.Comp.Unlocked || !args.DamageIncreased || args.DamageDelta == null
            || args.DamageDelta.GetTotal() < 1 || !_random.Prob(0.25f))
            return;

        _popup.PopupEntity(Loc.GetString("uplink-syndicrate-tamper"), ent, PopupType.LargeCaution);
        _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "Default", 30f, 5f, 10f, ent);
        QueueDel(ent);
    }

    #endregion

    #region Имплант вступления

    /// <summary>implant/nuclear_operative/implant(): только антагонист становится ядерным оперативником.</summary>
    private void OnInductionImplanted(Entity<UplinkInductionImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        var target = args.Implanted;
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return;

        if (HasComp<NukeOperativeComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("uplink-induction-already"), target, target, PopupType.MediumCaution);
            AddToxin(target, 2);
            return;
        }

        if (!_roles.MindIsAntagonist(mindId))
        {
            _popup.PopupEntity(Loc.GetString("uplink-induction-nothing"), target, target, PopupType.MediumCaution);
            AddToxin(target, 15);
            return;
        }

        EnsureComp<NukeOperativeComponent>(target);
        _faction.AddFaction(target, SyndicateFaction);
        if (!_roles.MindHasRole<NukeopsRoleComponent>(mindId))
            _roles.MindAddRole(mindId, NukeopsMindRole, mind, true);

        _popup.PopupEntity(Loc.GetString("uplink-induction-success"), target, target, PopupType.Large);
    }

    private void OnInductionRemoved(Entity<UplinkInductionImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        var target = args.Implanted;
        if (!HasComp<NukeOperativeComponent>(target))
            return;

        RemComp<NukeOperativeComponent>(target);
        _faction.RemoveFaction(target, SyndicateFaction);
        if (_mind.TryGetMind(target, out var mindId, out _))
            _roles.MindRemoveRole<NukeopsRoleComponent>(mindId);

        _popup.PopupEntity(Loc.GetString("uplink-induction-removed"), target, target, PopupType.Large);
    }

    private void AddToxin(EntityUid target, float amount)
    {
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        _bloodstream.TryAddToBloodstream((target, bloodstream), new Solution("Toxin", amount));
    }

    #endregion
}
