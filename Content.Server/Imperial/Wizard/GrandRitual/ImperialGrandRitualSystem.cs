using System.Linq;
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Station.Systems;
using Content.Server.StationEvents;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.GrandRitual;

/// <summary>Состояние Великого ритуала мага (/datum/action/cooldown/grand_ritual) — живёт на разуме.</summary>
[RegisterComponent]
public sealed partial class ImperialGrandRitualMindComponent : Component
{
    /// <summary>times_completed.</summary>
    [ViewVariables]
    public int TimesCompleted;

    /// <summary>target_area: маяк узла силы.</summary>
    [ViewVariables]
    public EntityUid? TargetBeacon;

    [ViewVariables]
    public string TargetName = string.Empty;

    /// <summary>drew_finale.</summary>
    [ViewVariables]
    public bool DrewFinale;

    /// <summary>drawing_rune.</summary>
    [ViewVariables]
    public bool Drawing;

    /// <summary>total_cheese_sacrificed.</summary>
    [ViewVariables]
    public int CheeseSacrificed;

    [ViewVariables]
    public bool CheeseGoalMet;

    /// <summary>rune: руна, нарисованная для текущего узла.</summary>
    [ViewVariables]
    public EntityUid? Rune;

    [ViewVariables]
    public EntityUid? Action;
}

/// <summary>/obj/effect/grand_rune.</summary>
[RegisterComponent]
public sealed partial class ImperialGrandRuneComponent : Component
{
    /// <summary>potency: сколько ритуалов уже завершено.</summary>
    [ViewVariables]
    public int Potency;

    [ViewVariables]
    public TimeSpan InvokeTime;

    [ViewVariables]
    public bool InUse;

    [ViewVariables]
    public int TimesInvoked;

    [ViewVariables]
    public int CheeseSacrificed;

    [ViewVariables]
    public string[] MagicWords = Array.Empty<string>();

    /// <summary>Разум мага, чей это ритуал.</summary>
    [ViewVariables]
    public EntityUid Ritual;

    /// <summary>/obj/effect/grand_rune/finale.</summary>
    [DataField]
    public bool Finale;

    /// <summary>/obj/effect/grand_rune/finale/cheesy.</summary>
    [DataField]
    public bool Cheesy;

    [ViewVariables]
    public bool ChosenEffect;

    [ViewVariables]
    public string? FinaleEffect;

    [ViewVariables]
    public int DireWarningsGiven;

    [DataField]
    public string StatePrefix = "rune";
}

/// <summary>Цель «Завершите Великий ритуал не менее семи раз» — выдаётся уже выполненной.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardRitualConditionComponent : Component;

