using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Drunk;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Toggleable;
using Content.Shared.Wagging;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.EmotePanel;

/// <summary>status_effect/offering TG: протянутый предмет-рука и те, кто может его принять.</summary>
[RegisterComponent]
public sealed partial class ImperialEmoteOfferComponent : Component
{
    public EntityUid Item;
    public ImperialHandItemKind Kind;

    /// <summary>offering/no_item_received/needs_resting: рука помогает подняться лежащему.</summary>
    public bool HelpUp;

    public ProtoId<AlertPrototype> Alert;
    public List<EntityUid> Takers = new();

    /// <summary>too_slowing_this_guy: кто уже попался на «слишком медленно».</summary>
    public HashSet<EntityUid> TooSlowing = new();
}

/// <summary>Кому сейчас что-то предлагают.</summary>
[RegisterComponent]
public sealed partial class ImperialEmoteTakerComponent : Component
{
    public EntityUid Offerer;
}

/// <summary>Держит руку-кольцо: тот, кто осмотрит его вблизи, получит щелбан.</summary>
[RegisterComponent]
public sealed partial class ImperialCircleOwnerComponent : Component
{
    public EntityUid Item;
}

/// <summary>Идёт подзатыльник: номер круга и «жестокость» (усики молей).</summary>
[RegisterComponent]
public sealed partial class ImperialNoogieComponent : Component
{
    public int Iteration;
    public bool Brutal;
}

