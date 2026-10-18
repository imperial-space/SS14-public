using System.Linq;
using System.Numerics;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Рыбалка (/obj/item/fishing_rod + /datum/fishing_challenge): заброс, ожидание, поклёвка, мини-игра,
/// подтягивание зацепленных предметов, опыт навыка.
/// </summary>
public sealed partial class ImperialFishingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ImperialFishSourceSystem _sources = default!;
    [Dependency] private readonly ImperialFishingSpotSystem _spots = default!;
    [Dependency] private readonly ImperialFishingSkillSystem _skill = default!;
    [Dependency] private readonly ImperialFishTraitSystem _traits = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;
    [Dependency] private readonly ImperialFishingBaitSystem _bait = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;

    private const string FloatProto = "ImperialFishingFloat";
    private const string LineProto = "ImperialFishingLineEnd";

    private static readonly SoundSpecifier SplashSound = new SoundPathSpecifier("/Audio/Imperial/Fishing/splash.ogg");
    private static readonly SoundSpecifier BigSplashSound = new SoundPathSpecifier("/Audio/Imperial/Fishing/bigsplash.ogg");
    private static readonly SoundSpecifier FishSplashSound = new SoundPathSpecifier("/Audio/Imperial/Fishing/fish_splash.ogg");
    private static readonly SoundSpecifier ReelSound = new SoundCollectionSpecifier("ImperialFishingReel");
    private static readonly SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ping.ogg");

    /// <summary>GLOB.fishing_challenges_by_user.</summary>
    private readonly Dictionary<EntityUid, ImperialFishingChallenge> _challenges = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishingRodComponent, BeforeRangedInteractEvent>(OnRodInteract);
        SubscribeLocalEvent<ImperialFishingRodComponent, UseInHandEvent>(OnRodUseInHand, before: new[] { typeof(ItemToggleSystem) });
        SubscribeLocalEvent<ImperialFishingRodComponent, GotUnequippedHandEvent>(OnRodUnequipped);
        SubscribeLocalEvent<ImperialFishingRodComponent, ExaminedEvent>(OnRodExamined);
        SubscribeLocalEvent<ImperialFishingRodComponent, GetVerbsEvent<AlternativeVerb>>(OnRodVerbs);
        SubscribeLocalEvent<ImperialFishingRodComponent, ItemSlotInsertAttemptEvent>(OnSlotInsertAttempt);
        SubscribeLocalEvent<ImperialFishingRodComponent, ItemSlotEjectAttemptEvent>(OnSlotEjectAttempt);
        SubscribeLocalEvent<ImperialFishingRodComponent, InteractUsingEvent>(OnRodInteractUsing, before: new[] { typeof(ItemSlotsSystem) });
        SubscribeLocalEvent<ImperialFishingRodComponent, ImperialFishingReelDoAfterEvent>(OnReelDoAfter);
        SubscribeLocalEvent<ImperialFishingRodComponent, ImperialFishingStopDoAfterEvent>(OnStopDoAfter);
        SubscribeLocalEvent<ImperialFishingRodComponent, ImperialFishingReelMessage>(OnReelMessage);
        SubscribeLocalEvent<ImperialFishingRodComponent, ImperialFishingRodSlotMessage>(OnRodSlotMessage);
        SubscribeLocalEvent<ImperialFishingRodComponent, ImperialFishingGiveUpMessage>(OnGiveUpMessage);
        SubscribeLocalEvent<ImperialFishingRodComponent, EntityTerminatingEvent>(OnRodTerminating);

        SubscribeLocalEvent<ImperialBaitCanComponent, UseInHandEvent>(OnBaitCanUse, before: new[] { typeof(ItemToggleSystem) });
        SubscribeLocalEvent<ImperialBaitCanComponent, ExaminedEvent>(OnBaitCanExamined);

        InitializeVisuals();
    }

    #region Снаряжение

    /// <summary>Удочка со снаряжением.</summary>
    public ImperialFishingRodInfo GetRodInfo(Entity<ImperialFishingRodComponent> rod)
    {
        var line = _slots.GetItemOrNull(rod, ImperialFishingRodComponent.LineSlot);
        var hook = _slots.GetItemOrNull(rod, ImperialFishingRodComponent.HookSlot);
        var bait = _slots.GetItemOrNull(rod, ImperialFishingRodComponent.BaitSlot);
        return new ImperialFishingRodInfo(rod, rod.Comp, line, CompOrNull<ImperialFishingLineComponent>(line),
            hook, CompOrNull<ImperialFishingHookComponent>(hook), bait);
    }

    /// <summary>get_cast_range(): удочка + леска + навык.</summary>
    public int GetCastRange(ImperialFishingRodInfo rod, EntityUid? user)
    {
        var range = Math.Max(rod.Comp.CastRange + (rod.LineComp?.CastRange ?? 0), 1);
        if (user != null && _hands.IsHolding(user.Value, rod.Rod))
            range += (int) MathF.Round(_skill.GetLevel(user.Value) * 0.3f);

        return Math.Max(range, 1);
    }

    private void OnSlotInsertAttempt(Entity<ImperialFishingRodComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (IsBusy(ent, args.User) || args.Slot.ID == ImperialFishingRodComponent.BaitSlot && (ent.Comp.LockedBait || !_bait.IsBait(args.Item)))
            args.Cancelled = true;
    }

    private void OnSlotEjectAttempt(Entity<ImperialFishingRodComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (IsBusy(ent, args.User) || args.Slot.ID == ImperialFishingRodComponent.BaitSlot && ent.Comp.LockedBait)
            args.Cancelled = true;
    }

    private bool IsBusy(Entity<ImperialFishingRodComponent> rod, EntityUid? user)
    {
        return rod.Comp.Hooked != null || user != null && _challenges.ContainsKey(user.Value);
    }

    /// <summary>ui_data() окна снасти.</summary>
    public void UpdateRodUi(Entity<ImperialFishingRodComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ImperialFishingRodUiKey.Key))
            return;

        ImperialFishingRodSlotData Slot(string slot)
        {
            var item = _slots.GetItemOrNull(ent, slot);
            return new ImperialFishingRodSlotData
            {
                Name = item is { } uid ? Name(uid) : null,
                Proto = item is { } p ? Prototype(p)?.ID : null,
            };
        }

        var state = new ImperialFishingRodBoundUserInterfaceState
        {
            Bait = Slot(ImperialFishingRodComponent.BaitSlot),
            Line = Slot(ImperialFishingRodComponent.LineSlot),
            Hook = Slot(ImperialFishingRodComponent.HookSlot),
            Description = ent.Comp.UiDescription is { } desc ? Loc.GetString(desc) : string.Empty,
        };
        _ui.SetUiState(ent.Owner, ImperialFishingRodUiKey.Key, state);
    }

    /// <summary>use_slot(): пустая рука — вынуть, предмет в руке — вставить или поменять местами.</summary>
    private void OnRodSlotMessage(Entity<ImperialFishingRodComponent> ent, ref ImperialFishingRodSlotMessage args)
    {
        var user = args.Actor;
        var slot = args.Slot switch
        {
            "bait" => ImperialFishingRodComponent.BaitSlot,
            "line" => ImperialFishingRodComponent.LineSlot,
            "hook" => ImperialFishingRodComponent.HookSlot,
            _ => null,
        };

        if (slot == null || IsBusy(ent, user) || slot == ImperialFishingRodComponent.BaitSlot && ent.Comp.LockedBait)
            return;

        var held = _hands.GetActiveItem(user);
        if (held == ent.Owner)
            held = null;

        // Из банки наживки достаётся одна наживка.
        if (held is { } canUid && slot == ImperialFishingRodComponent.BaitSlot && TryComp<ImperialBaitCanComponent>(canUid, out var can))
        {
            if (RetrieveBait((canUid, can), user) is not { } bait)
                return;
            held = bait;
        }

        var current = _slots.GetItemOrNull(ent, slot);
        if (held == null)
        {
            if (current != null && _slots.TryEject(ent, slot, user, out var removed))
                _hands.PickupOrDrop(user, removed.Value);
        }
        else
        {
            if (!_slots.TryGetSlot(ent, slot, out var itemSlot) || !_slots.CanInsert(ent, held.Value, user, itemSlot, swap: true))
            {
                _popup.PopupEntity(Loc.GetString("imperial-fishing-slot-wrong"), ent, user);
                return;
            }

            EntityUid? old = null;
            if (current != null && _slots.TryEject(ent, slot, user, out var removed))
                old = removed;

            if (_slots.TryInsert(ent, slot, held.Value, user) && old != null)
                _hands.PickupOrDrop(user, old.Value);
        }

        UpdateRodUi(ent);
    }

    /// <summary>Банку наживки можно прямо применить к удочке.</summary>
    private void OnRodInteractUsing(Entity<ImperialFishingRodComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ImperialBaitCanComponent>(args.Used, out var can) || ent.Comp.LockedBait)
            return;

        args.Handled = true;
        if (IsBusy(ent, args.User) || _slots.GetItemOrNull(ent, ImperialFishingRodComponent.BaitSlot) != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-bait-occupied"), ent, args.User);
            return;
        }

        if (RetrieveBait((args.Used, can), args.User) is not { } bait)
            return;

        if (!_slots.TryInsert(ent, ImperialFishingRodComponent.BaitSlot, bait, args.User))
            _hands.PickupOrDrop(args.User, bait);
    }

    private void OnBaitCanUse(Entity<ImperialBaitCanComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (RetrieveBait(ent, args.User) is { } bait)
            _hands.PickupOrDrop(args.User, bait);
    }

    /// <summary>retrieve_bait().</summary>
    private EntityUid? RetrieveBait(Entity<ImperialBaitCanComponent> can, EntityUid user)
    {
        if (can.Comp.Uses <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-bait-can-empty"), can, user);
            return null;
        }

        if (_timing.CurTime < can.Comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-bait-can-wait"), can, user);
            return null;
        }

        can.Comp.NextUse = _timing.CurTime + can.Comp.Cooldown;
        can.Comp.Uses--;
        return Spawn(can.Comp.Bait, Transform(user).Coordinates);
    }

    private void OnBaitCanExamined(Entity<ImperialBaitCanComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Uses > 0
            ? Loc.GetString("imperial-fishing-bait-can-examine", ("count", ent.Comp.Uses))
            : Loc.GetString("imperial-fishing-bait-can-examine-empty"));
    }

    private void OnRodExamined(Entity<ImperialFishingRodComponent> ent, ref ExaminedEvent args)
    {
        var rod = GetRodInfo(ent);
        var parts = new List<string>();
        if (rod.Line != null)
            parts.Add(Name(rod.Line.Value));
        if (rod.Hook != null)
            parts.Add(Name(rod.Hook.Value));
        if (rod.Bait != null)
            parts.Add(Name(rod.Bait.Value));

        if (parts.Count > 0)
            args.PushMarkup(Loc.GetString("imperial-fishing-rod-equipped", ("parts", string.Join(", ", parts))));
        if (rod.Bait == null)
            args.PushMarkup(Loc.GetString("imperial-fishing-rod-no-bait"));

        if (!_skill.CanExamineFish(args.Examiner))
            return;

        var percent = _skill.CanExamineDeeper(args.Examiner);
        args.PushMarkup(Loc.GetString("imperial-fishing-rod-cast-range", ("range", GetCastRange(rod, args.Examiner))));
        PushStat(args, percent, ent.Comp.DifficultyModifier * 0.01f, "difficulty", lessIsBetter: true);
        PushStat(args, percent, ent.Comp.ExperienceMultiplier - 1, "experience");
        PushStat(args, percent, ent.Comp.CompletionSpeedMult - 1, "completion");
        PushStat(args, percent, ent.Comp.BaitSpeedMult - 1, "reeling");
        PushStat(args, percent, ent.Comp.DecelerationMult - 1, "deceleration");
        PushStat(args, percent, ent.Comp.BouncinessMult - 1, "bounciness", lessIsBetter: true);
        PushStat(args, percent, ent.Comp.GravityMult - 1, "gravity", info: true);

        if (rod.HookComp?.AttractShiny == true)
            args.PushMarkup(Loc.GetString("imperial-fishing-rod-shiny"));
        if (rod.HookComp?.RemoveDud == true)
            args.PushMarkup(Loc.GetString("imperial-fishing-rod-no-dud"));
        if (rod.LineComp?.LavaUsable == true)
            args.PushMarkup(Loc.GetString("imperial-fishing-rod-lava"));
    }

    /// <summary>get_stat_info().</summary>
    private void PushStat(ExaminedEvent args, bool percent, float value, string stat, bool lessIsBetter = false, bool info = false)
    {
        if (MathF.Abs(value) < 0.001f)
            return;

        var good = lessIsBetter ? value < 0 : value > 0;
        var amount = percent ? $"{MathF.Round(MathF.Abs(value) * 100)}% " : "";
        var text = Loc.GetString($"imperial-fishing-rod-stat-{stat}-{(value > 0 ? "more" : "less")}", ("amount", amount));
        var color = info ? "lightblue" : good ? "lime" : "red";
        args.PushMarkup($"[color={color}]{text}[/color]");
    }

    #endregion

    #region Заброс и подтягивание

    private void OnRodInteract(Entity<ImperialFishingRodComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        // Во время рыбалки любой клик удочкой — подсечка или кручение блесны.
        if (_challenges.TryGetValue(user, out var challenge) && challenge.Rod == ent.Owner)
        {
            args.Handled = true;
            HandleClick(challenge);
            return;
        }

        var rod = GetRodInfo(ent);
        if (rod.Hook == null)
        {
            // Без крючка удочка работает как обычный предмет вблизи.
            if (!args.CanReach)
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("imperial-fishing-need-hook"), user, user);
            }

            return;
        }

        if (ent.Comp.Hooked != null)
        {
            args.Handled = true;
            StartReel(ent, user);
            return;
        }

        // Вблизи сначала рыбалка в месте ловли, иначе обычное взаимодействие.
        var spot = _spots.FindSpot(args.Target, args.ClickLocation, out var source);
        if (args.CanReach && spot == null)
            return;

        args.Handled = true;
        CastLine(ent, rod, user, args.Target, args.ClickLocation, spot, source);
    }

    /// <summary>
    /// profound_fisher: рыбалка пустой рукой встроенной удочкой (перчатки, модуль МОДа).
    /// Во время рыбалки клик — подсечка. Возвращает false, если цель не место ловли.
    /// </summary>
    public bool TryProfoundFish(EntityUid rodUid, EntityUid user, EntityUid target)
    {
        if (!TryComp<ImperialFishingRodComponent>(rodUid, out var rodComp))
            return false;

        if (_challenges.TryGetValue(user, out var challenge))
        {
            if (challenge.Rod != rodUid)
                return false;

            HandleClick(challenge);
            return true;
        }

        var spot = _spots.FindSpot(target, Transform(target).Coordinates, out var source);
        if (spot == null)
            return false;

        var ent = (rodUid, rodComp);
        CastLine(ent, GetRodInfo(ent), user, target, Transform(target).Coordinates, spot, source);
        return true;
    }

    /// <summary>cast_line(): заброс в место ловли или зацеп предмета.</summary>
    private void CastLine(Entity<ImperialFishingRodComponent> ent, ImperialFishingRodInfo rod, EntityUid user, EntityUid? target,
        EntityCoordinates click, EntityUid? spot, ImperialFishSourcePrototype? source)
    {
        if (_timing.CurTime < ent.Comp.NextCast)
            return;

        if (ent.Comp.RequiresToggle && !_toggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-extend-first"), user, user);
            return;
        }

        var castRange = GetCastRange(rod, user);
        var targetCoords = spot != null ? Transform(spot.Value).Coordinates : target != null ? Transform(target.Value).Coordinates : click;
        if (!_transform.InRange(targetCoords, Transform(user).Coordinates, castRange + 0.5f))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-too-far"), user, user);
            return;
        }

        ent.Comp.NextCast = _timing.CurTime + TimeSpan.FromSeconds(1);

        // Леска не проходит сквозь стены, кроме блюспейс-лески.
        if (!rod.LineHas(ImperialFishingLineTraits.Phase)
            && !_examine.InRangeUnOccluded(user, targetCoords, castRange + 0.5f, e => e == user || e == spot || e == target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-blocked"), user, user);
            return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg"), user, AudioParams.Default.WithVolume(-4));

        if (spot != null && source != null)
        {
            TryStartFishing(ent, rod, user, spot.Value, source);
            return;
        }

        if (target != null)
            HookEntity(ent, rod, user, target.Value);
    }

    /// <summary>hook_item(): зацепить предмет или (крючком спасателя/капканом) существо.</summary>
    private void HookEntity(Entity<ImperialFishingRodComponent> ent, ImperialFishingRodInfo rod, EntityUid user, EntityUid target)
    {
        if (target == user)
            return;

        var canHook = HasComp<ItemComponent>(target) && !_container.IsEntityInContainer(target)
                      || HasComp<MobStateComponent>(target) && rod.HookComp?.HookMobs == true;
        if (!canHook)
            return;

        ent.Comp.Hooked = target;
        ent.Comp.LineVisual = SpawnLine(user, target);
        Dirty(ent);
    }

    /// <summary>Конец лески, привязанный к цели и рыбаку визуалом соединения.</summary>
    private EntityUid SpawnLine(EntityUid user, EntityUid target)
    {
        var line = SpawnAttachedTo(LineProto, new EntityCoordinates(target, Vector2.Zero));
        if (TryComp<Content.Shared.Physics.JointVisualsComponent>(line, out var visuals))
        {
            visuals.Target = user;
            Dirty(line, visuals);
        }

        return line;
    }

    private void ClearHooked(Entity<ImperialFishingRodComponent> ent)
    {
        if (ent.Comp.LineVisual is { } line && !TerminatingOrDeleted(line))
            QueueDel(line);

        ent.Comp.LineVisual = null;
        ent.Comp.Hooked = null;
        Dirty(ent);
    }

    /// <summary>reel(): подтянуть зацепленное на шаг.</summary>
    private void StartReel(Entity<ImperialFishingRodComponent> ent, EntityUid user)
    {
        if (ent.Comp.Hooked is not { } hooked || TerminatingOrDeleted(hooked))
        {
            ClearHooked(ent);
            return;
        }

        _audio.PlayPvs(ReelSound, ent);
        var time = MathF.Max(0.8f - MathF.Round(_skill.GetLevel(user) * 0.04f, 1), 0.1f) * ent.Comp.BaitSpeedMult;
        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(time), new ImperialFishingReelDoAfterEvent(), ent, target: hooked, used: ent)
        {
            BreakOnMove = false,
            BreakOnHandChange = true,
            DistanceThreshold = 100f,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnReelDoAfter(Entity<ImperialFishingRodComponent> ent, ref ImperialFishingReelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Hooked is not { } hooked || TerminatingOrDeleted(hooked))
            return;

        args.Handled = true;
        var user = args.User;
        var rod = GetRodInfo(ent);

        if (Transform(hooked).Anchored)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-wont-budge", ("target", hooked)), user, user);
            return;
        }

        // Около тридцати минут непрерывного подтягивания от нуля до мастера.
        var time = MathF.Max(0.8f - MathF.Round(_skill.GetLevel(user) * 0.04f, 1), 0.1f) * ent.Comp.BaitSpeedMult;
        _skill.AddExperience(user, time * 10 * 0.13f * ent.Comp.ExperienceMultiplier);

        var userPos = _transform.GetWorldPosition(user);
        var hookedPos = _transform.GetWorldPosition(hooked);
        var delta = userPos - hookedPos;
        var isItem = HasComp<ItemComponent>(hooked);

        if (!isItem && delta.Length() < 1.5f)
        {
            ClearHooked(ent);
            return;
        }

        var step = delta.Length() <= 1 ? delta : Vector2.Normalize(delta);
        var newPos = hookedPos + step;
        if (!rod.LineHas(ImperialFishingLineTraits.Phase)
            && !_interaction.InRangeUnobstructed(hooked, new MapCoordinates(newPos, Transform(hooked).MapID), 1.5f, CollisionGroup.Impassable))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-wont-budge", ("target", hooked)), user, user);
            return;
        }

        _transform.SetWorldPosition(hooked, newPos);

        if (!isItem)
        {
            if ((userPos - newPos).Length() <= 1.5f)
                ClearHooked(ent);
            return;
        }

        if ((userPos - newPos).Length() < 0.5f)
        {
            ClearHooked(ent);
            _hands.PickupOrDrop(user, hooked);
        }
    }

    private void OnRodVerbs(Entity<ImperialFishingRodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // attack_self_secondary(): окно снасти.
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-fishing-verb-tackle"),
            Priority = 1,
            Act = () =>
            {
                _ui.OpenUi(ent.Owner, ImperialFishingRodUiKey.Key, user);
                UpdateRodUi(ent);
            },
        });

        if (ent.Comp.Hooked == null)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-fishing-verb-unhook"),
            Act = () => ClearHooked(ent),
        });
    }

    #endregion

    #region Рыбалка

    /// <summary>try_start_fishing().</summary>
    private void TryStartFishing(Entity<ImperialFishingRodComponent> ent, ImperialFishingRodInfo rod, EntityUid user, EntityUid spot, ImperialFishSourcePrototype source)
    {
        if (_challenges.ContainsKey(user))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-already"), user, user);
            return;
        }

        if (ReasonWeCantFish(rod, source, spot) is { } reason)
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user);
            return;
        }

        var challenge = new ImperialFishingChallenge
        {
            User = user,
            Rod = ent,
            Location = spot,
            Source = source.ID,
            Background = source.Background,
        };

        if (source.WaitTimeRange is { } range)
            challenge.WaitTimeRange = range;

        challenge.SpinFrequency = CompOrNull<ImperialFishingBaitComponent>(rod.Bait)?.SpinFrequency;
        if (challenge.SpinFrequency != null)
            challenge.WaitTimeRange = new Vector2(challenge.WaitTimeRange.X + 8, challenge.WaitTimeRange.Y - 8);

        challenge.Setup(ent.Comp, rod.LineComp, rod.HookComp, _skill.GetValueModifier(user));

        var coords = Transform(spot).Coordinates;
        challenge.Float = Spawn(FloatProto, coords);
        if (TryComp<ImperialFishingFloatComponent>(challenge.Float, out var floatComp))
        {
            floatComp.HasLure = challenge.SpinFrequency != null;
            Dirty(challenge.Float, floatComp);
        }

        if (TryComp<Content.Shared.Physics.JointVisualsComponent>(challenge.Float, out var visuals))
        {
            visuals.Target = user;
            Dirty(challenge.Float, visuals);
        }

        _challenges[user] = challenge;
        foreach (var effect in new[] { ImperialFishingRules.Antigrav, ImperialFishingRules.Flip, ImperialFishingRules.Camo })
        {
            if ((challenge.SpecialEffects & effect) != 0)
                challenge.ActiveEffects.Add(effect);
        }

        _popup.PopupEntity(Loc.GetString("imperial-fishing-start"), user, user);
        _audio.PlayPvs(SplashSound, spot);
        StartBaitingPhase(challenge, false);
    }

    /// <summary>reason_we_cant_fish().</summary>
    private string? ReasonWeCantFish(ImperialFishingRodInfo rod, ImperialFishSourcePrototype source, EntityUid spot)
    {
        if (rod.HookComp?.OnlyChasm == true && !source.Chasm)
            return "imperial-fishing-rescue-hook-only";

        if (source.RequiresLavaLine && rod.LineComp?.LavaUsable != true)
            return "imperial-fishing-need-lava-line";

        if (_sources.SpecialReasonCantFish(source, spot) is { } special)
            return special;

        return null;
    }

    /// <summary>start_baiting_phase().</summary>
    private void StartBaitingPhase(ImperialFishingChallenge challenge, bool penalty)
    {
        challenge.RewardProto = null;
        challenge.RewardEntity = null;
        challenge.LastBaitingClick = _timing.CurTime;

        TimeSpan wait;
        if (penalty)
        {
            var left = challenge.NextPhaseAt - _timing.CurTime;
            if (left < TimeSpan.Zero)
                left = TimeSpan.Zero;
            wait = TimeSpan.FromSeconds(Math.Min(left.TotalSeconds + _random.Next(3, 6), 30));
        }
        else
        {
            wait = TimeSpan.FromSeconds(_random.NextFloat(challenge.WaitTimeRange.X, challenge.WaitTimeRange.Y));
            if ((challenge.SpecialEffects & ImperialFishingRules.Autoreel) != 0 && wait >= TimeSpan.FromSeconds(15))
                wait = TimeSpan.FromSeconds(Math.Max(wait.TotalSeconds - 7.5, 15));
        }

        challenge.Phase = ImperialFishingPhase.Wait;
        challenge.NextPhaseAt = _timing.CurTime + wait;
        SetFloat(challenge, biting: false, spinReady: false);

        if (challenge.SpinFrequency != null)
            SetLureTimers(challenge);
    }

    /// <summary>set_lure_timers().</summary>
    private void SetLureTimers(ImperialFishingChallenge challenge)
    {
        challenge.SpinReady = false;
        challenge.SpinReadyAt = _timing.CurTime + TimeSpan.FromSeconds(challenge.SpinFrequency!.Value.X);
        challenge.SpinMissAt = _timing.CurTime + TimeSpan.FromSeconds(challenge.SpinFrequency.Value.Y);
        SetFloat(challenge, false, false);
    }

    private void SetFloat(ImperialFishingChallenge challenge, bool biting, bool spinReady)
    {
        if (!TryComp<ImperialFishingFloatComponent>(challenge.Float, out var comp))
            return;

        comp.Biting = biting;
        comp.SpinReady = spinReady;
        Dirty(challenge.Float, comp);
    }

    /// <summary>start_biting_phase(): бросок награды и поклёвка.</summary>
    private void StartBitingPhase(ImperialFishingChallenge challenge)
    {
        challenge.Phase = ImperialFishingPhase.Biting;
        var source = _proto.Index(challenge.Source);
        var rod = GetRodInfo((challenge.Rod, Comp<ImperialFishingRodComponent>(challenge.Rod)));

        var ev = new ImperialFishingRollRewardEvent(challenge, rod);
        RaiseLocalEvent(challenge.Location, ref ev);
        if (ev.RewardEntity != null)
            challenge.RewardEntity = ev.RewardEntity;
        else
            challenge.RewardProto = ev.RewardProto ?? _sources.RollReward(source, rod, challenge.User, challenge.Location);

        _audio.PlayPvs(FishSplashSound, challenge.Location);

        if (_skill.CanRevealFish(challenge.User))
        {
            challenge.FishIcon = GetFishIcon(challenge);
            _popup.PopupEntity(Loc.GetString($"imperial-fishing-reveal-{challenge.FishIcon.ToString().ToLowerInvariant()}"), challenge.Location, challenge.User, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity("!!!", challenge.Location, challenge.User, PopupType.Medium);
        }

        if ((challenge.SpecialEffects & ImperialFishingRules.Autoreel) != 0)
            challenge.AutoStartAt = _timing.CurTime + TimeSpan.FromSeconds(0.2);

        challenge.NextPhaseAt = _timing.CurTime + ImperialFishingChallenge.BitingTimeWindow;
        SetFloat(challenge, true, true);
    }

    /// <summary>GLOB.specific_fish_icons.</summary>
    private ImperialFishIcon GetFishIcon(ImperialFishingChallenge challenge)
    {
        if (challenge.RewardEntity is { } entity && TryComp<ImperialFishComponent>(entity, out var instance))
            return instance.Icon;

        if (challenge.RewardProto is not { } reward || reward == ImperialFishing.Dud)
            return ImperialFishIcon.Fish;

        if (_data.GetFish(reward) is { } fish)
            return fish.Icon;

        if (_proto.TryIndex<ImperialFishingIconsPrototype>("Default", out var icons))
        {
            foreach (var (proto, icon) in icons.Entities)
            {
                if (_bait.IsOrInherits(reward, proto))
                    return icon;
            }

            if (icons.Specials.TryGetValue(reward, out var special))
                return special;
        }

        return ImperialFishIcon.Fish;
    }

    /// <summary>handle_click(): клик во время рыбалки.</summary>
    private void HandleClick(ImperialFishingChallenge challenge)
    {
        switch (challenge.Phase)
        {
            case ImperialFishingPhase.Wait:
                if (_timing.CurTime < challenge.LastBaitingClick + TimeSpan.FromSeconds(0.25))
                    return;

                if (challenge.SpinFrequency != null)
                {
                    if (!challenge.SpinReady)
                    {
                        Alert(challenge, "imperial-fishing-too-early");
                        StartBaitingPhase(challenge, true);
                    }
                    else
                    {
                        Alert(challenge, "imperial-fishing-spun");
                        challenge.LastBaitingClick = _timing.CurTime;
                    }

                    challenge.SpinReady = false;
                    SetLureTimers(challenge);
                }
                else
                {
                    Alert(challenge, "imperial-fishing-miss");
                    StartBaitingPhase(challenge, true);
                }

                break;

            case ImperialFishingPhase.Biting:
                StartMinigame(challenge, false);
                break;
        }
    }

    private void Alert(ImperialFishingChallenge challenge, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), challenge.Location, challenge.User);
    }

    /// <summary>Использование удочки в руке во время рыбалки — прекратить (во время вываживания — 3 секунды).</summary>
    private void OnRodUseInHand(Entity<ImperialFishingRodComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_challenges.TryGetValue(args.User, out var challenge) || challenge.Rod != ent.Owner)
        {
            // Телескопическую удочку нельзя сложить во время рыбалки.
            if (!args.Handled && ent.Comp.RequiresToggle && (ent.Comp.Hooked != null || _challenges.ContainsKey(args.User)) && _toggle.IsActivated(ent.Owner))
                args.Handled = true;
            return;
        }

        args.Handled = true;
        if (challenge.Phase != ImperialFishingPhase.Minigame)
        {
            challenge.ExperienceMultiplier *= 0.5f;
            Alert(challenge, "imperial-fishing-stopped");
            Complete(challenge, false);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new ImperialFishingStopDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnStopDoAfter(Entity<ImperialFishingRodComponent> ent, ref ImperialFishingStopDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_challenges.TryGetValue(args.User, out var challenge) || challenge.Completed)
            return;

        args.Handled = true;
        challenge.ExperienceMultiplier *= 0.5f;
        Alert(challenge, "imperial-fishing-stopped");
        Complete(challenge, false);
    }

    private void OnRodUnequipped(Entity<ImperialFishingRodComponent> ent, ref GotUnequippedHandEvent args)
    {
        ClearHooked(ent);
        if (_challenges.TryGetValue(args.User, out var challenge) && challenge.Rod == ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-rod-dropped"), args.User, args.User);
            Interrupt(challenge);
        }
    }

    private void OnRodTerminating(Entity<ImperialFishingRodComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var challenge in _challenges.Values.Where(c => c.Rod == ent.Owner).ToList())
            Interrupt(challenge);
    }

    /// <summary>interrupt(): прервано извне.</summary>
    public void Interrupt(ImperialFishingChallenge challenge)
    {
        if (challenge.Completed)
            return;

        challenge.ExperienceMultiplier *= 0.5f;
        Complete(challenge, false);
    }

    /// <summary>Прервать рыбалку пользователя (источник исчез, рыба ушла и т. п.).</summary>
    public void InterruptUser(EntityUid user, string? reason = null)
    {
        if (!_challenges.TryGetValue(user, out var challenge))
            return;

        if (reason != null)
            Alert(challenge, reason);

        Interrupt(challenge);
    }

    #endregion
}

/// <summary>
/// Источник с особой логикой (аквариум, портал и т. п.) сам выбирает награду (COMSIG_FISHING_CHALLENGE_ROLL_REWARD).
/// </summary>
[ByRefEvent]
public record struct ImperialFishingRollRewardEvent(ImperialFishingChallenge Challenge, ImperialFishingRodInfo Rod, string? RewardProto = null, EntityUid? RewardEntity = null);
