using Content.Server.Atmos.EntitySystems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Xenobiology.Systems;

/// <summary>
/// Система заряженного ядра ксено-слайма (XenoChargedSlimeCore).
///
/// Жизненный цикл:
///   1. Ядро создаётся XenoSlimeCrossbreedSystem с двумя цветами (SlimeColor + ExtractColor).
///   2. Игрок вкалывает PlasmaRequired единиц Plasma через шприц/гипоспрей.
///   3. Система обнаруживает плазму через SolutionContainerChangedEvent → PlasmaCharged = true.
///   4. Игрок активирует ядро в руке (UseInHandEvent) → применяется эффект.
///
/// Матрица эффектов: SlimeColor (тип) × ExtractColor (материал).
/// </summary>
public sealed class XenoChargedSlimeCoreSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem             _popup     = default!;
    [Dependency] private readonly FirestarterSystem             _fire      = default!;
    [Dependency] private readonly EmpSystem                     _emp       = default!;
    [Dependency] private readonly ExplosionSystem               _explosion = default!;
    [Dependency] private readonly EntityLookupSystem            _lookup    = default!;
    [Dependency] private readonly TransformSystem               _xform     = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoChargedSlimeCoreComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<XenoChargedSlimeCoreComponent, UseInHandEvent>(OnUseInHand);
    }

    // ── Обнаружение плазмы ───────────────────────────────────────────────────

    private void OnSolutionChanged(EntityUid uid, XenoChargedSlimeCoreComponent comp,
        ref SolutionContainerChangedEvent args)
    {
        if (comp.PlasmaCharged)
            return;
        if (args.SolutionId != comp.SolutionName)
            return;

        var plasmaQty = args.Solution.GetTotalPrototypeQuantity("Plasma");
        if (plasmaQty < FixedPoint2.New(comp.PlasmaRequired))
            return;

        // Достаточно плазмы — заряжаем ядро и потребляем реагент
        comp.PlasmaCharged = true;

        if (_solutions.TryGetSolution(uid, comp.SolutionName, out var solEnt, out _))
        {
            _solutions.RemoveReagent(solEnt.Value, "Plasma", FixedPoint2.New(comp.PlasmaRequired));
            _solutions.UpdateChemicals(solEnt.Value);
        }

        _popup.PopupCoordinates(
            "Ядро заряжено! Активируйте его в руке.",
            _xform.GetMoverCoordinates(uid),
            PopupType.Medium);
    }

    // ── Активация в руке ─────────────────────────────────────────────────────

    private void OnUseInHand(EntityUid uid, XenoChargedSlimeCoreComponent comp, UseInHandEvent args)
    {
        if (!comp.PlasmaCharged)
        {
            _popup.PopupEntity(
                $"Ядро не заряжено. Вколите {comp.PlasmaRequired}u плазмы через шприц.",
                uid, args.User, PopupType.Small);
            return;
        }

        args.Handled = true;
        ActivateCore(uid, comp, args.User);
    }

    // ── Активация и применение эффекта ───────────────────────────────────────

    private void ActivateCore(EntityUid uid, XenoChargedSlimeCoreComponent comp, EntityUid user)
    {
        var coords    = _xform.GetMapCoordinates(uid);
        var tileCoords = _xform.GetMoverCoordinates(uid);

        ApplyCombinationEffect(uid, comp.SlimeColor, comp.ExtractColor, coords, tileCoords, user);
        QueueDel(uid);
    }

    /// <summary>
    /// Матрица эффектов кроссбридинга.
    /// SlimeColor = тип эффекта (архетип).
    /// ExtractColor = конкретное применение.
    /// </summary>
    private void ApplyCombinationEffect(
        EntityUid uid,
        XenoSlimeColor slimeColor,
        XenoSlimeColor extractColor,
        MapCoordinates coords,
        EntityCoordinates tileCoords,
        EntityUid user)
    {
        switch (slimeColor)
        {
            // ── Тип «Промышленный» (Grey) ────────────────────────────────────
            // Позволяет массово штамповать ресурсы
            case XenoSlimeColor.Grey:
                ApplyIndustrialEffect(uid, extractColor, coords, tileCoords, user);
                break;

            // ── Тип «Жгучий» (Orange) ────────────────────────────────────────
            // Мощный огонь/взрыв
            case XenoSlimeColor.Orange:
                ApplyBurningEffect(uid, extractColor, coords, tileCoords, user);
                break;

            // ── Тип «Регенеративный» (Blue) ──────────────────────────────────
            // Сильное исцеление
            case XenoSlimeColor.Blue:
                ApplyRegenEffect(uid, extractColor, coords, tileCoords, user);
                break;

            // ── Тип «Стабильный» (Pink) ──────────────────────────────────────
            // Создание постоянных предметов
            case XenoSlimeColor.Pink:
                ApplyStabilizedEffect(uid, extractColor, coords, tileCoords, user);
                break;

            // ── Тип «Разумный» (Gold) ────────────────────────────────────────
            // Контроль над существами
            case XenoSlimeColor.Gold:
                ApplyMindEffect(uid, extractColor, coords, tileCoords, user);
                break;

            // ── Тип «Самопревращение» (Green) ────────────────────────────────
            // Трансформация тела
            case XenoSlimeColor.Green:
                ApplyMorphEffect(uid, extractColor, coords, tileCoords, user);
                break;

            // ── Тип «Электрический» (Yellow) ─────────────────────────────────
            // Электрические / ЭМИ эффекты
            case XenoSlimeColor.Yellow:
                ApplyElectricEffect(uid, extractColor, coords, tileCoords, user);
                break;

            // ── Прочие цвета — универсальный ЭМИ ─────────────────────────────
            default:
                _emp.EmpPulse(coords, 4f, 200f, TimeSpan.FromSeconds(20));
                _popup.PopupCoordinates("Ядро испускает мощный импульс!", tileCoords, PopupType.LargeCaution);
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Тип Grey: Промышленный — спавн ресурсов
    // ══════════════════════════════════════════════════════════════════════════
    private void ApplyIndustrialEffect(EntityUid uid, XenoSlimeColor fill,
        MapCoordinates coords, EntityCoordinates tileCoords, EntityUid user)
    {
        // Спавним ресурсы соответствующего материала
        var proto = fill switch
        {
            XenoSlimeColor.Orange    => "SheetSteel",
            XenoSlimeColor.Purple    => "SheetPlastic",
            XenoSlimeColor.Blue      => "SheetGlass",
            XenoSlimeColor.Metal     => "IngotIron",
            XenoSlimeColor.Yellow    => "IngotGold",
            XenoSlimeColor.DarkBlue  => "SheetRGlass",
            XenoSlimeColor.Silver    => "IngotSilver",
            XenoSlimeColor.Bluespace => "SheetBluespace",
            XenoSlimeColor.Adamantine=> "XenoAdamantineBar",
            _                        => "SheetSteel",
        };

        for (var i = 0; i < 10; i++)
            Spawn(proto, coords);

        _popup.PopupCoordinates($"Ядро штампует ресурсы!", tileCoords, PopupType.Medium);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Тип Orange: Жгучий — огонь / взрыв
    // ══════════════════════════════════════════════════════════════════════════
    private void ApplyBurningEffect(EntityUid uid, XenoSlimeColor fill,
        MapCoordinates coords, EntityCoordinates tileCoords, EntityUid user)
    {
        switch (fill)
        {
            case XenoSlimeColor.Metal:
            case XenoSlimeColor.Adamantine:
                // Мощный взрыв
                _explosion.QueueExplosion(coords, "Default", 200f, 6f, 20f, null);
                _popup.PopupCoordinates("ВЗРЫВ!", tileCoords, PopupType.LargeCaution);
                break;

            case XenoSlimeColor.Bluespace:
                // Телепортирующий взрыв (обычный взрыв + случайная телепортация рядом)
                _explosion.QueueExplosion(coords, "Default", 120f, 5f, 15f, null);
                _popup.PopupCoordinates("Ядро взрывается синеполым огнём!", tileCoords, PopupType.LargeCaution);
                break;

            default:
                // Обычный поджог
                _fire.IgniteNearby(uid, tileCoords, 0.9f, 5f);
                _popup.PopupCoordinates("Ядро взрывается огнём!", tileCoords, PopupType.LargeCaution);
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Тип Blue: Регенеративный — спавн лечебных реагентов
    // ══════════════════════════════════════════════════════════════════════════
    private void ApplyRegenEffect(EntityUid uid, XenoSlimeColor fill,
        MapCoordinates coords, EntityCoordinates tileCoords, EntityUid user)
    {
        // Спавним реагенты в виде капель/флаконов (упрощённо: спавн сущностей)
        var (reagentProto, msg) = fill switch
        {
            XenoSlimeColor.Purple    => ("HealingReagentBottle", "Универсальное целебное зелье!"),
            XenoSlimeColor.Green     => ("BicaridineBottle",      "Флакон бикаридина!"),
            XenoSlimeColor.Pink      => ("DermalineBottle",       "Флакон дермалина!"),
            XenoSlimeColor.Silver    => ("SalineBottle",          "Флакон физраствора!"),
            XenoSlimeColor.Gold      => ("IcarusBottle",           "Флакон икаруса!"),
            XenoSlimeColor.Bluespace => ("CryoxadoneBottle",      "Флакон криоксадона!"),
            _                        => ("HealingReagentBottle",  "Целебное зелье!"),
        };

        // Спавним 3 флакона
        for (var i = 0; i < 3; i++)
            Spawn(reagentProto, coords);

        _popup.PopupCoordinates($"Ядро выделяет: {msg}", tileCoords, PopupType.Medium);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Тип Pink: Стабильный — создание постоянных предметов
    // ══════════════════════════════════════════════════════════════════════════
    private void ApplyStabilizedEffect(EntityUid uid, XenoSlimeColor fill,
        MapCoordinates coords, EntityCoordinates tileCoords, EntityUid user)
    {
        var (proto, msg) = fill switch
        {
            XenoSlimeColor.Grey      => ("ClothingBackpackInfinite",  "Бесконечный рюкзак!"),
            XenoSlimeColor.Silver    => ("ClothingOuterHardsuitVoid", "Энергетический щит-скафандр!"),
            XenoSlimeColor.Metal     => ("WeaponSwordCultFinal",      "Клинок из экстракт-металла!"),
            XenoSlimeColor.Blue      => ("SlimeDrinkGlass",           "Бесконечный напиток слайма!"),
            XenoSlimeColor.Rainbow   => ("XenoSlimeBlueprint",        "Схема создания слаймов!"),
            XenoSlimeColor.Bluespace => ("XenoBluespaceFloorTile",    "Плитка блюспейса!"),
            _                        => ("ClothingBackpackInfinite",  "Артефакт!"),
        };

        Spawn(proto, coords);
        _popup.PopupCoordinates($"Ядро создаёт: {msg}", tileCoords, PopupType.Medium);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Тип Gold: Разумный — доминирование над существами
    // ══════════════════════════════════════════════════════════════════════════
    private void ApplyMindEffect(EntityUid uid, XenoSlimeColor fill,
        MapCoordinates coords, EntityCoordinates tileCoords, EntityUid user)
    {
        // Делаем ближайших слаймов дружелюбными к игроку
        var slimes = new HashSet<Entity<XenoSlimeComponent>>();
        _lookup.GetEntitiesInRange<XenoSlimeComponent>(coords, 8f, slimes);

        foreach (var (slimeUid, slimeComp) in slimes)
            slimeComp.Friends.Add(user);

        var count = slimes.Count;
        _popup.PopupCoordinates(
            $"Ядро подчиняет {count} слаймов вашей воле!",
            tileCoords, PopupType.LargeCaution);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Тип Green: Самопревращение — трансформация
    // ══════════════════════════════════════════════════════════════════════════
    private void ApplyMorphEffect(EntityUid uid, XenoSlimeColor fill,
        MapCoordinates coords, EntityCoordinates tileCoords, EntityUid user)
    {
        // Пока: спавним специфические объекты-трансформаторы
        var (proto, msg) = fill switch
        {
            XenoSlimeColor.DarkBlue  => ("SlimeMorphGel", "Гель трансформации слаймолюда!"),
            XenoSlimeColor.Bluespace => ("SlimeMorphGel",  "Гель телепортации!"),
            XenoSlimeColor.Silver    => ("SlimeMorphGel",  "Гель серебряной формы!"),
            _                        => ("SlimeMorphGel",  "Гель превращения!"),
        };

        Spawn(proto, coords);
        _popup.PopupCoordinates($"Ядро выделяет: {msg}", tileCoords, PopupType.Medium);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Тип Yellow: Электрический — ЭМИ и электро-эффекты
    // ══════════════════════════════════════════════════════════════════════════
    private void ApplyElectricEffect(EntityUid uid, XenoSlimeColor fill,
        MapCoordinates coords, EntityCoordinates tileCoords, EntityUid user)
    {
        switch (fill)
        {
            case XenoSlimeColor.Metal:
            case XenoSlimeColor.DarkBlue:
                // Сильный ЭМИ
                _emp.EmpPulse(coords, 8f, 500f, TimeSpan.FromSeconds(30));
                _popup.PopupCoordinates("Мощный ЭМИ-импульс!", tileCoords, PopupType.LargeCaution);
                break;

            case XenoSlimeColor.Bluespace:
                // ЭМИ + взрыв
                _emp.EmpPulse(coords, 6f, 300f, TimeSpan.FromSeconds(20));
                _explosion.QueueExplosion(coords, "Electric", 80f, 4f, 10f, null);
                _popup.PopupCoordinates("Электрический взрыв!", tileCoords, PopupType.LargeCaution);
                break;

            case XenoSlimeColor.Orange:
                // Молния — ЭМИ + поджог
                _emp.EmpPulse(coords, 4f, 200f, TimeSpan.FromSeconds(15));
                _fire.IgniteNearby(uid, tileCoords, 0.7f, 3f);
                _popup.PopupCoordinates("Электрический разряд поджигает всё!", tileCoords, PopupType.LargeCaution);
                break;

            default:
                // Обычный ЭМИ
                _emp.EmpPulse(coords, 4f, 200f, TimeSpan.FromSeconds(20));
                _popup.PopupCoordinates("ЭМИ-импульс!", tileCoords, PopupType.Medium);
                break;
        }
    }
}
