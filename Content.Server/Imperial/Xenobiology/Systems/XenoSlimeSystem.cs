using System;
using System.Collections.Generic;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.Xenobiology.Systems;

/// <summary>
/// Система ксенобиологии слаймов.
///
/// Логика работы:
///   1. Слайм атакует пищу (MeleeHitEvent):
///      - Защита друзей: если цель — друг, убираем из списка ударов.
///      - Если цель — пища (XenoSlimeFoodComponent) в Critical/Dead и слайм не занят — автозапуск процесса проглатывания (DoAfter).
///   2. DoAfter завершается:
///      - Отменјн: слайм отошёл, SwallowTarget сбрасывается.
///      - Успех: тело помещается в желудок (исчезает из мира) → слайм растёт/делится.
///   3. Update (тик): таймер голода не копится пока слайм занят процессом проглатывания.
///
/// Логика роста:
///   Маленький (IsAdult=false) + еда → вырастает в большого.
///   Большой  (IsAdult=true)  + еда → делится на 4 маленьких.
/// </summary>
public sealed class XenoSlimeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem    _lookup     = default!;
    [Dependency] private readonly TransformSystem       _transform  = default!;
    [Dependency] private readonly IRobustRandom         _random     = default!;
    [Dependency] private readonly HTNSystem             _htn        = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedDoAfterSystem   _doAfter    = default!;

    // ─── прототипы по цвету (индекс = (int)XenoSlimeColor) ─────────────────
    private static readonly string[] SmallProtos =
    {
        "XenoSlimeGreySmall",        //  0 Grey        тир 0
        "XenoSlimeOrangeSmall",      //  1 Orange      тир 1
        "XenoSlimePurpleSmall",      //  2 Purple      тир 1
        "XenoSlimeBlueSmall",        //  3 Blue        тир 1
        "XenoSlimeMetalSmall",       //  4 Metal       тир 1
        "XenoSlimeYellowSmall",      //  5 Yellow      тир 2
        "XenoSlimeDarkPurpleSmall",  //  6 DarkPurple  тир 2
        "XenoSlimeDarkBlueSmall",    //  7 DarkBlue    тир 2
        "XenoSlimeSilverSmall",      //  8 Silver      тир 2
        "XenoSlimeBluespaceSmall",   //  9 Bluespace   тир 3 (SS13 2.5)
        "XenoSlimeSepiaSmall",       // 10 Sepia       тир 3 (SS13 2.5)
        "XenoSlimeCeruleanSmall",    // 11 Cerulean    тир 3 (SS13 2.5)
        "XenoSlimePyriteSmall",      // 12 Pyrite      тир 3 (SS13 2.5)
        "XenoSlimeGreenSmall",       // 13 Green       тир 4 (SS13 3)
        "XenoSlimeRedSmall",         // 14 Red         тир 4 (SS13 3)
        "XenoSlimePinkSmall",        // 15 Pink        тир 4 (SS13 3)
        "XenoSlimeGoldSmall",        // 16 Gold        тир 4 (SS13 3)
        "XenoSlimeOilSmall",         // 17 Oil         тир 5 (SS13 4)
        "XenoSlimeBlackSmall",       // 18 Black       тир 5 (SS13 4)
        "XenoSlimeLightPinkSmall",   // 19 LightPink   тир 5 (SS13 4)
        "XenoSlimeAdamantineSmall",  // 20 Adamantine  тир 5 (SS13 4)
        "XenoSlimeRainbowSmall",     // 21 Rainbow     тир 6 (SS13 5)
    };
    private static readonly string[] LargeProtos =
    {
        "XenoSlimeGreyLarge",        //  0 Grey
        "XenoSlimeOrangeLarge",      //  1 Orange
        "XenoSlimePurpleLarge",      //  2 Purple
        "XenoSlimeBlueLarge",        //  3 Blue
        "XenoSlimeMetalLarge",       //  4 Metal
        "XenoSlimeYellowLarge",      //  5 Yellow
        "XenoSlimeDarkPurpleLarge",  //  6 DarkPurple
        "XenoSlimeDarkBlueLarge",    //  7 DarkBlue
        "XenoSlimeSilverLarge",      //  8 Silver
        "XenoSlimeBluespaceLarge",   //  9 Bluespace
        "XenoSlimeSepiaLarge",       // 10 Sepia
        "XenoSlimeCeruleanLarge",    // 11 Cerulean
        "XenoSlimePyriteLarge",      // 12 Pyrite
        "XenoSlimeGreenLarge",       // 13 Green
        "XenoSlimeRedLarge",         // 14 Red
        "XenoSlimePinkLarge",        // 15 Pink
        "XenoSlimeGoldLarge",        // 16 Gold
        "XenoSlimeOilLarge",         // 17 Oil
        "XenoSlimeBlackLarge",       // 18 Black
        "XenoSlimeLightPinkLarge",   // 19 LightPink
        "XenoSlimeAdamantineLarge",  // 20 Adamantine
        "XenoSlimeRainbowLarge",     // 21 Rainbow
    };
    private static readonly string[] OldProtos =
    {
        "XenoSlimeGreyOld",          //  0 Grey
        "XenoSlimeOrangeOld",        //  1 Orange
        "XenoSlimePurpleOld",        //  2 Purple
        "XenoSlimeBlueOld",          //  3 Blue
        "XenoSlimeMetalOld",         //  4 Metal
        "XenoSlimeYellowOld",        //  5 Yellow
        "XenoSlimeDarkPurpleOld",    //  6 DarkPurple
        "XenoSlimeDarkBlueOld",      //  7 DarkBlue
        "XenoSlimeSilverOld",        //  8 Silver
        "XenoSlimeBluespaceOld",     //  9 Bluespace
        "XenoSlimeSepiaOld",         // 10 Sepia
        "XenoSlimeCeruleanOld",      // 11 Cerulean
        "XenoSlimePyriteOld",        // 12 Pyrite
        "XenoSlimeGreenOld",         // 13 Green
        "XenoSlimeRedOld",           // 14 Red
        "XenoSlimePinkOld",          // 15 Pink
        "XenoSlimeGoldOld",          // 16 Gold
        "XenoSlimeOilOld",           // 17 Oil
        "XenoSlimeBlackOld",         // 18 Black
        "XenoSlimeLightPinkOld",     // 19 LightPink
        "XenoSlimeAdamantineOld",    // 20 Adamantine
        "XenoSlimeRainbowOld",       // 21 Rainbow
    };
    private static readonly string[] AncientProtos =
    {
        "XenoSlimeGreyAncient",          //  0 Grey
        "XenoSlimeOrangeAncient",        //  1 Orange
        "XenoSlimePurpleAncient",        //  2 Purple
        "XenoSlimeBlueAncient",          //  3 Blue
        "XenoSlimeMetalAncient",         //  4 Metal
        "XenoSlimeYellowAncient",        //  5 Yellow
        "XenoSlimeDarkPurpleAncient",    //  6 DarkPurple
        "XenoSlimeDarkBlueAncient",      //  7 DarkBlue
        "XenoSlimeSilverAncient",        //  8 Silver
        "XenoSlimeBluespaceAncient",     //  9 Bluespace
        "XenoSlimeSepiaAncient",         // 10 Sepia
        "XenoSlimeCeruleanAncient",      // 11 Cerulean
        "XenoSlimePyriteAncient",        // 12 Pyrite
        "XenoSlimeGreenAncient",         // 13 Green
        "XenoSlimeRedAncient",           // 14 Red
        "XenoSlimePinkAncient",          // 15 Pink
        "XenoSlimeGoldAncient",          // 16 Gold
        "XenoSlimeOilAncient",           // 17 Oil
        "XenoSlimeBlackAncient",         // 18 Black
        "XenoSlimeLightPinkAncient",     // 19 LightPink
        "XenoSlimeAdamantineAncient",    // 20 Adamantine
        "XenoSlimeRainbowAncient",       // 21 Rainbow
    };

    /// <summary>Шанс мутации по тиру (индекс = тир, 0..6).</summary>
    private static readonly float[] MutationChance = { 0.90f, 0.80f, 0.65f, 0.50f, 0.35f, 0.20f, 0f };

    /// <summary>
    /// Карта допустимых мутаций: какие цвета МОЖЕТ дать данный цвет при делении.
    /// Терминальные тир-5 дают только Rainbow; Rainbow — терминальный (пустой массив).
    ///
    /// Дерево эволюции (SS13-стиль):
    ///   Тир 0: Grey → [Orange, Purple, Blue, Metal]
    ///   Тир 1: Orange      → [Yellow, DarkPurple]
    ///          Purple      → [DarkPurple, DarkBlue]
    ///          Blue        → [Silver, DarkBlue]
    ///          Metal       → [Yellow, Silver]
    ///   Тир 2: Yellow      → [Sepia, Pyrite]
    ///          DarkPurple  → [Sepia, Bluespace]
    ///          DarkBlue    → [Pyrite, Cerulean]
    ///          Silver      → [Cerulean, Bluespace]
    ///   Тир 3: Sepia       → [Red, Gold]
    ///          Bluespace   → [Green]
    ///          Cerulean    → [Pink]
    ///          Pyrite      → [Gold, Green]
    ///   Тир 4: Red         → [Oil]
    ///          Green       → [Black]
    ///          Pink        → [LightPink]
    ///          Gold        → [Adamantine]
    ///   Тир 5: Oil/Black/LightPink/Adamantine → [Rainbow]
    ///   Тир 6: Rainbow     → [] (терминальный)
    /// </summary>
    private static readonly Dictionary<XenoSlimeColor, XenoSlimeColor[]> OffspringMap = new()
    {
        // Тир 0
        [XenoSlimeColor.Grey]        = new[] { XenoSlimeColor.Orange,      XenoSlimeColor.Purple,
                                               XenoSlimeColor.Blue,         XenoSlimeColor.Metal },
        // Тир 1
        [XenoSlimeColor.Orange]      = new[] { XenoSlimeColor.Yellow,      XenoSlimeColor.DarkPurple },
        [XenoSlimeColor.Purple]      = new[] { XenoSlimeColor.DarkPurple,  XenoSlimeColor.DarkBlue },
        [XenoSlimeColor.Blue]        = new[] { XenoSlimeColor.Silver,      XenoSlimeColor.DarkBlue },
        [XenoSlimeColor.Metal]       = new[] { XenoSlimeColor.Yellow,      XenoSlimeColor.Silver },
        // Тир 2
        [XenoSlimeColor.Yellow]      = new[] { XenoSlimeColor.Sepia,       XenoSlimeColor.Pyrite },
        [XenoSlimeColor.DarkPurple]  = new[] { XenoSlimeColor.Sepia,       XenoSlimeColor.Bluespace },
        [XenoSlimeColor.DarkBlue]    = new[] { XenoSlimeColor.Pyrite,      XenoSlimeColor.Cerulean },
        [XenoSlimeColor.Silver]      = new[] { XenoSlimeColor.Cerulean,    XenoSlimeColor.Bluespace },
        // Тир 3 (SS13 2.5)
        [XenoSlimeColor.Sepia]       = new[] { XenoSlimeColor.Red,         XenoSlimeColor.Gold },
        [XenoSlimeColor.Bluespace]   = new[] { XenoSlimeColor.Green },
        [XenoSlimeColor.Cerulean]    = new[] { XenoSlimeColor.Pink },
        [XenoSlimeColor.Pyrite]      = new[] { XenoSlimeColor.Gold,        XenoSlimeColor.Green },
        // Тир 4 (SS13 3)
        [XenoSlimeColor.Red]         = new[] { XenoSlimeColor.Oil },
        [XenoSlimeColor.Green]       = new[] { XenoSlimeColor.Black },
        [XenoSlimeColor.Pink]        = new[] { XenoSlimeColor.LightPink },
        [XenoSlimeColor.Gold]        = new[] { XenoSlimeColor.Adamantine },
        // Тир 5 (SS13 4) — производят только Rainbow
        [XenoSlimeColor.Oil]         = new[] { XenoSlimeColor.Rainbow },
        [XenoSlimeColor.Black]       = new[] { XenoSlimeColor.Rainbow },
        [XenoSlimeColor.LightPink]   = new[] { XenoSlimeColor.Rainbow },
        [XenoSlimeColor.Adamantine]  = new[] { XenoSlimeColor.Rainbow },
        // Тир 6 — терминальный
        [XenoSlimeColor.Rainbow]     = Array.Empty<XenoSlimeColor>(),
    };

    /// <summary>Фракция слайма в нейтральном состоянии.</summary>
    private const string FactionNeutral = "XenoSlimeFaction";
    // FactionAggressive удалена — вместо неё используется HTNSystem.SetHTNEnabled
    /// <summary>Скорость потери сытости: 50 % за 4 минуты (спавн → агрессия).</summary>
    private const float HungerDrainRate = 50f / 240f;
    /// <summary>Скорость роста сытости при переваривании: +70 % за 60 секунд.</summary>
    private const float DigestHungerRate = 70f / 60f;
    /// <summary>Таймер периодического сканирования критованной еды рядом.</summary>
    private float _slimeScanTimer = 0f;

    // ─── инициализация ───────────────────────────────────────────────────

    public override void Initialize()
    {
        base.Initialize();

        // Инициализируем желудок слайма при старте
        SubscribeLocalEvent<XenoSlimeComponent, ComponentStartup>(OnSlimeStartup);
        // Слайм бьёт кого-то — может запустить процесс проглатывания
        SubscribeLocalEvent<XenoSlimeComponent, MeleeHitEvent>(OnSlimeMeleeHit);
        // Делаем подписку на изменение MobState еды: при переходе в крит/смерть
        // автоматически запускаем DoAfter на слайме, который бил последним
        SubscribeLocalEvent<XenoSlimeFoodComponent, MobStateChangedEvent>(OnFoodStateChanged);
        // DoAfter процесса проглатывания завершћн
        SubscribeLocalEvent<XenoSlimeComponent, XenoSlimeSwallowDoAfterEvent>(OnSwallowDoAfter);
        // При гибели слайма — дроп экстракта
        SubscribeLocalEvent<XenoSlimeComponent, MobStateChangedEvent>(OnSlimeMobStateChanged);
        // Инъекция реагентов в слайма
        SubscribeLocalEvent<XenoSlimeComponent, XenoSlimeInjectedEvent>(OnSlimeInjected);    }

    private void OnSlimeStartup(EntityUid uid, XenoSlimeComponent _, ComponentStartup args)
    {
        // Создаём контейнер-желудок, в который будут помещаться тела поглощённых сущностей
        _containers.EnsureContainer<Container>(uid, XenoSlimeComponent.StomachContainerId);
    }

    // ─── тик голода ──────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Периодический флаг: поддерживать агрессивную фракцию + искать критованную еду
        _slimeScanTimer += frameTime;
        var doScan = _slimeScanTimer >= 1f;
        if (doScan) _slimeScanTimer = 0f;

        // Список слаймов для отложенной обработки (ПОСЛЕ итерации, чтобы избежать
        // "Collection was modified" — Spawn/QueueDel меняют структуры ECS).
        var toTransform = new List<(EntityUid uid, XenoSlimeComponent slime, XenoSlimeAge stage)>();
        var toFeed      = new List<(EntityUid uid, XenoSlimeComponent slime)>();

        var query = EntityQueryEnumerator<XenoSlimeComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var slime, out var mob))
        {
            // Мёртвые слаймы не голодают и не стареют
            if (mob.CurrentState == MobState.Dead)
                continue;

            // Переваривание: сытость растёт на 70 % за 60 секунд; при 100 % — рост/деление
            if (slime.IsDigesting)
            {
                slime.HungerPercent += DigestHungerRate * frameTime;
                slime.AgeSeconds    += frameTime;
                if (slime.HungerPercent >= 100f)
                {
                    slime.HungerPercent = 100f;
                    slime.IsDigesting   = false;
                    slime.DigestTimer   = 0f;
                    toFeed.Add((uid, slime));
                }
                continue; // голод не убывает пока переваривает
            }

            // Страховка: если цель пропала (удалена/съедена другим) — сбрасываем
            if (slime.SwallowTarget != null && Deleted(slime.SwallowTarget.Value))
                slime.SwallowTarget = null;

            // Дрейф сытости: идёт всегда (кроме переваривания), даже во время DoAfter
            slime.HungerPercent -= HungerDrainRate * frameTime;
            if (slime.HungerPercent < 0f) slime.HungerPercent = 0f;

            slime.AgeSeconds += frameTime;

            // Голод → агрессия
            if (slime.Mood == XenoSlimeMood.Neutral && slime.HungerPercent <= 0f)
                SetMood(uid, slime, XenoSlimeMood.Aggressive);

            // HTN: включён всегда, кроме момента активного поглощения (DoAfter).
            // Нейтральный слайм тоже атакует обезьяну — чтобы ввести в крит и потом съесть.
            SetHTNActive(uid, slime.SwallowTarget == null);

            // Раз в секунду: ищем еду рядом
            if (doScan)
            {
                if (slime.SwallowTarget == null)
                    TryPickCritFoodTarget(uid, slime);
            }

            // Проверяем переход в стадию Old/Ancient (только для взрослых)
            if (slime.IsAdult && NeedsAgeTransition(slime))
            {
                var stage = slime.AgeSeconds >= XenoSlimeComponent.AncientAgeThreshold
                    ? XenoSlimeAge.Ancient
                    : XenoSlimeAge.Old;
                toTransform.Add((uid, slime, stage));
            }
        }

        // Применяем FeedSlime после окончания итерации — избегаем изменения коллекций
        foreach (var (uid, slime) in toFeed)
        {
            if (!Deleted(uid))
                FeedSlime((uid, slime));
        }

        // Применяем возрастные переходы ПОСЛЕ окончания итерации
        foreach (var (uid, slime, stage) in toTransform)
        {
            if (!Deleted(uid))
                TransformSlimeAgeStage(uid, slime, stage);
        }
    }

    /// <summary>
    /// Управляет реакцией на еду рядом:
    /// — крит/мёртвая → запускаем DoAfter поглощения;
    /// — живая        → добавляем агрессивную фракцию, чтобы NPC атаковал её;
    /// — нет еды      → если нейтральный — убираем временную агрессию охоты.
    /// </summary>
    private void TryPickCritFoodTarget(EntityUid slimeUid, XenoSlimeComponent slime)
    {
        var slimeCoords = _transform.GetMapCoordinates(slimeUid);

        // Ищем крит/мёртвую еду → запускаем DoAfter поглощения
        // (HTN управляется в Update — здесь только инициируем глотание)
        if (slime.SwallowTarget != null || slime.IsDigesting)
            return;

        foreach (var (foodUid, foodComp) in _lookup.GetEntitiesInRange<XenoSlimeFoodComponent>(slimeCoords, 5f))
        {
            if (Deleted(foodUid)) continue;
            if (slime.Friends.Contains(foodUid)) continue;
            if (foodComp.ClaimedBySlime is { } claimedBy && claimedBy != slimeUid) continue;
            if (!TryComp<MobStateComponent>(foodUid, out var foodMob)) continue;
            if (foodMob.CurrentState != MobState.Critical && foodMob.CurrentState != MobState.Dead) continue;

            foodComp.ClaimedBySlime = slimeUid;
            slime.SwallowTarget = foodUid;
            var started = _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager, slimeUid, slime.SwallowDuration,
                new XenoSlimeSwallowDoAfterEvent(), slimeUid, target: foodUid)
            {
                BreakOnMove        = false,
                BreakOnHandChange  = false,
                BlockDuplicate     = true,
                DuplicateCondition = DuplicateConditions.SameTool,
            });
            if (!started)
            {
                slime.SwallowTarget = null;
                if (foodComp.ClaimedBySlime == slimeUid)
                    foodComp.ClaimedBySlime = null;
            }
            return;
        }
    }

    // ─── слайм ударил кого-то ────────────────────────────────────────────

    private void OnSlimeMeleeHit(EntityUid slimeUid, XenoSlimeComponent slime, MeleeHitEvent args)
    {
        var mutableList = args.HitEntities as List<EntityUid>;

        for (var i = (mutableList?.Count ?? args.HitEntities.Count) - 1; i >= 0; i--)
        {
            var target = args.HitEntities[i];

            // Защита друзей
            if (slime.Friends.Contains(target))
            {
                mutableList?.RemoveAt(i);
                continue;
            }

            // Запоминаем последнего атакующего на будущее
            if (TryComp<XenoSlimeFoodComponent>(target, out var foodComp))
                foodComp.LastAttackerSlime = slimeUid;
        }
    }

    // ─── еда перешла в крит/смерть ─────────────────────────────────

    private void OnFoodStateChanged(EntityUid foodUid, XenoSlimeFoodComponent food, MobStateChangedEvent args)
    {
        // Работаем только при переходе в крит или смерть
        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        // Очищаем устаревший claim.
        if (food.ClaimedBySlime is { } claimedBy
            && (Deleted(claimedBy)
                || !TryComp<XenoSlimeComponent>(claimedBy, out var claimedComp)
                || claimedComp.SwallowTarget != foodUid))
        {
            food.ClaimedBySlime = null;
        }

        // Слайм-атакующий должен быть живым и свободным
        EntityUid? slimeUid = null;
        XenoSlimeComponent? slimeComp = null;

        if (food.LastAttackerSlime is { } attacker
            && !IsDeadOrDeleted(attacker)
            && TryComp(attacker, out XenoSlimeComponent? sc)
            && sc.SwallowTarget == null
            && !sc.IsDigesting
            && (food.ClaimedBySlime == null || food.ClaimedBySlime == attacker)
            && !sc.Friends.Contains(foodUid))
        {
            slimeUid = attacker;
            slimeComp = sc;
        }
        else
        {
            // Если последний атакующий недоступен — ищем ближайшего свободного
            var foodCoords = _transform.GetMapCoordinates(foodUid);
            var bestDist = float.MaxValue;

            foreach (var (uid, comp) in _lookup.GetEntitiesInRange<XenoSlimeComponent>(foodCoords, 6f))
            {
                if (IsDeadOrDeleted(uid) || comp.SwallowTarget != null || comp.IsDigesting)
                    continue;
                if (comp.Friends.Contains(foodUid))
                    continue;
                if (food.ClaimedBySlime is { } claimedOther && claimedOther != uid)
                    continue;

                var d = (_transform.GetMapCoordinates(uid).Position - foodCoords.Position).Length();
                if (d < bestDist)
                {
                    bestDist   = d;
                    slimeUid   = uid;
                    slimeComp  = comp;
                }
            }
        }

        if (slimeUid == null || slimeComp == null)
            return;

        if (food.ClaimedBySlime is { } claimedByOther && claimedByOther != slimeUid.Value)
            return;

        food.ClaimedBySlime = slimeUid.Value;

        // Записываем цель и запускаем DoAfter процесса проглатывания (HTN выключится в Update пока SwallowTarget != null)
        slimeComp.SwallowTarget = foodUid;

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            slimeUid.Value,
            slimeComp.SwallowDuration,
            new XenoSlimeSwallowDoAfterEvent(),
            slimeUid.Value,
            target: foodUid)
        {
            BreakOnMove        = false,  // еда уже не сопротивляется
            BreakOnHandChange  = false,
            BlockDuplicate     = true,
            DuplicateCondition = DuplicateConditions.SameTool,
        });
        if (!started)
        {
            slimeComp.SwallowTarget = null;
            if (food.ClaimedBySlime == slimeUid.Value)
                food.ClaimedBySlime = null;
        }
    }

    private void OnSwallowDoAfter(EntityUid slimeUid, XenoSlimeComponent slime, XenoSlimeSwallowDoAfterEvent args)
    {
        // В любом случае разблокируем слот — слайм снова свободен
        slime.SwallowTarget = null;

        if (args.Cancelled)
        {
            if (args.Args.Target is { } cancelledFood
                && TryComp<XenoSlimeFoodComponent>(cancelledFood, out var cancelledFoodComp)
                && cancelledFoodComp.ClaimedBySlime == slimeUid)
            {
                cancelledFoodComp.ClaimedBySlime = null;
            }

            // Ретрай: если цель ещё крит/мертва — немедленно начинаем снова
            if (args.Args.Target is { } retryFood
                && !Deleted(retryFood)
                && TryComp<XenoSlimeFoodComponent>(retryFood, out var retryFoodComp)
                && TryComp<MobStateComponent>(retryFood, out var retryMob)
                && (retryMob.CurrentState == MobState.Critical || retryMob.CurrentState == MobState.Dead))
            {
                if (retryFoodComp.ClaimedBySlime is { } claimedBy && claimedBy != slimeUid)
                    return;

                retryFoodComp.ClaimedBySlime = slimeUid;

                // HTN выключится автоматически в Update пока SwallowTarget != null
                slime.SwallowTarget = retryFood;
                var retryStarted = _doAfter.TryStartDoAfter(new DoAfterArgs(
                    EntityManager, slimeUid, slime.SwallowDuration,
                    new XenoSlimeSwallowDoAfterEvent(), slimeUid, target: retryFood)
                {
                    BreakOnMove        = false,  // еда уже не сопротивляется
                    BreakOnHandChange  = false,
                    BlockDuplicate     = true,
                    DuplicateCondition = DuplicateConditions.SameTool,
                });
                if (!retryStarted)
                {
                    slime.SwallowTarget = null;
                    if (retryFoodComp.ClaimedBySlime == slimeUid)
                        retryFoodComp.ClaimedBySlime = null;
                }
            }
            return;
        }

        if (args.Args.Target == null)
            return;

        var foodUid = args.Args.Target.Value;

        if (Deleted(foodUid))
            return;

        if (!TryComp<XenoSlimeFoodComponent>(foodUid, out var foodComp)
            || foodComp.ClaimedBySlime != slimeUid)
            return;

        var foodCoords = _transform.GetMapCoordinates(foodUid);

        // Тело втягивается в желудок слайма (исчезает из мира)
        var stomach = _containers.EnsureContainer<Container>(slimeUid, XenoSlimeComponent.StomachContainerId);
        if (!_containers.Insert(foodUid, stomach))
        {
            if (foodComp.ClaimedBySlime == slimeUid)
                foodComp.ClaimedBySlime = null;
            return;
        }

        // Цель успешно захвачена желудком.
        if (foodComp.ClaimedBySlime == slimeUid)
            foodComp.ClaimedBySlime = null;

        // Запоминаем игроков только после успешного проглатывания.
        TrackNearbyFeeders(slimeUid, slime, foodCoords);

        // Начинаем постепенное переваривание: +70 % за 60 секунд (в Update)
        // При достижении 100 % слайм вырастет/разделится автоматически
        slime.IsDigesting = true;
        slime.DigestTimer = 0f;
        SetMood(slimeUid, slime, XenoSlimeMood.Neutral);
    }

    // ─── вспомогательные ─────────────────────────────────────────────────

    /// <summary>
    /// Ищет игроков (ActorComponent) рядом с координатами еды и увеличивает счётчик кормлений.
    /// При достижении порога (2) — добавляет игрока в список друзей слайма.
    /// </summary>
    private void TrackNearbyFeeders(EntityUid slimeUid, XenoSlimeComponent slime, MapCoordinates foodCoords)
    {
        foreach (var (playerUid, _) in _lookup.GetEntitiesInRange<ActorComponent>(foodCoords, slime.FeederDetectRange))
        {
            slime.FeedCounts.TryGetValue(playerUid, out var count);
            count++;
            slime.FeedCounts[playerUid] = count;

            if (count >= 2)
                slime.Friends.Add(playerUid);
        }
    }

    /// <summary>Сытость: возвращаем нейтральное настроение, затем рост/деление.</summary>
    private void FeedSlime(Entity<XenoSlimeComponent> slime)
    {
        var comp = slime.Comp;

        SetMood(slime.Owner, comp, XenoSlimeMood.Neutral);

        // Рост / деление
        var coords = _transform.GetMapCoordinates(slime.Owner);
        var color  = comp.Color;

        if (!comp.IsAdult)
        {
            // Маленький → взрослый
            QueueDel(slime.Owner);
            var child = Spawn(GetLargeProto(color), coords);
            if (TryComp<XenoSlimeComponent>(child, out var childComp))
            {
                childComp.StabilizationLevel = comp.StabilizationLevel;
                childComp.SteroidCount       = comp.SteroidCount;
                childComp.AgeSeconds         = comp.AgeSeconds; // передаём возраст
                CopyFriendState(comp, childComp);
            }
        }
        else
        {
            var mutBoost = comp.MutationBoost;
            comp.MutationBoost = 0;
            QueueDel(slime.Owner);

            // Поведение зависит от возрастной стадии
            switch (comp.AgeStage)
            {
                case XenoSlimeAge.Old:
                    SpawnOffspringOld(coords, color, comp.Tier, comp.StabilizationLevel, mutBoost, comp.AgeSeconds);
                    break;
                case XenoSlimeAge.Ancient:
                    SpawnOffspringAncient(coords, color, comp.Tier, comp.StabilizationLevel, mutBoost, comp.AgeSeconds);
                    break;
                default:
                    SpawnOffspring(coords, color, comp.Tier, comp.StabilizationLevel, mutBoost);
                    break;
            }
        }
    }

    /// <summary>Меняем настроение слайма (только mood, HTN управляется в Update).</summary>
    private void SetMood(EntityUid uid, XenoSlimeComponent slime, XenoSlimeMood mood)
    {
        if (slime.Mood == mood)
            return;

        slime.Mood = mood;
    }

    /// <summary>Включает или выключает HTN у слайма. Идемпотентно.</summary>
    private void SetHTNActive(EntityUid uid, bool active)
    {
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), active);
    }

    /// <summary>
    /// Спавним 2–3 маленьких слайма:
    ///   1 гарантированно того же цвета,
    ///   остальные — выбираются из OffspringMap с вероятностью мутации по тиру.
    ///   Если цвет терминальный (OffspringMap пустой) — все потомки того же цвета.
    /// </summary>
    private void SpawnOffspring(MapCoordinates coords, XenoSlimeColor color, byte tier, byte stabilizationLevel = 0, byte mutationBoost = 0)
    {
        // Гарантированный потомок того же цвета
        ApplyOffspringStabilization(Spawn(GetSmallProto(color), coords), stabilizationLevel);

        // 2 или 3 всего (50/50)
        var count = _random.Prob(0.5f) ? 3 : 2;

        // Терминальный цвет: нет допустимых мутаций — все клоны
        if (!OffspringMap.TryGetValue(color, out var allowed) || allowed.Length == 0)
        {
            for (var i = 1; i < count; i++)
                ApplyOffspringStabilization(Spawn(GetSmallProto(color), coords), stabilizationLevel);
            return;
        }

        // Базовый шанс мутации - снижаем стабилизатором (максимально до 0), повышаем MutationBoost
        var effectiveTier = Math.Clamp(tier - mutationBoost, 0, MutationChance.Length - 1);
        var baseMutChance = MutationChance[effectiveTier];
        var mutChance     = Math.Max(0f, baseMutChance - stabilizationLevel * 0.15f);
        for (var i = 1; i < count; i++)
        {
            // С вероятностью mutChance — мутируем в один из разрешённых цветов
            var spawnColor = _random.Prob(mutChance)
                ? _random.Pick(allowed)
                : color;
            ApplyOffspringStabilization(Spawn(GetSmallProto(spawnColor), coords), stabilizationLevel);
        }
    }

    private void ApplyOffspringStabilization(EntityUid child, byte stabilizationLevel)
    {
        if (stabilizationLevel <= 0 || !TryComp<XenoSlimeComponent>(child, out var childComp))
            return;

        childComp.StabilizationLevel = stabilizationLevel;
    }

    private bool IsDeadOrDeleted(EntityUid uid)
    {
        if (Deleted(uid)) return true;
        if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead) return true;
        return false;
    }

    private static void CopyFriendState(XenoSlimeComponent from, XenoSlimeComponent to)
    {
        to.Friends.UnionWith(from.Friends);
        foreach (var (friendUid, count) in from.FeedCounts)
            to.FeedCounts[friendUid] = count;
    }

    private static string GetSmallProto(XenoSlimeColor color)   => SmallProtos[(int) color];
    private static string GetLargeProto(XenoSlimeColor color)   => LargeProtos[(int) color];
    private static string GetOldProto(XenoSlimeColor color)     => OldProtos[(int) color];
    private static string GetAncientProto(XenoSlimeColor color) => AncientProtos[(int) color];

    /// <summary>Возвращает true, если взрослый слайм должен перейти в Old или Ancient.</summary>
    private static bool NeedsAgeTransition(XenoSlimeComponent slime)
    {
        if (slime.AgeStage == XenoSlimeAge.Ancient)
            return false;
        if (slime.AgeStage != XenoSlimeAge.Old && slime.AgeSeconds >= XenoSlimeComponent.AncientAgeThreshold)
            return true;
        if (slime.AgeStage < XenoSlimeAge.Old && slime.AgeSeconds >= XenoSlimeComponent.OldAgeThreshold)
            return true;
        return false;
    }

    // ─── дроп экстракта при гибели слайма ────────────────────────────────

    // Прототипы экстрактов (индекс = (int)XenoSlimeColor)
    private static readonly string[] ExtractProtos =
    {
        "XenoSlimeExtractGrey",        //  0 Grey
        "XenoSlimeExtractOrange",      //  1 Orange
        "XenoSlimeExtractPurple",      //  2 Purple
        "XenoSlimeExtractBlue",        //  3 Blue
        "XenoSlimeExtractMetal",       //  4 Metal
        "XenoSlimeExtractYellow",      //  5 Yellow
        "XenoSlimeExtractDarkPurple",  //  6 DarkPurple
        "XenoSlimeExtractDarkBlue",    //  7 DarkBlue
        "XenoSlimeExtractSilver",      //  8 Silver
        "XenoSlimeExtractBluespace",   //  9 Bluespace
        "XenoSlimeExtractSepia",       // 10 Sepia
        "XenoSlimeExtractCerulean",    // 11 Cerulean
        "XenoSlimeExtractPyrite",      // 12 Pyrite
        "XenoSlimeExtractGreen",       // 13 Green
        "XenoSlimeExtractRed",         // 14 Red
        "XenoSlimeExtractPink",        // 15 Pink
        "XenoSlimeExtractGold",        // 16 Gold
        "XenoSlimeExtractOil",         // 17 Oil
        "XenoSlimeExtractBlack",       // 18 Black
        "XenoSlimeExtractLightPink",   // 19 LightPink
        "XenoSlimeExtractAdamantine",  // 20 Adamantine
        "XenoSlimeExtractRainbow",     // 21 Rainbow
    };

    private static string GetExtractProto(XenoSlimeColor color) => ExtractProtos[(int) color];

    /// <summary>
    /// При гибели слайма — ничего не дропаем. Экстракты производит только дробилка (XenoSlimeCrusherSystem).
    /// </summary>
    private void OnSlimeMobStateChanged(EntityUid uid, XenoSlimeComponent comp, MobStateChangedEvent args)
    {
        // Экстракт намеренно не спавнится при смерти — только через дробилку.
    }

    // ─── возрастные переходы ─────────────────────────────────────────────

    /// <summary>
    /// Удаляет текущую сущность и спавнит новую с нужным прото (Old/Ancient),
    /// передавая баффы, возраст и голод.
    /// </summary>
    private void TransformSlimeAgeStage(EntityUid uid, XenoSlimeComponent slime, XenoSlimeAge newStage)
    {
        var coords   = _transform.GetMapCoordinates(uid);
        var protoId  = newStage == XenoSlimeAge.Old
            ? GetOldProto(slime.Color)
            : GetAncientProto(slime.Color);

        QueueDel(uid);

        var newUid = Spawn(protoId, coords);
        if (!TryComp<XenoSlimeComponent>(newUid, out var newComp))
            return;

        // Передаём все параметры
        newComp.StabilizationLevel = slime.StabilizationLevel;
        newComp.SteroidCount       = slime.SteroidCount;
        newComp.MutationBoost      = slime.MutationBoost;
        newComp.HungerPercent      = slime.HungerPercent;
        newComp.AgeSeconds         = slime.AgeSeconds;
        newComp.AgeStage           = newStage;
        CopyFriendState(slime, newComp);

        // Синхронизируем настроение
        if (slime.Mood == XenoSlimeMood.Aggressive)
            SetMood(newUid, newComp, XenoSlimeMood.Aggressive);
    }

    /// <summary>
    /// Старый слайм делится: 1 Large того же цвета + 1-2 мутанта Small.
    /// </summary>
    private void SpawnOffspringOld(MapCoordinates coords, XenoSlimeColor color, byte tier,
        byte stabilizationLevel = 0, byte mutationBoost = 0, float ageSeconds = 0f)
    {
        // Гарантированный Large потомок
        ApplyOffspringStabilization(Spawn(GetLargeProto(color), coords), stabilizationLevel);

        // 2 или 3 всего (50/50)
        var count = _random.Prob(0.5f) ? 3 : 2;

        if (!OffspringMap.TryGetValue(color, out var allowed) || allowed.Length == 0)
        {
            for (var i = 1; i < count; i++)
                ApplyOffspringStabilization(Spawn(GetSmallProto(color), coords), stabilizationLevel);
            return;
        }

        var effectiveTier = Math.Clamp(tier - mutationBoost, 0, MutationChance.Length - 1);
        var baseMutChance = MutationChance[effectiveTier];
        var mutChance     = Math.Max(0f, baseMutChance - stabilizationLevel * 0.15f);

        for (var i = 1; i < count; i++)
        {
            var spawnColor = _random.Prob(mutChance) ? _random.Pick(allowed) : color;
            ApplyOffspringStabilization(Spawn(GetSmallProto(spawnColor), coords), stabilizationLevel);
        }
    }

    /// <summary>
    /// Древний слайм делится: 1 Old + 1 Large + 2 Small (с шансом мутации).
    /// </summary>
    private void SpawnOffspringAncient(MapCoordinates coords, XenoSlimeColor color, byte tier,
        byte stabilizationLevel = 0, byte mutationBoost = 0, float ageSeconds = 0f)
    {
        // Гарантированный Old потомок (наследует ~половину возраста)
        var oldChild = Spawn(GetOldProto(color), coords);
        if (TryComp<XenoSlimeComponent>(oldChild, out var oldComp))
        {
            oldComp.StabilizationLevel = stabilizationLevel;
            oldComp.SteroidCount       = 0;
            oldComp.AgeSeconds         = XenoSlimeComponent.OldAgeThreshold; // стартует уже Old
        }

        // Гарантированный Large
        var largeChild = Spawn(GetLargeProto(color), coords);
        ApplyOffspringStabilization(largeChild, stabilizationLevel);

        // 1 или 2 малых потомка с мутацией
        var count = _random.Prob(0.5f) ? 2 : 1;

        if (!OffspringMap.TryGetValue(color, out var allowed) || allowed.Length == 0)
        {
            for (var i = 0; i < count; i++)
                ApplyOffspringStabilization(Spawn(GetSmallProto(color), coords), stabilizationLevel);
            return;
        }

        var effectiveTier = Math.Clamp(tier - mutationBoost, 0, MutationChance.Length - 1);
        var baseMutChance = MutationChance[effectiveTier];
        var mutChance     = Math.Max(0f, baseMutChance - stabilizationLevel * 0.15f);

        for (var i = 0; i < count; i++)
        {
            var spawnColor = _random.Prob(mutChance) ? _random.Pick(allowed) : color;
            ApplyOffspringStabilization(Spawn(GetSmallProto(spawnColor), coords), stabilizationLevel);
        }
    }

    // -- Инъекция реагентов в слайма -------------------------------------------

    /// <summary>
    /// Вызывается когда в слайма вводят реагент через шприц/гипоспрей.
    /// Обрабатывает: SlimeStabilizer, SlimeSteroid, XenoSlimeGrowthFactor.
    /// </summary>
    private void OnSlimeInjected(EntityUid uid, XenoSlimeComponent comp, XenoSlimeInjectedEvent args)
    {
        switch (args.ReagentId)
        {
            case "SlimeStabilizer":
                if (comp.StabilizationLevel < 3)
                    comp.StabilizationLevel++;
                break;

            case "SlimeSteroid":
                if (comp.SteroidCount < 3)
                    comp.SteroidCount++;
                break;

            case "XenoSlimeGrowthFactor":
                ApplyGrowthFactor(uid, comp);
                break;
        }
    }

    /// <summary>
    /// Применяет фактор роста к слайму.
    /// Если слайм маленький  мгновенно вырастает во взрослого.
    /// Если взрослый  сразу становится голодным (немедленно делится).
    /// </summary>
    public void ApplyGrowthFactor(EntityUid uid, XenoSlimeComponent comp)
    {
        var coords = _transform.GetMapCoordinates(uid);
        var color  = comp.Color;

        if (!comp.IsAdult)
        {
            // Защита от повторной обработки до фактического удаления сущности.
            comp.IsAdult = true;

            // Маленький → мгновенно взрослый
            QueueDel(uid);
            var adult = Spawn(GetLargeProto(color), coords);
            if (TryComp<XenoSlimeComponent>(adult, out var adultComp))
            {
                adultComp.StabilizationLevel = comp.StabilizationLevel;
                adultComp.SteroidCount       = comp.SteroidCount;
                adultComp.AgeSeconds         = comp.AgeSeconds;
                CopyFriendState(comp, adultComp);
            }
        }
        else
        {
            // Взрослый → немедленно голоден (агрессивен, после еды разделится)
            comp.HungerPercent = 0f;
            SetMood(uid, comp, XenoSlimeMood.Aggressive);
        }
    }
}