/// <summary>/obj/item/hand_item TG: пощёчина и «дать пять», поцелуй, кольцо, подзатыльник, рука, кража обуви.</summary>
public sealed class ImperialEmoteHandSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ImperialEmoteSystem _emote = default!;

    private static readonly EntProtoId KissProjectile = "ImperialKissProjectile";
    private static readonly ProtoId<AlertPrototype> AlertHighFive = "ImperialOfferHighFive";
    private static readonly ProtoId<AlertPrototype> AlertHand = "ImperialOfferHand";
    private static readonly ProtoId<AlertPrototype> AlertHelpUp = "ImperialOfferHelpUp";
    private static readonly ProtoId<AlertPrototype> AlertKiss = "ImperialOfferKiss";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    private static readonly SoundSpecifier SlapSound = new SoundPathSpecifier("/Audio/Imperial/EmotePanel/slap.ogg");
    private static readonly SoundSpecifier TableSound = new SoundPathSpecifier("/Audio/Imperial/EmotePanel/tableslam.ogg");
    private static readonly SoundSpecifier KissSound = new SoundPathSpecifier("/Audio/Imperial/EmotePanel/kiss.ogg");
    private static readonly SoundSpecifier PunchSound = new SoundPathSpecifier("/Audio/Weapons/punch3.ogg");
    private static readonly SoundSpecifier WhooshSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
    private static readonly SoundSpecifier RustleSound = new SoundPathSpecifier("/Audio/Effects/rustle1.ogg");

    private const float OfferRange = 1.5f;
    private const float KissSpeed = 10f;

    /// <summary>strip_delay ботинок.</summary>
    private static readonly TimeSpan StealDelay = TimeSpan.FromSeconds(4);

    private readonly List<(TimeSpan Time, Action Action)> _scheduled = new();
    private TimeSpan _nextOfferCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialHandItemComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<ImperialHandItemComponent, BeforeRangedInteractEvent>(OnInteract);
        SubscribeLocalEvent<ImperialHandItemComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ImperialHandItemComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ImperialKissProjectileComponent, ProjectileHitEvent>(OnKissHit);
        SubscribeLocalEvent<ImperialCircleOwnerComponent, ExaminedEvent>(OnCircleExamined);
        SubscribeLocalEvent<ImperialEmoteOfferAlertEvent>(OnOfferAlert);
        SubscribeLocalEvent<ImperialNoogieComponent, ImperialNoogieDoAfterEvent>(OnNoogieDoAfter);
        SubscribeLocalEvent<HandsComponent, ImperialShoeStealDoAfterEvent>(OnShoeStealDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        for (var i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (now < _scheduled[i].Time)
                continue;

            var action = _scheduled[i].Action;
            _scheduled.RemoveAt(i);
            action();
        }

        if (now < _nextOfferCheck)
            return;

        _nextOfferCheck = now + TimeSpan.FromSeconds(0.25);
        var query = EntityQueryEnumerator<ImperialEmoteOfferComponent>();
        while (query.MoveNext(out var uid, out var offer))
            CheckOffer(uid, offer);
    }

    private void Schedule(float seconds, Action action) => _scheduled.Add((_timing.CurTime + TimeSpan.FromSeconds(seconds), action));

    /// <summary>visible_message: другим одно, себе другое, цели третье.</summary>
    private void Show(EntityUid source, string others, string self, EntityUid? target = null, string? targetMessage = null, PopupType type = PopupType.Small)
    {
        var filter = Filter.PvsExcept(source);
        if (target != null && targetMessage != null)
        {
            filter.RemoveWhereAttachedEntity(e => e == target.Value);
            _popup.PopupEntity(targetMessage, target.Value, target.Value, PopupType.MediumCaution);
        }

        _popup.PopupEntity(others, source, filter, true, type);
        _popup.PopupEntity(self, source, source, type);
    }

    private string L(string key, params (string, object)[] args) => Loc.GetString($"imperial-hand-{key}", args);

    #region Выдача

    /// <summary>put_in_hands предмета-руки из эмоции.</summary>
    public void GiveHandItem(EntityUid uid, EntProtoId proto)
    {
        var item = Spawn(proto, Transform(uid).Coordinates);
        var kind = Comp<ImperialHandItemComponent>(item).Kind;
        var name = kind.ToString().ToLowerInvariant();

        if (kind == ImperialHandItemKind.Circle && _hands.CountFreeHands(uid) == 0)
        {
            Del(item);
            _popup.PopupEntity(L("circle-no-hands"), uid, uid);
            return;
        }

        if (!_hands.TryPickupAnyHand(uid, item, checkActionBlocker: false, animate: false))
        {
            Del(item);
            _popup.PopupEntity(L($"{name}-fail"), uid, uid);
            return;
        }

        _popup.PopupEntity(L($"{name}-ready"), uid, uid);
        if (kind == ImperialHandItemKind.Circle)
            EnsureComp<ImperialCircleOwnerComponent>(uid).Item = item;
    }

    /// <summary>DROPDEL: рука перестаёт быть рукой, как только её отпустили.</summary>
    private void OnUnequipped(Entity<ImperialHandItemComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.Kind == ImperialHandItemKind.Circle && TryComp<ImperialCircleOwnerComponent>(args.User, out var owner) && owner.Item == ent.Owner)
            RemComp<ImperialCircleOwnerComponent>(args.User);

        QueueDel(ent);
    }

    #endregion

    #region Клики

    private void OnInteract(Entity<ImperialHandItemComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (ent.Comp.Kind == ImperialHandItemKind.Kisser)
        {
            args.Handled = true;
            if (args.Target is { } kissed && kissed != user)
                Show(user, L("kiss-blow-others", ("user", user), ("target", kissed)), L("kiss-blow-self", ("target", kissed)));

            FireKiss(user, args.ClickLocation, null);
            QueueDel(ent);
            return;
        }

        if (!args.CanReach || args.Target is not { } target)
            return;

        var mob = HasComp<MobStateComponent>(target);
        switch (ent.Comp.Kind)
        {
            case ImperialHandItemKind.Slapper when mob:
                args.Handled = true;
                Slap(user, target);
                break;
            case ImperialHandItemKind.Slapper when HasComp<PlaceableSurfaceComponent>(target):
                args.Handled = true;
                SlapTable(user, ent, target, false);
                break;
            case ImperialHandItemKind.Noogie when mob:
                args.Handled = true;
                TryNoogie(user, target);
                break;
            case ImperialHandItemKind.Offer when mob && target != user:
                args.Handled = true;
                Offer(user, ent, target);
                break;
            case ImperialHandItemKind.Stealer when mob:
                args.Handled = true;
                TrySteal(user, target);
                break;
        }
    }

    /// <summary>Правая кнопка TG у шлепка — удар кулаком по столу; здесь это удар в боевом режиме.</summary>
    private void OnMeleeHit(Entity<ImperialHandItemComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.Kind != ImperialHandItemKind.Slapper || args.HitEntities.Count == 0)
            return;

        var target = args.HitEntities[0];
        if (HasComp<MobStateComponent>(target))
            Slap(args.User, target);
        else if (HasComp<PlaceableSurfaceComponent>(target))
            SlapTable(args.User, ent, target, true);
    }

    /// <summary>Предложить (give TG): пятюня, рука, поцелуй.</summary>
    private void OnUseInHand(Entity<ImperialHandItemComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Kind is not (ImperialHandItemKind.Slapper or ImperialHandItemKind.Offer or ImperialHandItemKind.Kisser))
            return;

        args.Handled = true;
        Offer(args.User, ent, null);
    }

    #endregion

    #region Пощёчина

    private void Slap(EntityUid user, EntityUid target)
    {
        var volume = 0f;

        // Ответ на предложенный поцелуй: «Да как ты смеешь!»
        if (TryComp<ImperialEmoteOfferComponent>(target, out var offer) && offer.Kind == ImperialHandItemKind.Kisser && offer.Takers.Contains(user))
        {
            Show(user, L("slap-kiss-others", ("user", user), ("target", target)), L("slap-kiss-self", ("target", target)),
                target, L("slap-kiss-target", ("user", user)), PopupType.MediumCaution);
            volume = 6f;
            _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(2.5));
            _stamina.TakeStaminaDamage(target, 40, source: user);
            EndOffer(target, offer);
        }
        else if (user == target)
        {
            Show(user, L("facepalm-others", ("user", user)), L("facepalm-self"));
        }
        else if (HasComp<SleepingComponent>(target))
        {
            Show(user, L("slap-wake-others", ("user", user), ("target", target)), L("slap-wake-self", ("target", target)));
            _sleeping.TryWaking(target, user: user);
        }
        else
        {
            Show(user, L("slap-others", ("user", user), ("target", target)), L("slap-self", ("target", target)),
                target, L("slap-target", ("user", user)));
        }

        // unwag_tail: от пощёчины хвост перестаёт вилять.
        if (TryComp<WaggingComponent>(target, out var wagging) && wagging.Wagging)
            RaiseLocalEvent(target, new ToggleActionEvent());

        _audio.PlayPvs(SlapSound, target, AudioParams.Default.WithVolume(volume).WithVariation(0.125f));
    }

    private void SlapTable(EntityUid user, Entity<ImperialHandItemComponent> item, EntityUid table, bool slam)
    {
        // slam_table: только первым ударом и только целой рукой.
        if (slam && item.Comp.TableSmacks == 3)
        {
            _audio.PlayPvs(TableSound, table, AudioParams.Default.WithVolume(6f).WithVariation(0.125f));
            Show(user, L("table-slam-others", ("user", user), ("table", table)), L("table-slam-self", ("table", table)), type: PopupType.MediumCaution);
            QueueDel(item);
            return;
        }

        _audio.PlayPvs(TableSound, table, AudioParams.Default.WithVolume(-4f).WithVariation(0.125f));
        Show(user, L("table-slap-others", ("user", user), ("table", table)), L("table-slap-self", ("table", table)));
        if (--item.Comp.TableSmacks <= 0)
            QueueDel(item);
    }

    #endregion

    #region Предложения

    private bool CanTake(EntityUid user, EntityUid taker, bool helpUp)
    {
        if (taker == user || !HasComp<MobStateComponent>(taker) || _mobState.IsIncapacitated(taker))
            return false;

        if (!_interaction.InRangeUnobstructed(user, taker, OfferRange))
            return false;

        return helpUp ? _standing.IsDown(taker) : _hands.CountFreeHands(taker) > 0 || TryComp<HandsComponent>(taker, out var hands) && hands.Count > 0;
    }

    private void Offer(EntityUid user, Entity<ImperialHandItemComponent> item, EntityUid? target)
    {
        var kind = item.Comp.Kind;
        if (kind == ImperialHandItemKind.Offer && _standing.IsDown(user))
        {
            _popup.PopupEntity(L("hand-lying"), user, user);
            return;
        }

        var nearby = new List<EntityUid>();
        if (target != null)
        {
            nearby.Add(target.Value);
        }
        else
        {
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(user).Coordinates, OfferRange))
                nearby.Add(mob.Owner);
        }

        // Рука лежащему — помочь подняться, иначе просто протянутая рука.
        var helpUp = kind == ImperialHandItemKind.Offer && nearby.Exists(e => e != user && _standing.IsDown(e) && CanTake(user, e, true));
        var takers = nearby.FindAll(e => CanTake(user, e, helpUp));
        if (kind == ImperialHandItemKind.Kisser)
            takers.RemoveAll(e => !_emote.IsKind(e, ImperialEmoteMob.Carbon));

        if (takers.Count == 0)
        {
            _popup.PopupEntity(L("offer-nobody"), user, user);
            return;
        }

        if (TryComp<ImperialEmoteOfferComponent>(user, out var old))
            EndOffer(user, old);

        var offer = EnsureComp<ImperialEmoteOfferComponent>(user);
        offer.Item = item;
        offer.Kind = kind;
        offer.HelpUp = helpUp;
        offer.Takers = takers;
        offer.Alert = kind switch
        {
            ImperialHandItemKind.Slapper => AlertHighFive,
            ImperialHandItemKind.Kisser => AlertKiss,
            _ => helpUp ? AlertHelpUp : AlertHand,
        };

        foreach (var taker in takers)
        {
            _alerts.ShowAlert(taker, offer.Alert);
            EnsureComp<ImperialEmoteTakerComponent>(taker).Offerer = user;
        }

        switch (kind)
        {
            case ImperialHandItemKind.Slapper:
                Show(user, L("highfive-offer-others", ("user", user)), L("highfive-offer-self"));
                break;
            case ImperialHandItemKind.Kisser:
                Show(user, L("kiss-offer-others", ("user", user)), L("kiss-offer-self"));
                break;
            case ImperialHandItemKind.Offer when helpUp:
                Show(user, L("helpup-offer-others", ("user", user), ("target", takers[0])), L("helpup-offer-self", ("target", takers[0])));
                break;
            default:
                Show(user, L("hand-offer-others", ("user", user)), L("hand-offer-self"));
                break;
        }
    }

    /// <summary>check_taker_in_range / check_owner_in_range / dropped_item.</summary>
    private void CheckOffer(EntityUid uid, ImperialEmoteOfferComponent offer)
    {
        if (_mobState.IsIncapacitated(uid))
        {
            EndOffer(uid, offer);
            return;
        }

        // Пятюню можно отдёрнуть: предложение остаётся ради «слишком медленно».
        var held = Exists(offer.Item) && _hands.IsHolding(uid, offer.Item);
        if (!held && offer.Kind != ImperialHandItemKind.Slapper)
        {
            EndOffer(uid, offer);
            return;
        }

        foreach (var taker in offer.Takers.ToArray())
        {
            if (Exists(taker) && !offer.TooSlowing.Contains(taker) && !CanTake(uid, taker, offer.HelpUp))
                RemoveTaker(uid, offer, taker);
        }

        if (offer.Takers.Count == 0)
            EndOffer(uid, offer);
    }

    private void RemoveTaker(EntityUid offerer, ImperialEmoteOfferComponent offer, EntityUid taker)
    {
        offer.Takers.Remove(taker);
        if (!Exists(taker))
            return;

        _alerts.ClearAlert(taker, offer.Alert);
        if (TryComp<ImperialEmoteTakerComponent>(taker, out var comp) && comp.Offerer == offerer)
            RemComp<ImperialEmoteTakerComponent>(taker);
    }

    private void EndOffer(EntityUid offerer, ImperialEmoteOfferComponent offer)
    {
        foreach (var taker in offer.Takers.ToArray())
            RemoveTaker(offerer, offer, taker);

        RemComp<ImperialEmoteOfferComponent>(offerer);
    }

    private void OnOfferAlert(ImperialEmoteOfferAlertEvent args)
    {
        var taker = args.User;
        if (!TryComp<ImperialEmoteTakerComponent>(taker, out var comp) ||
            !TryComp<ImperialEmoteOfferComponent>(comp.Offerer, out var offer) ||
            !offer.Takers.Contains(taker))
            return;

        args.Handled = true;
        var offerer = comp.Offerer;
        switch (offer.Kind)
        {
            case ImperialHandItemKind.Slapper:
                TakeHighFive(offerer, offer, taker);
                break;
            case ImperialHandItemKind.Kisser:
                Show(offerer, L("kiss-give-others", ("user", offerer), ("target", taker)), L("kiss-give-self", ("target", taker)),
                    taker, L("kiss-give-target", ("user", offerer)));
                FireKiss(offerer, Transform(taker).Coordinates, taker);
                QueueDel(offer.Item);
                EndOffer(offerer, offer);
                break;
            case ImperialHandItemKind.Offer:
                TakeHand(offerer, offer, taker);
                break;
        }
    }

    /// <summary>high_fiver: пятюня или «пять-на-пять» двумя руками; без свободной руки не выйдет.</summary>
    private void TakeHighFive(EntityUid offerer, ImperialEmoteOfferComponent offer, EntityUid taker)
    {
        if (offer.TooSlowing.Contains(taker))
            return;

        if (!Exists(offer.Item) || !_hands.IsHolding(offerer, offer.Item))
        {
            TooSlow(offerer, offer, taker);
            return;
        }

        if (_hands.CountFreeHands(taker) <= 0)
        {
            _popup.PopupEntity(L("highfive-full-hands", ("target", offerer)), taker, taker);
            return;
        }

        var slappers = 0;
        foreach (var held in _hands.EnumerateHeld(offerer))
        {
            if (HasComp<ImperialHandItemComponent>(held))
                slappers++;
        }

        var highTen = slappers >= 2;
        _audio.PlayPvs(SlapSound, offerer, AudioParams.Default.WithVolume(highTen ? 4f : 0f).WithVariation(0.125f));
        var key = highTen ? "highten" : "highfive";
        Show(offerer, L($"{key}-others", ("user", taker), ("target", offerer)), L($"{key}-self", ("target", taker)),
            taker, L($"{key}-target", ("target", offerer)));
        EndOffer(offerer, offer);
    }

    /// <summary>too_slow: пятюню отдёрнули в последний момент.</summary>
    private void TooSlow(EntityUid offerer, ImperialEmoteOfferComponent offer, EntityUid rube)
    {
        offer.TooSlowing.Add(rube);
        Show(offerer, L("tooslow-start-others", ("user", rube), ("target", offerer)), L("tooslow-start-self", ("target", rube)),
            rube, L("tooslow-start-target", ("target", offerer)));

        Schedule(0.5f, () =>
        {
            if (!Exists(offerer) || !Exists(rube))
                return;

            Show(offerer, L("tooslow-end-others", ("user", offerer), ("target", rube)), L("tooslow-end-self", ("target", rube)),
                rube, L("tooslow-end-target", ("target", offerer)), PopupType.LargeCaution);
            _audio.PlayPvs(WhooshSound, offerer);
            _stun.TryKnockdown(rube, TimeSpan.FromSeconds(1));
            if (TryComp<ImperialEmoteOfferComponent>(offerer, out var current))
                EndOffer(offerer, current);
        });
    }

    /// <summary>hand: помочь лежащему подняться или взять за руку и повести.</summary>
    private void TakeHand(EntityUid offerer, ImperialEmoteOfferComponent offer, EntityUid taker)
    {
        if (_standing.IsDown(taker))
        {
            if (!_standing.Stand(taker))
                return;

            Show(offerer, L("helpup-others", ("user", offerer), ("target", taker)), L("helpup-self", ("target", taker)),
                taker, L("helpup-target", ("user", offerer)));
            QueueDel(offer.Item);
            EndOffer(offerer, offer);
            return;
        }

        if (!_pulling.TryStartPull(offerer, taker))
            return;

        _popup.PopupEntity(L("hand-pull-self", ("target", taker)), offerer, offerer);
        _popup.PopupEntity(L("hand-pull-target", ("user", offerer)), taker, taker);
        QueueDel(offer.Item);
        EndOffer(offerer, offer);
    }

    #endregion

    #region Поцелуй

    private void FireKiss(EntityUid user, EntityCoordinates target, EntityUid? direct)
    {
        var kiss = Spawn(KissProjectile, Transform(user).Coordinates);
        EnsureComp<ImperialKissProjectileComponent>(kiss).Direct = direct != null;
        _meta.SetEntityName(kiss, L("kiss-name", ("user", user)));

        var from = _transform.GetMapCoordinates(user).Position;
        var direction = _transform.ToMapCoordinates(target).Position - from;
        if (direction.LengthSquared() < 0.01f)
            direction = _transform.GetWorldRotation(user).ToWorldVec();

        _gun.ShootProjectile(kiss, direction, Vector2.Zero, user, user, KissSpeed);
    }

    /// <summary>harmless_on_hit и try_fluster: стеснительных поцелуй выбивает из колеи.</summary>
    private void OnKissHit(Entity<ImperialKissProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;
        _audio.PlayPvs(KissSound, target);
        if (!HasComp<MobStateComponent>(target))
            return;

        if (!ent.Comp.Direct)
            _popup.PopupEntity(L("kiss-hit", ("kiss", ent.Owner), ("target", target)), target);

        if (!Factory.TryGetRegistration("ImperialQuirkSocialAnxiety", out var anxiety) || !HasComp(target, anxiety.Type) ||
            _mobState.IsIncapacitated(target) || _random.Prob(0.5f))
            return;

        var firer = args.Shooter;
        switch (_random.Next(1, 4))
        {
            case 1:
                _drunk.TryApplyDrunkenness(target, TimeSpan.FromSeconds(_random.Next(5, 11)));
                Show(target, L("fluster-1-others", ("user", target)), L("fluster-1-self"));
                break;
            case 2:
                _stutter.DoStutter(target, TimeSpan.FromSeconds(_random.Next(10, 31)), true);
                Show(target, L("fluster-2-others", ("user", target)), L("fluster-2-self"));
                var phrase = L($"fluster-say-{_random.Next(1, 6)}");
                Schedule(_random.NextFloat(0.5f, 1.5f), () =>
                {
                    if (Exists(target))
                        _chat.TrySendInGameICMessage(target, phrase, InGameICChatType.Speak, false);
                });
                break;
            default:
                _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(_random.Next(3, 9)));
                Show(target, L("fluster-3-others", ("user", target), ("target", firer ?? target)), L("fluster-3-self", ("target", firer ?? target)));
                break;
        }
    }

    #endregion

    #region Кольцо

    /// <summary>circlegame: кто вблизи посмотрел на кольцо — тот попался.</summary>
    private void OnCircleExamined(Entity<ImperialCircleOwnerComponent> ent, ref ExaminedEvent args)
    {
        var owner = ent.Owner;
        var sucker = args.Examiner;
        var item = ent.Comp.Item;
        if (!_interaction.InRangeUnobstructed(owner, sucker, OfferRange))
            return;

        Schedule(0.4f, () =>
        {
            if (!Exists(item) || !Exists(owner) || !Exists(sucker))
                return;

            if (owner == sucker)
            {
                _popup.PopupEntity(L("circle-self-notice"), owner, owner, PopupType.MediumCaution);
                Schedule(1f, () => SelfGottem(owner, item));
                return;
            }

            _popup.PopupEntity(L("circle-notice"), sucker, sucker, PopupType.MediumCaution);
            Schedule(0.6f, () => Gottem(owner, sucker, item));
        });
    }

    private void SelfGottem(EntityUid owner, EntityUid item)
    {
        if (!Exists(item) || !Exists(owner))
            return;

        _audio.PlayPvs(PunchSound, owner);
        Show(owner, L("circle-self-others", ("user", owner)), L("circle-self-self"));
        _stamina.TakeStaminaDamage(owner, 100);
        _stun.TryKnockdown(owner, TimeSpan.FromSeconds(1));
        QueueDel(item);
    }

    private void Gottem(EntityUid owner, EntityUid sucker, EntityUid item)
    {
        if (!Exists(sucker))
            return;

        if (!Exists(item) || !Exists(owner))
        {
            _popup.PopupEntity(L("circle-imagination"), sucker, sucker);
            return;
        }

        if (!_interaction.InRangeUnobstructed(owner, sucker, OfferRange) || _emote.HandsBlocked(owner))
        {
            _popup.PopupEntity(L("circle-escaped", ("user", owner)), sucker, sucker);
            return;
        }

        _audio.PlayPvs(PunchSound, owner);
        _popup.PopupEntity(L("circle-gottem-owner", ("target", sucker)), owner, owner);
        Show(owner, L("circle-gottem-others", ("user", owner), ("target", sucker)), L("circle-gottem-self", ("target", sucker)),
            sucker, L("circle-gottem-target", ("user", owner)));
        _stamina.TakeStaminaDamage(sucker, 15, source: owner);
        QueueDel(item);
    }

    #endregion

    #region Подзатыльник

    private void TryNoogie(EntityUid user, EntityUid target)
    {
        if (!_emote.IsKind(target, ImperialEmoteMob.Carbon))
        {
            _popup.PopupEntity(L("noogie-cant", ("target", target)), user, user);
            return;
        }

        if (HasComp<PacifiedComponent>(user))
        {
            _popup.PopupEntity(L("noogie-pacified", ("target", target)), user, user);
            return;
        }

        // Агрессивного захвата в SS14 нет: достаточно тащить цель.
        if (_pulling.GetPulling(user) != target)
        {
            _popup.PopupEntity(L("noogie-grab", ("target", target)), user, user);
            return;
        }

        if (Tired(user))
            return;

        var brutal = TryComp<HumanoidProfileComponent>(target, out var profile) && profile.Species == "Moth";
        var noogie = EnsureComp<ImperialNoogieComponent>(user);
        noogie.Iteration = 0;
        noogie.Brutal = brutal;

        var kind = brutal ? "brutal" : "rough";
        Show(user, L($"noogie-start-{kind}-others", ("user", user), ("target", target)), L($"noogie-start-{kind}-self", ("target", target)),
            target, L($"noogie-start-{kind}-target", ("user", user)), PopupType.MediumCaution);
        StartNoogie(user, target, TimeSpan.FromSeconds(1.5));
    }

    private bool Tired(EntityUid user)
    {
        return TryComp<StaminaComponent>(user, out var stamina) && stamina.StaminaDamage > 80;
    }

    private void StartNoogie(EntityUid user, EntityUid target, TimeSpan delay)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, new ImperialNoogieDoAfterEvent(), user, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    /// <summary>noogie_loop: урон по голове, крик от сильных, усталость растёт с каждым кругом.</summary>
    private void OnNoogieDoAfter(Entity<ImperialNoogieComponent> ent, ref ImperialNoogieDoAfterEvent args)
    {
        var user = ent.Owner;
        if (args.Target is not { } target)
            return;

        if (args.Cancelled || args.Handled)
        {
            if (args.Cancelled)
                Show(user, L("noogie-fail-others", ("user", user), ("target", target)), L("noogie-fail-self", ("target", target)),
                    target, L("noogie-fail-target", ("user", user)));
            RemComp<ImperialNoogieComponent>(user);
            return;
        }

        args.Handled = true;
        if (_pulling.GetPulling(user) != target)
        {
            RemComp<ImperialNoogieComponent>(user);
            return;
        }

        if (Tired(user))
        {
            _popup.PopupEntity(L("noogie-tired-self", ("target", target)), user, user);
            _popup.PopupEntity(L("noogie-tired-target", ("user", user)), target, target);
            RemComp<ImperialNoogieComponent>(user);
            return;
        }

        var damage = _random.Next(1, 6);
        if (ent.Comp.Brutal)
            damage += _random.Next(3, 8);

        if (damage >= 5)
            _emote.TryEmote(target, "scream");

        _damageable.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Blunt), damage), origin: user);
        _stamina.TakeStaminaDamage(user, ent.Comp.Iteration + 5);
        _audio.PlayPvs(RustleSound, user);

        if (_random.Prob(0.33f))
        {
            Show(user, L("noogie-continue-others", ("user", user), ("target", target)), L("noogie-continue-self", ("target", target)),
                target, L("noogie-continue-target", ("user", user)));
        }

        var delay = TimeSpan.FromSeconds(1 + ent.Comp.Iteration * 0.2);
        ent.Comp.Iteration++;
        StartNoogie(user, target, delay);
    }

    #endregion

    #region Кража обуви

    private void TrySteal(EntityUid user, EntityUid target)
    {
        if (!HasComp<HumanoidProfileComponent>(target))
            return;

        if (target == user)
        {
            _popup.PopupEntity(L("steal-self"), user, user);
            return;
        }

        if (!_inventory.TryGetSlotEntity(target, "shoes", out var shoes))
            return;

        // Красть обувь можно только лёжа, подкравшись снизу.
        if (!_standing.IsDown(user))
        {
            _popup.PopupEntity(L("steal-standing"), user, user);
            return;
        }

        Show(user, L("steal-start-others", ("user", user), ("target", target), ("item", shoes.Value)), L("steal-start-self", ("target", target), ("item", shoes.Value)),
            target, L("steal-start-target", ("user", user), ("item", shoes.Value)), PopupType.MediumCaution);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, StealDelay, new ImperialShoeStealDoAfterEvent(), user, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnShoeStealDoAfter(Entity<HandsComponent> ent, ref ImperialShoeStealDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        var user = ent.Owner;
        if (!_inventory.TryUnequip(user, target, "shoes", out var shoes, true, true))
            return;

        // Рука-воровка освобождает место для добычи.
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<ImperialHandItemComponent>(held, out var hand) && hand.Kind == ImperialHandItemKind.Stealer)
            {
                QueueDel(held);
                _hands.TryDrop(user, held, checkActionBlocker: false);
                break;
            }
        }

        _hands.TryPickupAnyHand(user, shoes.Value, checkActionBlocker: false);
        Show(user, L("steal-done-others", ("user", user), ("target", target), ("item", shoes.Value)), L("steal-done-self", ("target", target), ("item", shoes.Value)),
            target, L("steal-done-target", ("user", user), ("item", shoes.Value)), PopupType.MediumCaution);
    }

    #endregion
}
