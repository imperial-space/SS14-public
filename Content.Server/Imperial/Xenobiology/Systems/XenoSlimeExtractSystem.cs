using Content.Server.Atmos.EntitySystems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Xenobiology.Systems;

/// <summary>
/// Обрабатывает эффекты экстрактов ксено-слаймов при инъекции реагентов.
///
/// Механика (SS13 xenobiology):
///   1. Игрок вводит Кровь / Плазму / Воду в раствор "slime" экстракта через шприц.
///   2. Система перехватывает SolutionContainerChangedEvent.
///   3. В зависимости от типа эффекта:
///      - ProduceReagent → наполняет раствор "food" нужным реагентом.
///        Игрок забирает его шприцом ИЛИ перерабатывает в блендере.
///      - SpawnItems / SpawnMob → спавн сущностей на месте экстракта, экстракт исчезает.
///      - EmpPulse / StartFire / Explosion → мгновенный эффект, экстракт исчезает.
///      - MakeSlimesBerserk → ближайшие слаймы агрессивны, экстракт исчезает.
///      - SnapCool → охлаждение газа в тайле, экстракт исчезает.
///      - SpawnRandomSlime / SpawnRandomFromPool → случайный спавн, экстракт исчезает.
/// </summary>
public sealed class XenoSlimeExtractSystem : EntitySystem
{
    private const string SimpleHostileFaction = "SimpleHostile";

    [Dependency] private readonly SharedSolutionContainerSystem _solutions  = default!;
    [Dependency] private readonly SharedPopupSystem             _popup      = default!;
    [Dependency] private readonly FirestarterSystem             _fire       = default!;
    [Dependency] private readonly EmpSystem                     _emp        = default!;
    [Dependency] private readonly ExplosionSystem               _explosion  = default!;
    [Dependency] private readonly EntityLookupSystem            _lookup     = default!;
    [Dependency] private readonly TransformSystem               _xform      = default!;
    [Dependency] private readonly AtmosphereSystem              _atmos      = default!;
    [Dependency] private readonly IRobustRandom                 _random     = default!;
    [Dependency] private readonly NpcFactionSystem              _factionSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoSlimeExtractComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    // ── Реакция на изменение раствора ────────────────────────────────────────
    private void OnSolutionChanged(EntityUid uid, XenoSlimeExtractComponent comp,
        ref SolutionContainerChangedEvent args)
    {
        if (comp.Used)
            return;
        if (args.SolutionId != comp.SolutionName)
            return;

        var sol = args.Solution;
        var minAmount = FixedPoint2.New(comp.TriggerMinAmount);

        // Проверяем зелье усилителя экстракторов (Cerulean plasma)
        const string amplifierReagentId = "XenoCeruleanPotion";
        if (sol.GetTotalPrototypeQuantity(amplifierReagentId) >= FixedPoint2.New(1)
            && !HasComp<XenoAmplifiedExtractComponent>(uid))
        {
            AddComp<XenoAmplifiedExtractComponent>(uid);
            _popup.PopupCoordinates(
                Loc.GetString("xeno-extract-amplified"),
                Transform(uid).Coordinates);
            // Удаляем использованный усилитель из раствора
            if (_solutions.TryGetSolution(uid, comp.SolutionName, out var solEnt, out _))
            {
                _solutions.RemoveReagent(solEnt.Value, amplifierReagentId, FixedPoint2.New(1));
                _solutions.UpdateChemicals(solEnt.Value);
            }
            return;
        }

        XenoExtractEffect? effect   = null;
        string?            triggerId = null;

        if (sol.GetTotalPrototypeQuantity(comp.BloodReagentId)  >= minAmount && comp.BloodEffect  != null)
        {
            effect    = comp.BloodEffect;
            triggerId = comp.BloodReagentId;
        }
        else if (sol.GetTotalPrototypeQuantity(comp.PlasmaReagentId) >= minAmount && comp.PlasmaEffect != null)
        {
            effect    = comp.PlasmaEffect;
            triggerId = comp.PlasmaReagentId;
        }
        else if (sol.GetTotalPrototypeQuantity(comp.WaterReagentId)  >= minAmount && comp.WaterEffect  != null)
        {
            effect    = comp.WaterEffect;
            triggerId = comp.WaterReagentId;
        }

        if (effect == null || triggerId == null)
            return;

        comp.Used = true;

        // Удаляем триггерный реагент из раствора "slime"
        if (_solutions.TryGetSolution(uid, comp.SolutionName, out var slimeSolEnt, out _))
        {
            _solutions.RemoveReagent(slimeSolEnt.Value, triggerId, minAmount);
            _solutions.UpdateChemicals(slimeSolEnt.Value);
        }

        // Показываем popup игроку рядом
        if (effect.UseMessage != null)
            _popup.PopupCoordinates(effect.UseMessage, Transform(uid).Coordinates);

        // Применяем эффект с учётом усиления
        var amplifier = CompOrNull<XenoAmplifiedExtractComponent>(uid);
        ApplyEffect(uid, effect, amplifier?.Multiplier ?? 1);
    }

