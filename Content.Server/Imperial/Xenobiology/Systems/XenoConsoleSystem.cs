using System.Linq;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Xenobiology.Systems;

/// <summary>
/// Система ксенобиологической консоли.
///
/// Игрок нажимает E на консоли → входит в режим камеры (призрак с видом ИИ).
/// В режиме камеры доступны 7 действий:
///   camera_off   — выход, возврат в тело
///   monkey_up    — взять XenoMonkey из-под камеры в хранилище
///   monkey_down  — выложить XenoMonkey из хранилища под камеру
///   slime_up     — взять XenoSlime из-под камеры в хранилище
///   slime_down   — выложить XenoSlime из хранилища под камеру
///   slime_potion — ввести реагент из консоли слайму под камерой
///   slime_scan   — показать информацию о слайме под камерой
///
/// MonkeyCube (id: MonkeyCube) можно вставить в консоль E+предмет:
///   куб удаляется, в хранилище появляется XenoMonkey.
/// </summary>
public sealed class XenoConsoleSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem               _transform  = default!;
    [Dependency] private readonly EntityLookupSystem            _lookup     = default!;
    [Dependency] private readonly SolutionContainerSystem       _solutions  = default!;
    [Dependency] private readonly MindSystem                    _mind       = default!;
    [Dependency] private readonly SharedPopupSystem             _popup      = default!;
    [Dependency] private readonly MetaDataSystem                _metaData   = default!;

    private const float PickupRange = 1.5f;

    private static readonly float[] MutationChanceTable =
        { 0.90f, 0.80f, 0.65f, 0.50f, 0.35f, 0.20f, 0f };

    public override void Initialize()
    {
        base.Initialize();

        // Консоль
        SubscribeLocalEvent<XenoConsoleComponent, InteractHandEvent>(OnConsoleActivate);
        SubscribeLocalEvent<XenoConsoleComponent, InteractUsingEvent>(OnConsoleInsertItem);
        SubscribeLocalEvent<XenoConsoleComponent, ExaminedEvent>(OnConsoleExamined);

        // Действия камеры
        SubscribeLocalEvent<XenoConsoleGhostComponent, XenoGhostCameraOffEvent>  (OnCameraOff);
        SubscribeLocalEvent<XenoConsoleGhostComponent, XenoGhostMonkeyUpEvent>   (OnMonkeyUp);
        SubscribeLocalEvent<XenoConsoleGhostComponent, XenoGhostMonkeyDownEvent> (OnMonkeyDown);
        SubscribeLocalEvent<XenoConsoleGhostComponent, XenoGhostSlimeUpEvent>    (OnSlimeUp);
        SubscribeLocalEvent<XenoConsoleGhostComponent, XenoGhostSlimeDownEvent>  (OnSlimeDown);
        SubscribeLocalEvent<XenoConsoleGhostComponent, XenoGhostSlimePotionEvent>(OnSlimePotion);
        SubscribeLocalEvent<XenoConsoleGhostComponent, XenoGhostSlimeScanEvent>  (OnSlimeScan);

        // Отсоединение / уничтожение
        SubscribeLocalEvent<XenoConsoleGhostComponent, PlayerDetachedEvent>      (OnGhostDetached);
        SubscribeLocalEvent<XenoConsoleGhostComponent, EntityTerminatingEvent>   (OnGhostTerminating);
    }

    // =========================================================================
    // Интерактивность консоли
    // =========================================================================

    private void OnConsoleActivate(EntityUid uid, XenoConsoleComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (comp.GhostEntity != null && !Deleted(comp.GhostEntity.Value))
        {
            _popup.PopupEntity("Консоль уже используется.", uid, args.User);
            args.Handled = true;
            return;
        }

        var mindId = _mind.GetMind(args.User);
        if (mindId == null)
            return;

        if (!TryComp<MindComponent>(mindId.Value, out var mindComp) || mindComp.OwnedEntity == null)
            return;

        args.Handled = true;
        var originalBody = mindComp.OwnedEntity.Value;

        // Спавним призрака и настраиваем его
        var ghostUid  = Spawn("XenoConsoleGhost", Transform(uid).Coordinates);
        var ghostComp = Comp<XenoConsoleGhostComponent>(ghostUid);

        ghostComp.ConsoleUid      = uid;
        ghostComp.MindId          = mindId.Value;
        ghostComp.OriginalBodyUid = originalBody;
        ghostComp.ConsoleWorldPos = _transform.GetMapCoordinates(uid).Position;

        comp.GhostEntity = ghostUid;

        // Переносим разум — тот же механизм что у обычных призраков
        _mind.TransferTo(mindId.Value, ghostUid, ghostCheckOverride: true, createGhost: false);
    }

    private void OnConsoleInsertItem(EntityUid uid, XenoConsoleComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var meta = MetaData(args.Used);

        // MonkeyCube
        if (meta.EntityPrototype?.ID == "MonkeyCube")
        {
            args.Handled = true;
            QueueDel(args.Used);
            var monkey = Spawn(comp.MonkeyProto, Transform(uid).Coordinates);
            _transform.DetachParentToNull(monkey, Transform(monkey));
            _metaData.SetEntityPaused(monkey, true);
            comp.MonkeyStorage.Add(monkey);
            _popup.PopupEntity("\u041e\u0431\u0435\u0437\u044c\u044f\u043d\u0438\u0439 \u043a\u0443\u0431 \u043f\u0440\u0438\u043d\u044f\u0442. \u041e\u0431\u0435\u0437\u044c\u044f\u043d\u0430 \u0434\u043e\u0431\u0430\u0432\u043b\u0435\u043d\u0430 \u0432 \u0445\u0440\u0430\u043d\u0438\u043b\u0438\u0449\u0435.", uid, args.User);
            return;
        }

        // Мензурка/шприц: перелить реагенты в резервуар консоли
        if (!_solutions.TryGetDrainableSolution(args.Used, out var drainable, out var drainSoln))
            return;

        args.Handled = true;

        if (drainSoln!.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Мензурка пуста.", uid, args.User);
            return;
        }

        if (!_solutions.TryGetSolution(uid, comp.PotionSolutionName, out var consoleSolnEnt, out var consoleSoln))
            return;

        if (consoleSoln!.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("Резервуар реагентов консоли заполнен.", uid, args.User);
            return;
        }

        var toTransfer  = FixedPoint2.Min(drainSoln!.Volume, consoleSoln!.AvailableVolume);
        var transferred = _solutions.SplitSolution(drainable!.Value, toTransfer);
        _solutions.TryAddSolution(consoleSolnEnt!.Value, transferred);
        _popup.PopupEntity($"Перелито {toTransfer} ед. реагента в консоль.", uid, args.User);
    }

    // =========================================================================
    // Действия камеры-призрака
    // =========================================================================

    private void OnCameraOff(EntityUid uid, XenoConsoleGhostComponent ghostComp, XenoGhostCameraOffEvent args)
    {
        ReturnAndCleanup(uid, ghostComp);
    }

    private void OnMonkeyUp(EntityUid uid, XenoConsoleGhostComponent ghostComp, XenoGhostMonkeyUpEvent args)
    {
        if (!TryComp<XenoConsoleComponent>(ghostComp.ConsoleUid, out var comp))
            return;

        var pos     = _transform.GetMapCoordinates(uid);
        var monkeys = _lookup.GetEntitiesInRange<XenoSlimeFoodComponent>(pos, PickupRange);

        if (monkeys.Count == 0)
        {
            _popup.PopupEntity("Нет обезьяны под камерой.", uid, args.Performer);
            return;
        }

        var monkey = monkeys.First().Owner;
        _transform.DetachParentToNull(monkey, Transform(monkey));
        _metaData.SetEntityPaused(monkey, true);
        comp.MonkeyStorage.Add(monkey);
        _popup.PopupEntity("Обезьяна помещена в хранилище.", uid, args.Performer);
    }

    private void OnMonkeyDown(EntityUid uid, XenoConsoleGhostComponent ghostComp, XenoGhostMonkeyDownEvent args)
    {
        if (!TryComp<XenoConsoleComponent>(ghostComp.ConsoleUid, out var comp))
            return;

        comp.MonkeyStorage.RemoveAll(e => Deleted(e));

        if (comp.MonkeyStorage.Count == 0)
        {
            _popup.PopupEntity("Хранилище обезьян пусто.", uid, args.Performer);
            return;
        }

        var monkey = comp.MonkeyStorage[0];
        comp.MonkeyStorage.RemoveAt(0);
        _metaData.SetEntityPaused(monkey, false);
        _transform.SetCoordinates(monkey, Transform(uid).Coordinates);
        _popup.PopupEntity("Обезьяна выложена под камерой.", uid, args.Performer);
    }

    private void OnSlimeUp(EntityUid uid, XenoConsoleGhostComponent ghostComp, XenoGhostSlimeUpEvent args)
    {
        if (!TryComp<XenoConsoleComponent>(ghostComp.ConsoleUid, out var comp))
            return;

        var pos    = _transform.GetMapCoordinates(uid);
        var slimes = _lookup.GetEntitiesInRange<XenoSlimeComponent>(pos, PickupRange);

        if (slimes.Count == 0)
        {
            _popup.PopupEntity("Нет слайма под камерой.", uid, args.Performer);
            return;
        }

        var slime = slimes.First().Owner;
        _transform.DetachParentToNull(slime, Transform(slime));
        _metaData.SetEntityPaused(slime, true);
        comp.SlimeStorage.Add(slime);
        _popup.PopupEntity("Слайм помещён в хранилище.", uid, args.Performer);
    }

    private void OnSlimeDown(EntityUid uid, XenoConsoleGhostComponent ghostComp, XenoGhostSlimeDownEvent args)
    {
        if (!TryComp<XenoConsoleComponent>(ghostComp.ConsoleUid, out var comp))
            return;

        comp.SlimeStorage.RemoveAll(e => Deleted(e));

        if (comp.SlimeStorage.Count == 0)
        {
            _popup.PopupEntity("Хранилище слаймов пусто.", uid, args.Performer);
            return;
        }

        var slime = comp.SlimeStorage[0];
        comp.SlimeStorage.RemoveAt(0);
        _metaData.SetEntityPaused(slime, false);
        _transform.SetCoordinates(slime, Transform(uid).Coordinates);
        _popup.PopupEntity("Слайм выложен под камерой.", uid, args.Performer);
    }

    private void OnSlimePotion(EntityUid uid, XenoConsoleGhostComponent ghostComp, XenoGhostSlimePotionEvent args)
    {
        if (!TryComp<XenoConsoleComponent>(ghostComp.ConsoleUid, out var comp))
            return;

        var pos    = _transform.GetMapCoordinates(uid);
        var slimes = _lookup.GetEntitiesInRange<XenoSlimeComponent>(pos, PickupRange);

        if (slimes.Count == 0)
        {
            _popup.PopupEntity("Нет ксено-слайма под камерой.", uid, args.Performer);
            return;
        }

        if (!_solutions.TryGetSolution(ghostComp.ConsoleUid, comp.PotionSolutionName, out var consoleSolnEnt, out var consoleSoln))
            return;

        if (consoleSoln!.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity("В консоли нет реагентов.", uid, args.Performer);
            return;
        }

        var slimeUid = slimes.First().Owner;

        if (!_solutions.TryGetInjectableSolution(slimeUid, out var injectable, out _))
        {
            _popup.PopupEntity("Слайм не принимает реагенты.", uid, args.Performer);
            return;
        }

        var toInject = _solutions.SplitSolution(consoleSolnEnt!.Value, consoleSoln!.Volume);
        _solutions.TryAddSolution(injectable!.Value, toInject);

        _popup.PopupEntity("Реагент введён слайму.", uid, args.Performer);
    }

    private void OnSlimeScan(EntityUid uid, XenoConsoleGhostComponent ghostComp, XenoGhostSlimeScanEvent args)
    {
        var pos    = _transform.GetMapCoordinates(uid);
        var slimes = _lookup.GetEntitiesInRange<XenoSlimeComponent>(pos, PickupRange);

        if (slimes.Count == 0)
        {
            _popup.PopupEntity("Нет ксено-слайма под камерой.", uid, args.Performer);
            return;
        }

        var slimeEnt  = slimes.First();
        var slimeComp = slimeEnt.Comp;

        // HP
        var hpStr = "N/A";
        if (TryComp<DamageableComponent>(slimeEnt.Owner, out var damageable) &&
            TryComp<MobThresholdsComponent>(slimeEnt.Owner, out var thresholds))
        {
            var deadThreshold = FixedPoint2.Zero;
            foreach (var (dmg, state) in thresholds.Thresholds)
            {
                if (state == MobState.Dead)
                {
                    deadThreshold = dmg;
                    break;
                }
            }
            var hp = deadThreshold - damageable.TotalDamage;
            hpStr = $"{hp} / {deadThreshold}";
        }

        // Голод
        var hungerStr  = slimeComp.Mood == XenoSlimeMood.Aggressive
            ? "Голодный (агрессивный)"
            : $"Сытый ({slimeComp.HungerPercent:F0}%)";

        // Шанс мутации
        var tier          = Math.Clamp((int) slimeComp.Tier, 0, 6);
        var effectiveTier = Math.Clamp((int) Math.Max(1, tier - slimeComp.MutationBoost), 0, 6);
        var mutChance     = MutationChanceTable[effectiveTier];

        // Цвет
        var colorName = slimeComp.Color switch
        {
            XenoSlimeColor.Grey       => "Серый",
            XenoSlimeColor.Orange     => "Оранжевый",
            XenoSlimeColor.Purple     => "Фиолетовый",
            XenoSlimeColor.Blue       => "Синий",
            XenoSlimeColor.Metal      => "Металлический",
            XenoSlimeColor.Yellow     => "Жёлтый",
            XenoSlimeColor.DarkPurple => "Тёмно-фиолетовый",
            XenoSlimeColor.DarkBlue   => "Тёмно-синий",
            XenoSlimeColor.Silver     => "Серебристый",
            XenoSlimeColor.Bluespace  => "Блюспейс",
            XenoSlimeColor.Sepia      => "Сепия",
            XenoSlimeColor.Cerulean   => "Небесно-синий",
            XenoSlimeColor.Pyrite     => "Пирит",
            XenoSlimeColor.Green      => "Зелёный",
            XenoSlimeColor.Red        => "Красный",
            XenoSlimeColor.Pink       => "Розовый",
            XenoSlimeColor.Gold       => "Золотой",
            XenoSlimeColor.Oil        => "Масляный",
            XenoSlimeColor.Black      => "Чёрный",
            XenoSlimeColor.LightPink  => "Нежно-розовый",
            XenoSlimeColor.Adamantine => "Адамантиновый",
            XenoSlimeColor.Rainbow    => "Радужный",
            _                         => slimeComp.Color.ToString(),
        };

        // Возраст
        var ageStr = slimeComp.AgeStage switch
        {
            XenoSlimeAge.Young   => "Молодой",
            XenoSlimeAge.Adult   => "Взрослый",
            XenoSlimeAge.Old     => "Старый",
            XenoSlimeAge.Ancient => "Древний",
            _                    => slimeComp.AgeStage.ToString(),
        };

        var sizeStr = slimeComp.IsAdult ? "большой" : "маленький";

        var msg = $"=== СКАНИРОВАНИЕ СЛАЙМА ===" +
                  $"\nВид: {colorName} ({sizeStr})" +
                  $"\nHP: {hpStr}" +
                  $"\nГолод: {hungerStr}" +
                  $"\nВозраст: {ageStr}" +
                  $"\nШанс мутации: {mutChance * 100f:F0}%";

        _popup.PopupEntity(msg, uid, args.Performer, PopupType.Large);
    }

    // =========================================================================
    // Очистка и возврат
    // =========================================================================

    private void ReturnAndCleanup(EntityUid ghostUid, XenoConsoleGhostComponent ghostComp)
    {
        if (ghostComp.OriginalBodyUid.IsValid() && !Deleted(ghostComp.OriginalBodyUid))
            _mind.TransferTo(ghostComp.MindId, ghostComp.OriginalBodyUid, ghostCheckOverride: true, createGhost: false);
        ClearConsoleGhost(ghostComp.ConsoleUid, ghostUid);
        QueueDel(ghostUid);
    }

    private void ClearConsoleGhost(EntityUid consoleUid, EntityUid ghostUid)
    {
        if (!TryComp<XenoConsoleComponent>(consoleUid, out var comp))
            return;
        if (comp.GhostEntity == ghostUid)
            comp.GhostEntity = null;
    }

    private void OnConsoleExamined(EntityUid uid, XenoConsoleComponent comp, ExaminedEvent args)
    {
        comp.MonkeyStorage.RemoveAll(e => Deleted(e));
        comp.SlimeStorage.RemoveAll(e => Deleted(e));
        args.PushMarkup($"Обезьян в хранилище: [color=yellow]{comp.MonkeyStorage.Count}[/color]");
        args.PushMarkup($"Слаймов в хранилище: [color=cyan]{comp.SlimeStorage.Count}[/color]");
    }

    private void OnGhostDetached(EntityUid uid, XenoConsoleGhostComponent ghostComp, PlayerDetachedEvent args)
    {
        // Игрок отключился — вернуть разум в тело и удалить камеру
        ReturnAndCleanup(uid, ghostComp);
    }

    private void OnGhostTerminating(EntityUid uid, XenoConsoleGhostComponent ghostComp, ref EntityTerminatingEvent args)
    {
        ClearConsoleGhost(ghostComp.ConsoleUid, uid);
    }

    // =========================================================================
    // Update: ограничение дальности камеры
    // =========================================================================

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<XenoConsoleGhostComponent>();
        while (query.MoveNext(out var uid, out var ghostComp))
        {
            var xform    = Transform(uid);
            var worldPos = _transform.GetWorldPosition(xform);
            var dist     = (worldPos - ghostComp.ConsoleWorldPos).Length();

            if (dist > XenoConsoleGhostComponent.MaxRange)
            {
                var dir     = worldPos - ghostComp.ConsoleWorldPos;
                var clamped = ghostComp.ConsoleWorldPos + dir * (XenoConsoleGhostComponent.MaxRange / dist);
                _transform.SetWorldPosition(xform, clamped);
            }
        }
    }
}