/// <summary>
/// Великий ритуал — альтернативная победа мага: семь рун в случайных узлах силы станции,
/// каждая вызывает событие и местный побочный эффект, седьмая позволяет выбрать гранд-финал.
/// </summary>
public sealed partial class ImperialGrandRitualSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly EventManagerSystem _events = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly ImperialWizardTeleportSystem _teleport = default!;
    [Dependency] private readonly ImperialWizardRuleSystem _wizard = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly EntProtoId ActionProto = "ImperialActionGrandRitual";
    private static readonly EntProtoId RuneProto = "ImperialGrandRune";
    private static readonly EntProtoId FinaleRuneProto = "ImperialGrandRuneFinale";
    private static readonly EntProtoId CheesyRuneProto = "ImperialGrandRuneCheesy";
    private static readonly EntProtoId RitualObjective = "ImperialWizardRitualObjective";
    private static readonly EntProtoId CheeseWheel = "FoodCheese";

    /// <summary>GRAND_RITUAL_FINALE_COUNT.</summary>
    public const int FinaleCount = 7;

    /// <summary>GRAND_RITUAL_RUNES_WARNING_POTENCY.</summary>
    public const int WarningPotency = 3;

    /// <summary>GRAND_RITUAL_IMMINENT_FINALE_POTENCY.</summary>
    public const int ImminentPotency = 6;

    /// <summary>CHEESE_SACRIFICE_GOAL.</summary>
    public const int CheeseGoal = 50;

    /// <summary>GRAND_RUNE_INVOKES_TO_COMPLETE.</summary>
    private const int InvokesToComplete = 3;

    private static readonly TimeSpan BaseInvokeTime = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan AddInvokeTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DrawTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan RitualCooldown = TimeSpan.FromMinutes(2);

    /// <summary>Радиус «зоны» узла силы вокруг его маяка.</summary>
    private const float AreaRadius = 7f;

    /// <summary>possible_magic_words.</summary>
    private static readonly string[][] PossibleMagicWords =
    {
        new[] { "А-А-А-А!!", "А-А-А-А-А!!", "А-А-А-А-А-А-А!!" },
        new[] { "Абра...", "Кадабра...", "Алаказам!" },
        new[] { "Азарат!", "Метрион!", "Зинтрос!!" },
        new[] { "Бибити!", "Бобити!", "Буу!" },
        new[] { "Биш", "Баш", "Бош!" },
        new[] { "Эни... ", "Мени... ", "Миниму!!" },
        new[] { "Эсальп!", "Оу Натх!", "Ем Есукзме!!" },
        new[] { "Фус", "Ро", "Да!!" },
        new[] { "git checkout origin master", "git reset --hard HEAD~2", "git push origin master --force!!" },
        new[] { "Фокус Покус!", "Флим Флам!", "Вабаджак!" },
        new[] { "Я желаю...", "Я бы хотел...", "Желание, которое я загадаю сегодня вечером!" },
        new[] { "Клату!", "Барада!", "Никто!!" },
        new[] { "Пусть расширяются просторы!", "Пусть эон станет мгновением!", "Распахните ворота настежь!!" },
        new[] { "Левиос!", "Гравиоле!", "Експломб!!" },
        new[] { "Микрато", "Рапиж", "Сактонишь!" },
        new[] { "Нокту!", "Орфей!", "Ауде! Фраетор!" },
        new[] { "Квуас!", "Векс!", "Ексорт!" },
        new[] { "Сим!", "Сала!", "Бим!" },
        new[] { "Семь теней отброшены, семь судеб предсказаны!", "Пусть их слова отзвучат в вашей пустой душе!", "Разрушение наступило!!" },
        new[]
        {
            "Свифткаст! Хастега! Ограждающее поле 2! Продление 4! Продвинутое усиление Тенсера! Защита от добра! Усиление эффекта 3! Аркановое ус...",
            "...иление 4! Превращение паразитов 10! Защита от зла 2! Щит мага! Посредственное усиление Венериуса 2! Расширить силу! Изгнать го...",
            "...лод 2! Защита от нейтралов! Сурекастага! Обновление! Обновление 2! Шарпкаст 10! Эфирная манипуляция! Поглощение Лей-линии! Вызвать Великий Ритуал!!",
        },
        new[] { "Тен!", "Чи!", "Джи!" },
        new[] { "Высшая школа магии!", "Ультимативный ритуал!", "Макрокосмос!!" },
        new[] { "Яаба", "Да Ба", "Дуум!!" },
    };

    private static readonly string[] CheesyWords = { "Здравствуйте! Приветствую!", "Добро пожаловать! А теперь уходи.", "Уходи. Беги. Или умри." };

    /// <summary>Отложенные действия (addtimer).</summary>
    private readonly List<(TimeSpan At, Action Action)> _scheduled = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialGrandRitualActionEvent>(OnAction);
        SubscribeLocalEvent<ImperialGrandRuneDrawDoAfterEvent>(OnDrawDoAfter);
        SubscribeLocalEvent<ImperialGrandRuneComponent, InteractHandEvent>(OnRuneInteract);
        SubscribeLocalEvent<ImperialGrandRuneComponent, ImperialGrandRuneInvokeDoAfterEvent>(OnInvokeDoAfter);
        SubscribeLocalEvent<ImperialGrandRuneComponent, ExaminedEvent>(OnRuneExamined);
        SubscribeLocalEvent<ImperialWizardRitualConditionComponent, ObjectiveGetProgressEvent>((_, _, args) => args.Progress = 1f);
        InitializeFinales();
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _scheduled.Clear();
            _immortality = false;
        });
    }

    /// <summary>Запланировать действие через задержку (addtimer).</summary>
    public void Schedule(TimeSpan delay, Action action)
    {
        _scheduled.Add((_timing.CurTime + delay, action));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_scheduled.Count == 0)
            return;

        var now = _timing.CurTime;
        var due = _scheduled.Where(s => s.At <= now).ToList();
        _scheduled.RemoveAll(s => s.At <= now);
        foreach (var (_, action) in due)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Error($"Ошибка отложенного действия Великого ритуала: {e}");
            }
        }
    }

    #region Действие

    /// <summary>assign_ritual() + Grant(): действие живёт на разуме и переходит вместе с ним.</summary>
    public void AssignRitual(EntityUid body, EntityUid mindId)
    {
        var ritual = EnsureComp<ImperialGrandRitualMindComponent>(mindId);
        if (ritual.Action == null || TerminatingOrDeleted(ritual.Action))
        {
            ritual.Action = _actionContainer.AddAction(mindId, ActionProto);
            if (ritual.Action is { } action)
                _actions.GrantContainedAction(body, mindId, action);
        }

        if (ritual.TargetBeacon == null)
            SetNewArea(mindId, ritual);
    }

    private void OnAction(ImperialGrandRitualActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        if (!_mind.TryGetMind(user, out var mindId, out _) || !TryComp<ImperialGrandRitualMindComponent>(mindId, out var ritual))
            return;

        // IsAvailable(): «не достаёт до пола!»
        if (Transform(user).ParentUid != Transform(user).GridUid)
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-no-floor"), user, user);
            return;
        }

        args.Handled = true;
        ValidateArea(mindId, ritual);
        if (InTargetArea(user, ritual))
            StartDrawingRune(user, mindId, ritual);
        else
            PinpointArea(user, ritual);
    }

    /// <summary>validate_area(): узел исчез — выбрать другой.</summary>
    private void ValidateArea(EntityUid mindId, ImperialGrandRitualMindComponent ritual)
    {
        if (ritual.TargetBeacon is not { } beacon || TerminatingOrDeleted(beacon))
            SetNewArea(mindId, ritual);
    }

    /// <summary>set_new_area(): случайный узел силы среди зон станции.</summary>
    private void SetNewArea(EntityUid mindId, ImperialGrandRitualMindComponent ritual)
    {
        var areas = _teleport.GetAreas();
        if (areas.Count == 0)
        {
            ritual.TargetBeacon = null;
            ritual.TargetName = string.Empty;
            return;
        }

        var (name, beacon) = _random.Pick(areas.ToList());
        ritual.TargetBeacon = beacon;
        ritual.TargetName = name;
        Notify(mindId, Loc.GetString("imperial-grand-ritual-next-area", ("area", name)));
    }

    private bool InTargetArea(EntityUid user, ImperialGrandRitualMindComponent ritual)
    {
        if (ritual.TargetBeacon is not { } beacon)
            return false;

        var userXform = Transform(user);
        var beaconXform = Transform(beacon);
        if (userXform.GridUid == null || userXform.GridUid != beaconXform.GridUid)
            return false;

        return (_transform.GetWorldPosition(userXform) - _transform.GetWorldPosition(beaconXform)).Length() <= AreaRadius;
    }

    /// <summary>pinpoint_area() / get_pinpoint_text().</summary>
    private void PinpointArea(EntityUid user, ImperialGrandRitualMindComponent ritual)
    {
        string text;
        if (ritual.TargetBeacon is not { } beacon)
        {
            text = Loc.GetString("imperial-grand-ritual-pinpoint-unknown");
        }
        else
        {
            var target = _transform.GetMapCoordinates(beacon);
            var mine = _transform.GetMapCoordinates(user);
            if (target.MapId != mine.MapId)
            {
                text = Loc.GetString(_station.GetOwningStation(user) == null
                    ? "imperial-grand-ritual-pinpoint-station"
                    : "imperial-grand-ritual-pinpoint-elsewhere");
            }
            else
            {
                var delta = target.Position - mine.Position;
                var dist = delta.Length();
                var dir = Loc.GetString($"imperial-grand-ritual-dir-{delta.ToWorldAngle().GetDir().ToString().ToLowerInvariant()}");
                text = dist switch
                {
                    <= 15 => Loc.GetString("imperial-grand-ritual-pinpoint-very-close", ("dir", dir)),
                    <= 31 => Loc.GetString("imperial-grand-ritual-pinpoint-close", ("dir", dir)),
                    <= 127 => Loc.GetString("imperial-grand-ritual-pinpoint-far", ("dir", dir)),
                    _ => Loc.GetString("imperial-grand-ritual-pinpoint-very-far"),
                };
            }
        }

        _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-pinpoint", ("area", ritual.TargetName), ("text", text)), user, user);
    }

    #endregion

    #region Рисование руны

    /// <summary>start_drawing_rune().</summary>
    private void StartDrawingRune(EntityUid user, EntityUid mindId, ImperialGrandRitualMindComponent ritual)
    {
        if (ritual.Rune is { } existing && !TerminatingOrDeleted(existing))
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-rune-exists"), user, user);
            return;
        }

        var coords = Transform(user).Coordinates;
        if (!FloorAround(coords))
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-bad-floor"), user, user);
            return;
        }

        foreach (var near in _lookup.GetEntitiesInRange<ImperialGrandRuneComponent>(coords, 3f))
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-too-close"), user, user);
            return;
        }

        if (ritual.Drawing)
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-already-drawing"), user, user);
            return;
        }

        ritual.Drawing = true;
        var cheesy = NextRuneProto(ritual) == CheesyRuneProto;
        var effect = Spawn(cheesy ? "ImperialGrandRuneDrawingCheese" : "ImperialGrandRuneDrawing", _transform.GetMapCoordinates(user));
        _popup.PopupCoordinates(Loc.GetString("imperial-grand-ritual-drawing"), coords, user);

        var ev = new ImperialGrandRuneDrawDoAfterEvent();
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, DrawTime, ev, user, used: effect)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            Broadcast = true,
        });

        if (!started)
        {
            ritual.Drawing = false;
            QueueDel(effect);
        }
    }

    /// <summary>blacklisted_rune_turfs: космос и бездны вокруг нельзя.</summary>
    private bool FloorAround(EntityCoordinates coords)
    {
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var center = _map.TileIndicesFor(gridUid, grid, coords);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var tile = _map.GetTileRef(gridUid, grid, center + new Vector2i(x, y));
                if (tile.Tile.IsEmpty || _turf.IsSpace(tile))
                    return false;
            }
        }

        return true;
    }

    private EntProtoId NextRuneProto(ImperialGrandRitualMindComponent ritual)
    {
        // get_appropriate_rune_typepath()
        if (ritual.TimesCompleted < FinaleCount - 1 || ritual.DrewFinale)
            return RuneProto;

        return ritual.CheeseSacrificed >= CheeseGoal ? CheesyRuneProto : FinaleRuneProto;
    }

    /// <summary>draw_rune(): испаряет препятствия и создаёт руну.</summary>
    private void OnDrawDoAfter(ImperialGrandRuneDrawDoAfterEvent args)
    {
        var user = args.User;
        if (!_mind.TryGetMind(user, out var mindId, out _) || !TryComp<ImperialGrandRitualMindComponent>(mindId, out var ritual))
            return;

        ritual.Drawing = false;
        var cheesy = NextRuneProto(ritual) == CheesyRuneProto;
        if (args.Used is { } drawing)
            QueueDel(drawing);

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-interrupted"), user, user);
            Spawn(cheesy ? "ImperialGrandRuneFailCheese" : "ImperialGrandRuneFail", _transform.GetMapCoordinates(user));
            return;
        }

        args.Handled = true;
        var coords = Transform(user).Coordinates;
        var evaporated = false;
        foreach (var obstacle in _lookup.GetEntitiesInRange(coords, 1.4f, LookupFlags.Static | LookupFlags.Dynamic))
        {
            if (obstacle == user || HasComp<Content.Shared.Mobs.Components.MobStateComponent>(obstacle)
                || !TryComp<PhysicsComponent>(obstacle, out var physics) || !physics.CanCollide || !physics.Hard
                || (physics.CollisionLayer & (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable)) == 0)
                continue;

            evaporated = true;
            Spawn("EffectEmpPulse", _transform.GetMapCoordinates(obstacle));
            QueueDel(obstacle);
        }

        if (evaporated)
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/blind.ogg"), user, AudioParams.Default.WithVolume(4));

        _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-rune-created"), user, user);
        var proto = NextRuneProto(ritual);
        var rune = Spawn(proto, _transform.GetMapCoordinates(user));
        SetupRune(rune, mindId, ritual.TimesCompleted);
        if (proto != RuneProto)
            ritual.DrewFinale = true;

        ritual.Rune = rune;
        if (ritual.Action is { } action)
            _actions.SetCooldown(action, RitualCooldown);
    }

    /// <summary>Initialize(potency): слова, время произнесения, объявление.</summary>
    private void SetupRune(EntityUid uid, EntityUid mindId, int potency)
    {
        var rune = EnsureComp<ImperialGrandRuneComponent>(uid);
        rune.Potency = potency;
        rune.Ritual = mindId;
        rune.MagicWords = rune.Cheesy ? CheesyWords : _random.Pick(PossibleMagicWords);
        if (rune.Cheesy)
        {
            rune.FinaleEffect = "Cheese";
            rune.ChosenEffect = true;
        }

        rune.InvokeTime = GetInvokeTime(rune);
        AnnounceRune(uid, potency);
    }

    /// <summary>announce_rune(): после третьего ритуала экипаж узнаёт, где вы.</summary>
    private void AnnounceRune(EntityUid rune, int potency)
    {
        var area = AreaName(rune);
        if (potency >= ImminentPotency)
            Announce(Loc.GetString("imperial-grand-ritual-announce-major", ("area", area)));
        else if (potency >= WarningPotency)
            Announce(Loc.GetString("imperial-grand-ritual-announce-minor", ("area", area)));
    }

    public void Announce(string message, string? sender = null, SoundSpecifier? sound = null, Color? color = null)
    {
        _chat.DispatchGlobalAnnouncement(message, sender ?? Loc.GetString("imperial-grand-ritual-announce-sender"), true, sound, color ?? Color.FromHex("#a03bff"));
    }

    /// <summary>Название ближайшего к точке маяка — аналог имени зоны.</summary>
    public string AreaName(EntityUid uid)
    {
        var pos = _transform.GetMapCoordinates(uid);
        var best = Loc.GetString("imperial-grand-ritual-unknown-area");
        var bestDist = float.MaxValue;
        foreach (var (name, beacon) in _teleport.GetAreas())
        {
            var other = _transform.GetMapCoordinates(beacon);
            if (other.MapId != pos.MapId)
                continue;

            var dist = (other.Position - pos.Position).LengthSquared();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = name;
            }
        }

        return best;
    }

    #endregion

    #region Произнесение

    private bool IsWizard(EntityUid user)
    {
        return _mind.TryGetMind(user, out var mindId, out _)
               && HasComp<ImperialWizardMindComponent>(mindId) && !_wizard.IsApprentice(mindId)
               && HasComp<ImperialGrandRitualMindComponent>(mindId);
    }

    private void OnRuneExamined(Entity<ImperialGrandRuneComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.TimesInvoked >= InvokesToComplete)
        {
            args.PushMarkup(Loc.GetString("imperial-grand-ritual-examine-spent"));
            return;
        }

        if (IsWizard(args.Examiner))
            args.PushMarkup(Loc.GetString("imperial-grand-ritual-examine-left", ("count", InvokesToComplete - ent.Comp.TimesInvoked)));
    }

    /// <summary>can_interact() + interact().</summary>
    private void OnRuneInteract(Entity<ImperialGrandRuneComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !IsWizard(args.User) || ent.Comp.InUse || ent.Comp.TimesInvoked >= InvokesToComplete)
            return;

        args.Handled = true;
        if (ent.Comp.Finale && !FinaleInteract(ent, args.User))
            return;

        InvokeRune(ent, args.User);
    }

    /// <summary>invoke_rune(): маг парит над руной и произносит слова.</summary>
    private void InvokeRune(Entity<ImperialGrandRuneComponent> ent, EntityUid user)
    {
        if (ent.Comp.Finale)
            FinaleDireWarning(ent);

        ent.Comp.InUse = true;
        _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-invoking"), user, user);
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.InvokeTime, new ImperialGrandRuneInvokeDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        });

        if (!started)
            ent.Comp.InUse = false;
    }

    private void OnInvokeDoAfter(Entity<ImperialGrandRuneComponent> ent, ref ImperialGrandRuneInvokeDoAfterEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (args.Cancelled)
        {
            ent.Comp.InUse = false;
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-interrupted"), user, user);
            return;
        }

        args.Handled = true;
        ent.Comp.TimesInvoked++;
        SacrificeCheese(ent);

        var words = ent.Comp.MagicWords;
        if (words.Length > 0)
            _chat.TrySendInGameICMessage(user, words[Math.Min(ent.Comp.TimesInvoked, words.Length) - 1], InGameICChatType.Speak, false, ignoreActionBlocker: true);

        // light.flicker()
        foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(Transform(ent).Coordinates, 4f))
        {
            RaiseLocalEvent(light, new GhostBooEvent());
        }

        if (ent.Comp.TimesInvoked >= InvokesToComplete)
        {
            OnInvocationComplete(ent, user);
            return;
        }

        Spawn(ent.Comp.StatePrefix == "cheese" ? "ImperialGrandRuneFlashCheese" : "ImperialGrandRuneFlash", _transform.GetMapCoordinates(ent));
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/staff_animation.ogg"), ent);
        InvokeRune(ent, user);
    }

    /// <summary>Сыр на руне постепенно уходит в иной мир.</summary>
    private void SacrificeCheese(Entity<ImperialGrandRuneComponent> ent)
    {
        var cheese = new List<EntityUid>();
        foreach (var near in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 1.5f))
        {
            if (MetaData(near).EntityPrototype?.ID == CheeseWheel.Id && !HasComp<TimedDespawnComponent>(near))
                cheese.Add(near);
        }

        if (cheese.Count == 0)
            return;

        _random.Shuffle(cheese);
        var take = Math.Min(cheese.Count, Math.Max((int) Math.Round(cheese.Count * ent.Comp.TimesInvoked * 0.4), ent.Comp.TimesInvoked >= InvokesToComplete ? cheese.Count : 1));
        foreach (var wheel in cheese.Take(take))
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-cheese-haunted", ("cheese", wheel)), wheel, PopupType.Medium);
            EnsureComp<TimedDespawnComponent>(wheel).Lifetime = 10f;
            ent.Comp.CheeseSacrificed++;
        }
    }

    private TimeSpan GetInvokeTime(ImperialGrandRuneComponent rune)
    {
        if (rune.Finale && rune.FinaleEffect != null)
            return FinaleInvokeTime(rune.FinaleEffect);

        return BaseInvokeTime + AddInvokeTime * rune.Potency;
    }

    /// <summary>on_invocation_complete().</summary>
    private void OnInvocationComplete(Entity<ImperialGrandRuneComponent> ent, EntityUid user)
    {
        ent.Comp.InUse = false;
        var coords = _transform.GetMapCoordinates(ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/staff_change.ogg"), ent);

        if (ent.Comp.Finale)
            FinaleSummon(ent, user);
        else
            SummonRoundEvent(user);

        TriggerSideEffects(ent.Comp.Potency, Transform(ent).Coordinates, user);
        TearReality(ent.Comp.Potency);
        OnRuneComplete(ent.Comp.Ritual, ent.Comp.CheeseSacrificed);

        var cheese = ent.Comp.StatePrefix == "cheese";
        Spawn(cheese ? "ImperialGrandRuneActivateCheese" : "ImperialGrandRuneActivate", coords);
        var remains = cheese ? "ImperialGrandRuneRemainsCheese" : "ImperialGrandRuneRemains";
        var rune = ent.Owner;
        Schedule(TimeSpan.FromSeconds(0.6), () =>
        {
            if (TerminatingOrDeleted(rune))
                return;

            Spawn(remains, _transform.GetMapCoordinates(rune));
            QueueDel(rune);
        });
    }

    /// <summary>summon_round_event(): случайное событие станции.</summary>
    private void SummonRoundEvent(EntityUid user)
    {
        if (_events.PickRandomEvent() is not { } picked)
        {
            _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-no-event"), user, user);
            return;
        }

        _ticker.StartGameRule(picked);
        var name = _proto.TryIndex(picked, out var proto) ? proto.Name : picked;
        _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-event", ("event", name)), user, user, PopupType.Medium);
    }

    /// <summary>tear_reality(): до трёх разрывов реальности (половина — уже иссушены).</summary>
    private void TearReality(int potency)
    {
        var max = potency switch
        {
            <= 2 => 1,
            <= 5 => 2,
            _ => 3,
        };

        var toCreate = _random.Next(0, max + 1);
        var areas = _teleport.GetAreas().Values.ToList();
        for (var i = 0; i < toCreate && areas.Count > 0; i++)
        {
            if (!_teleport.TryGetDestination(_random.Pick(areas), out var dest))
                continue;

            Spawn(_random.Prob(0.5f) ? "HereticRealityBreach" : "HereticRealityRift", dest);
        }
    }

    /// <summary>on_rune_complete(): сыр, счётчик, новый узел, предупреждения.</summary>
    private void OnRuneComplete(EntityUid mindId, int cheese)
    {
        if (!TryComp<ImperialGrandRitualMindComponent>(mindId, out var ritual))
            return;

        ritual.CheeseSacrificed += cheese;
        if (ritual.CheeseSacrificed >= CheeseGoal)
        {
            Notify(mindId, Loc.GetString(ritual.CheeseGoalMet ? "imperial-grand-ritual-cheese-more" : "imperial-grand-ritual-cheese-goal"));
            ritual.CheeseGoalMet = true;
        }
        else if (ritual.CheeseSacrificed > 0)
        {
            Notify(mindId, Loc.GetString("imperial-grand-ritual-cheese-progress", ("left", CheeseGoal - ritual.CheeseSacrificed)));
        }

        ritual.Rune = null;
        ritual.TimesCompleted++;
        SetNewArea(mindId, ritual);
        switch (ritual.TimesCompleted)
        {
            case WarningPotency:
                Notify(mindId, Loc.GetString("imperial-grand-ritual-warning"));
                break;
            case ImminentPotency:
                Notify(mindId, Loc.GetString(ritual.CheeseSacrificed >= CheeseGoal
                    ? "imperial-grand-ritual-imminent-cheese"
                    : "imperial-grand-ritual-imminent"));
                break;
            case FinaleCount:
                OnRitualFinalComplete(mindId);
                break;
        }
    }

    /// <summary>wizard/on_ritual_complete(): цели заменяются выполненным ритуалом.</summary>
    private void OnRitualFinalComplete(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return;

        for (var i = mind.Objectives.Count - 1; i >= 0; i--)
        {
            _mind.TryRemoveObjective(mindId, mind, i);
        }

        if (_objectives.TryCreateObjective(mindId, mind, RitualObjective) is { } objective)
            _mind.AddObjective(mindId, mind, objective);
    }

    public void Notify(EntityUid mindId, string message)
    {
        if (TryComp<MindComponent>(mindId, out var mind) && mind.UserId is { } userId && _players.TryGetSessionById(userId, out var session))
            _chatManager.DispatchServerMessage(session, message, true);

        if (TryComp<MindComponent>(mindId, out var owned) && owned.OwnedEntity is { } body)
            _popup.PopupEntity(message, body, body, PopupType.Medium);
    }

    #endregion
}