    // ── Применение эффекта ───────────────────────────────────────────────────
    private void ApplyEffect(EntityUid uid, XenoExtractEffect effect, int amplifier = 1)
    {
        var xform  = Transform(uid);
        var coords = xform.Coordinates;
        var mapCoords = _xform.GetMapCoordinates(xform);

        switch (effect.Type)
        {
            // --- Продукция реагента в food-solution -->шприц или блендер --------
            case XenoExtractEffectType.ProduceReagent:
            {
                if (effect.ProduceReagent == null) break;

                // Убеждаемся что food-раствор существует
                _solutions.EnsureSolution(uid, "food", out _, FixedPoint2.New(80 * amplifier));

                if (_solutions.TryGetSolution(uid, "food", out var foodEnt, out _))
                {
                    var toAdd = new Solution();
                    toAdd.AddReagent(effect.ProduceReagent, FixedPoint2.New(effect.ProduceAmount * amplifier));
                    var _ = _solutions.TryAddSolution(foodEnt.Value, toAdd);
                }
                // Экстракт остаётся — его можно выжать шприцом или перемолоть
                break;
            }

            // --- Спавн предметов -------------------------------------------------
            case XenoExtractEffectType.SpawnItems:
            {
                foreach (var proto in effect.SpawnEntities)
                    Spawn(proto, coords);
                QueueDel(uid);
                break;
            }

            // --- Спавн моба/существа --------------------------------------------
            case XenoExtractEffectType.SpawnMob:
            {
                if (effect.MobPrototype != null)
                    Spawn(effect.MobPrototype.Value, coords);
                QueueDel(uid);
                break;
            }

            // --- Случайный слайм ------------------------------------------------
            case XenoExtractEffectType.SpawnRandomSlime:
            {
                EntProtoId picked = effect.RandomSlimePool.Count > 0
                    ? _random.Pick(effect.RandomSlimePool)
                    : PickRandomSlime();
                Spawn(picked, coords);
                QueueDel(uid);
                break;
            }

            // --- Случайная сущность из пула ------------------------------------
            case XenoExtractEffectType.SpawnRandomFromPool:
            {
                if (effect.RandomPool.Count > 0)
                    Spawn(_random.Pick(effect.RandomPool), coords);
                QueueDel(uid);
                break;
            }

            // --- ЭМИ импульс ---------------------------------------------------
            case XenoExtractEffectType.EmpPulse:
            {
                _emp.EmpPulse(mapCoords,
                    effect.EmpRange,
                    effect.EmpEnergyConsumption,
                    effect.EmpDuration);
                QueueDel(uid);
                break;
            }

            // --- Поджигание ----------------------------------------------------
            case XenoExtractEffectType.StartFire:
            {
                _fire.IgniteNearby(uid, coords, effect.FireSeverity, effect.FireRange);
                QueueDel(uid);
                break;
            }

            // --- Взрыв ---------------------------------------------------------
            case XenoExtractEffectType.Explosion:
            {
                _explosion.QueueExplosion(uid,
                    effect.ExplosionTypeId,
                    effect.ExplosionTotalIntensity,
                    effect.ExplosionSlope,
                    effect.ExplosionMaxTileIntensity);
                QueueDel(uid); // удаляем экстракт после взрыва
                break;
            }

            // --- Агрессия слаймов в радиусе ------------------------------------
            case XenoExtractEffectType.MakeSlimesBerserk:
            {
                var slimes = new HashSet<Entity<XenoSlimeComponent>>();
                _lookup.GetEntitiesInRange(coords, effect.BerserkerRange, slimes);
                foreach (var slime in slimes)
                {
                    slime.Comp.Mood          = XenoSlimeMood.Aggressive;
                    slime.Comp.HungerPercent = 0f;
                    // Добавляем агрессивную фракцию — слайм атакует игрока
                    // XenoSlimeFaction остаётся — слайм также атакует обезьян
                    _factionSys.AddFaction(slime.Owner, SimpleHostileFaction, dirty: true);
                }
                QueueDel(uid);
                break;
            }

            // --- Заморозка тайла -----------------------------------------------
            case XenoExtractEffectType.SnapCool:
            {
                var gridUid = xform.GridUid;
                if (gridUid == null)
                {
                    _popup.PopupCoordinates(Loc.GetString("xeno-extract-no-grid"), coords);
                    break;
                }

                var mapUid  = xform.MapUid;
                var tile    = _xform.GetGridTilePositionOrDefault((uid, xform));
                var range   = (int) Math.Ceiling(effect.SnapCoolRange);

                for (var dx = -range; dx <= range; dx++)
                for (var dy = -range; dy <= range; dy++)
                {
                    if (dx * dx + dy * dy > range * range)
                        continue;

                    var tilePos = new Vector2i(tile.X + dx, tile.Y + dy);
                    var mix = _atmos.GetTileMixture(gridUid, mapUid, tilePos, excite: true);
                    if (mix != null)
                        mix.Temperature = Math.Max(Math.Min(mix.Temperature, effect.SnapCoolTemperature), 1f);
                }
                QueueDel(uid);
                break;
            }
            // --- Нагрев тайла ------------------------------------
            case XenoExtractEffectType.SnapHeat:
            {
                var gridUidH = xform.GridUid;
                if (gridUidH == null)
                {
                    _popup.PopupCoordinates(Loc.GetString("xeno-extract-no-grid"), coords);
                    break;
                }

                var mapUidH  = xform.MapUid;
                var tileH    = _xform.GetGridTilePositionOrDefault((uid, xform));
                var rangeH   = (int) Math.Ceiling(effect.SnapHeatRange);

                for (var dx = -rangeH; dx <= rangeH; dx++)
                for (var dy = -rangeH; dy <= rangeH; dy++)
                {
                    if (dx * dx + dy * dy > rangeH * rangeH)
                        continue;

                    var tilePos = new Vector2i(tileH.X + dx, tileH.Y + dy);
                    var mix = _atmos.GetTileMixture(gridUidH, mapUidH, tilePos, excite: true);
                    if (mix != null)
                        mix.Temperature = Math.Max(mix.Temperature, effect.SnapHeatTemperature);
                }
                QueueDel(uid);
                break;
            }
        }
    }

    // ── Вспомогательные ──────────────────────────────────────────────────────

    private static readonly string[] RandomSlimeProtos =
    {
        "XenoSlimeOrangeSmall", "XenoSlimePurpleSmall", "XenoSlimeBlueSmall", "XenoSlimeMetalSmall",
        "XenoSlimeYellowSmall", "XenoSlimeDarkPurpleSmall", "XenoSlimeDarkBlueSmall", "XenoSlimeSilverSmall",
        "XenoSlimeBluespaceSmall", "XenoSlimeSepiaSmall", "XenoSlimeCeruleanSmall", "XenoSlimePyriteSmall",
        "XenoSlimeGreenSmall", "XenoSlimeRedSmall", "XenoSlimePinkSmall", "XenoSlimeGoldSmall",
    };

    private EntProtoId PickRandomSlime() => _random.Pick(RandomSlimeProtos);
}
