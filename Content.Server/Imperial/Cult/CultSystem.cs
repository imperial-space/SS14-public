using Content.Server.Actions;
using System.Linq;
using System.Numerics;
using Content.Server.Antag;
using Content.Server.AlertLevel;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Shared.Antag;
using Content.Server.DoAfter;
using Content.Server.Emp;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Construction.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Mind.Components;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Actions;
using Content.Shared.Cuffs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Maps;
using Content.Shared.Overlays;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Pinpointer;
using Content.Shared.Speech.Muting;
using Content.Shared.Players;
using Content.Server.Damage.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Основная система культа Нар'Си.
/// </summary>
public sealed class CultSystem : EntitySystem
{
    private const float NarSieBeaconRange = 4f;
    private const float NarSieDrawTime = 80f;
    private const string CultMagicSound = "/Audio/Effects/desecration-01.ogg";
    private const string NarSieRitualMusic = "/Audio/Imperial/cult/Tear-of-veil.ogg";
    private static readonly TimeSpan CultReagentCheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HolyWaterDeconversionDelay = TimeSpan.FromSeconds(150);
    private static readonly FixedPoint2 HolyWaterDeconversionThreshold = FixedPoint2.New(40);

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly CultRuleSystem _cultRule = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly CultShieldSystem _cultShield = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    private readonly Dictionary<EntityUid, List<EntityUid>> _activeNarSieBarriers = new();

    public bool IsCultAligned(EntityUid uid)
    {
        return HasComp<CultistComponent>(uid) || HasComp<CultConstructComponent>(uid);
    }

    // Стартовые способности (Commune + Blood Magic)
    private static readonly EntProtoId[] CultistStartActions =
    {
        "ActionCultCommune",
        "ActionCultBloodMagic",
    };

    // Заклинания, доступные через Blood Magic
    private static readonly EntProtoId[] CultSpells =
    {
        "ActionCultStun",
        "ActionCultShackles",
        "ActionCultTeleport",
        "ActionCultEmp",
        "ActionCultTwistedConstruction",
        "ActionCultSummonDagger",
        "ActionCultSummonEquipment",
        "ActionCultConcealPresence",
        "ActionCultBloodRites",
    };

    private static readonly EntProtoId RunedMetalProto = "CultRunedMetal";
    private static readonly EntProtoId CultDaggerProto = "CultDagger";
    private static readonly EntProtoId ActionCultRecallBloodSpear = "ActionCultRecallBloodSpear";

    // Фразы культа, которые жертва выкрикивает в радио под эффектом стана
    private static readonly string[] CultRadioPhrases =
    {
        "Gal'h'rfikk harfrandid mud'gib!",
        "H'drak v'loso, mir'kanas!",
        "Nab'arkanos s'ranath shek!",
        "Khal'ithar gub'veth mir!",
        "Sas'so c'arta forbici!",
        "Nar'suk meh'dast vel'tarak!",
        "Barhah dag'a forren!",
    };

    // Лимит слотов заклинаний
    private const int SpellLimitNormal = 1;
    private const int SpellLimitEmpowered = 4;

    // Стоимость подготовки заклинания (brute damage)
    private const float SpellBloodCost = 20f;

    // Время подготовки заклинания (секунды)
    private const float SpellPrepTime = 10f;

    public override void Initialize()
    {
        base.Initialize();

        // Commune  всегда доступен
        SubscribeLocalEvent<CultistComponent, CultCommuneActionEvent>(OnCommune);

        // Заклинания (вызываются после выдачи через Blood Magic)
        SubscribeLocalEvent<CultistComponent, CultStunActionEvent>(OnStun);
        SubscribeLocalEvent<CultistComponent, CultShacklesActionEvent>(OnShackles);
        SubscribeLocalEvent<CultistComponent, CultTeleportActionEvent>(OnTeleport);
        SubscribeLocalEvent<CultistComponent, CultEmpActionEvent>(OnEmp);
        SubscribeLocalEvent<CultistComponent, CultTwistedConstructionActionEvent>(OnTwistedConstruction);
        SubscribeLocalEvent<CultistComponent, CultSummonDaggerActionEvent>(OnSummonDagger);
        SubscribeLocalEvent<CultistComponent, CultSummonEquipmentActionEvent>(OnSummonEquipment);
        SubscribeLocalEvent<CultistComponent, CultConcealPresenceActionEvent>(OnConcealPresence);
        SubscribeLocalEvent<CultistComponent, CultBloodRitesActionEvent>(OnBloodRites);
        SubscribeLocalEvent<CultistComponent, CultRecallBloodSpearActionEvent>(OnRecallBloodSpear);
        SubscribeLocalEvent<CultBloodSpearComponent, ComponentShutdown>(OnBloodSpearShutdown);

        // Blood Magic  открывает окно выбора заклинания
        SubscribeLocalEvent<CultistComponent, CultBloodMagicActionEvent>(OnBloodMagicAction);

        // Рисование рун  Z-клавиша открывает BUI
        SubscribeLocalEvent<CultDaggerComponent, UseInHandEvent>(OnDaggerUseInHand);
        SubscribeLocalEvent<CultistComponent, DrawRuneDoAfterEvent>(OnDrawRuneDoAfter);

        // BUI сообщения от кинжала (руны)
        SubscribeLocalEvent<CultDaggerComponent, CultSelectRuneMessage>(OnRuneSelected);
        SubscribeLocalEvent<CultDaggerComponent, CultTeleportSelectMessage>(OnTeleportRune);

        // BUI сообщения от CultBuiHolder (Blood Magic + Commune — без кинжала)
        SubscribeLocalEvent<CultBuiHolderComponent, CultSpellSelectedMessage>(OnSpellSelected);
        SubscribeLocalEvent<CultBuiHolderComponent, CultCommuneTextMessage>(OnCommuneTextMessage);
        SubscribeLocalEvent<CultBuiHolderComponent, CultSpellSwapMessage>(OnSpellSwap);
        // Пропускаем проверку дистанции для держателя BUI культиста
        SubscribeLocalEvent<CultBuiHolderComponent, BoundUserInterfaceCheckRangeEvent>(OnBuiHolderRangeCheck);

        // Рунный металл — меню постройки
        SubscribeLocalEvent<CultRunedMetalComponent, UseInHandEvent>(OnRunedMetalUseInHand);

        // Структуры культа — создание предметов (алтарь/кузница/архив)
        SubscribeLocalEvent<CultStructureComponent, ActivateInWorldEvent>(OnStructureActivate);
        SubscribeLocalEvent<CultStructureComponent, CultStructureCreateMessage>(OnStructureCreate);

        // DoAfter подготовки заклинания
        SubscribeLocalEvent<CultistComponent, PrepareSpellDoAfterEvent>(OnPrepareSpellDoAfter);

        // Выдача способностей при старте компонента
        SubscribeLocalEvent<CultistComponent, ComponentStartup>(OnCultistStartup);
        SubscribeLocalEvent<CultistComponent, ExaminedEvent>(OnCultistExamined);

        // Предметы-заклинания (hand items)
        SubscribeLocalEvent<CultSpellItemComponent, AfterInteractEvent>(OnSpellItemAfterInteract);
        SubscribeLocalEvent<CultSpellItemComponent, UseInHandEvent>(OnSpellItemUseInHand);
        SubscribeLocalEvent<CultSpellItemComponent, CultTeleportSelectMessage>(OnSpellItemTeleportSelect);
        SubscribeLocalEvent<CultSpellItemComponent, CultBloodRitesChoiceMessage>(OnBloodRitesChoice);

        // Повязка фанатика
        SubscribeLocalEvent<CultFanaticBandageComponent, GotEquippedEvent>(OnFanaticBandageEquipped);
        SubscribeLocalEvent<CultFanaticBandageComponent, GotUnequippedEvent>(OnFanaticBandageUnequipped);
        SubscribeLocalEvent<CanSeeConcealedComponent, GetVisMaskEvent>(OnCanSeeConcealedGetVis);

        // Руническая дверь — блокировка для не-культистов
        SubscribeLocalEvent<CultStructureComponent, BeforeDoorOpenedEvent>(OnCultAirlockOpenAttempt);

        // Якорение/открепление только ритуальным кинжалом
        SubscribeLocalEvent<CultStructureComponent, AnchorAttemptEvent>(OnCultStructureAnchorAttempt);
        SubscribeLocalEvent<CultStructureComponent, UnanchorAttemptEvent>(OnCultStructureUnanchorAttempt);
        // Ритуальный кинжал обходит проверку ToolQuality Anchoring
        SubscribeLocalEvent<CultStructureComponent, InteractUsingEvent>(OnCultStructureDaggerInteract);
    }

    private void OnCultStructureDaggerInteract(EntityUid uid, CultStructureComponent comp, InteractUsingEvent args)
    {
        if (args.Handled) return;
        if (!HasComp<CultDaggerComponent>(args.Used)) return;

        var xform = Transform(uid);
        if (xform.Anchored)
        {
            _xform.Unanchor(uid, xform);
            _popup.PopupEntity(Loc.GetString("anchorable-unanchored"), uid, args.User);
        }
        else
        {
            _xform.AnchorEntity(uid, xform);
            _popup.PopupEntity(Loc.GetString("anchorable-anchored"), uid, args.User);
        }
        args.Handled = true;
    }

    private void OnCultStructureAnchorAttempt(EntityUid uid, CultStructureComponent comp, AnchorAttemptEvent args)
    {
        if (!HasComp<CultDaggerComponent>(args.Tool))
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("cult-structure-anchor-dagger-required"), args.User, args.User);
        }
    }

    private void OnCultStructureUnanchorAttempt(EntityUid uid, CultStructureComponent comp, UnanchorAttemptEvent args)
    {
        if (!HasComp<CultDaggerComponent>(args.Tool))
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("cult-structure-anchor-dagger-required"), args.User, args.User);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CultSpeechAffectedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.ExpiresAt)
            {
                RemCompDeferred<CultSpeechAffectedComponent>(uid);
                continue;
            }
            if (now >= comp.NextPhrase)
            {
                comp.NextPhrase = now + TimeSpan.FromSeconds(3 + _random.NextDouble() * 3);
                var phrase = _random.Pick(CultRadioPhrases);
                _radio.SendRadioMessage(uid, phrase, "Common", uid);
            }
        }

        UpdatePylons(now);
        UpdateCultChemicals(now);
    }

    private void UpdateCultChemicals(TimeSpan now)
    {
        var query = EntityQueryEnumerator<CultistComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var cultist, out var bloodstream))
        {
            if (now < cultist.NextReagentCheck)
                continue;

            cultist.NextReagentCheck = now + CultReagentCheckInterval;

            if (!_solutionContainer.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var chemicalSolution))
                continue;

            var holyWaterAmount = chemicalSolution.GetTotalPrototypeQuantity("Holywater");
            if (holyWaterAmount >= HolyWaterDeconversionThreshold)
            {
                cultist.HolyWaterThresholdReachedAt ??= now;
                if (now - cultist.HolyWaterThresholdReachedAt.Value >= HolyWaterDeconversionDelay)
                {
                    RemoveCultist(uid);
                    continue;
                }
            }
            else
            {
                cultist.HolyWaterThresholdReachedAt = null;
            }

        }
    }

    private static readonly TimeSpan PylonTickInterval = TimeSpan.FromSeconds(5);
    private const float PylonRange = 4f;

    private void UpdatePylons(TimeSpan now)
    {
        // Сначала собираем все пилоны в список, чтобы не получить
        // Collection Modified при Spawn/QueueDel внутри итерации.
        var pylons = new List<(EntityUid uid, CultStructureComponent comp, TransformComponent xform)>();
        var pylonQuery = EntityQueryEnumerator<CultStructureComponent, TransformComponent>();
        while (pylonQuery.MoveNext(out var pylonUid, out var structure, out var pylonXform))
        {
            if (structure.StructureType != CultStructureType.Pylon) continue;
            if (now < structure.NextPylonTick) continue;
            pylons.Add((pylonUid, structure, pylonXform));
        }

        foreach (var (pylonUid, structure, pylonXform) in pylons)
        {
            structure.NextPylonTick = now + PylonTickInterval;

            var coords = pylonXform.Coordinates;
            var nearby = new HashSet<EntityUid>();
            _lookup.GetEntitiesInRange(coords, PylonRange, nearby);

            EntityUid? wallToConvert = null;
            EntityUid? airlockToConvert = null;

            foreach (var entity in nearby)
            {
                if (entity == pylonUid)
                    continue;

                // Ищем ближайшую некультовую стену для конвертации
                if (wallToConvert == null
                    && _tagSystem.HasTag(entity, "Wall")
                    && MetaData(entity).EntityPrototype?.ID != "WallCult")
                {
                    wallToConvert = entity;
                }

                // Ищем ближайший некультовый шлюз для конвертации
                if (airlockToConvert == null
                    && HasComp<AirlockComponent>(entity)
                    && !HasComp<CultStructureComponent>(entity))
                {
                    airlockToConvert = entity;
                }
            }

            // Конвертируем одну стену → WallCult
            if (wallToConvert.HasValue)
            {
                var wallCoords = Transform(wallToConvert.Value).Coordinates;
                QueueDel(wallToConvert.Value);
                Spawn("WallCult", wallCoords);
            }

            // Конвертируем один шлюз → CultRunedAirlock
            if (airlockToConvert.HasValue)
            {
                var airlockCoords = Transform(airlockToConvert.Value).Coordinates;
                var originalProto = MetaData(airlockToConvert.Value).EntityPrototype?.ID;
                QueueDel(airlockToConvert.Value);
                var cultAirlock = Spawn("CultRunedAirlock", airlockCoords);
                if (TryComp<CultStructureComponent>(cultAirlock, out var cultStructComp))
                {
                    cultStructComp.OriginalProto = originalProto;
                    Dirty(cultAirlock, cultStructComp);
                }
            }

            // Постепенно конвертируем тайлы пола в FloorCult: 2 ближайших за тик
            if (pylonXform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
            {
                var worldPos = _xform.GetWorldPosition(pylonXform);
                if (_tileDefManager.TryGetDefinition("FloorCult", out var cultTileDef))
                {
                    const int tilesPerTick = 2;
                    var toConvert = _mapSystem.GetTilesIntersecting(gridUid, grid,
                            new Circle(worldPos, PylonRange), false)
                        .Where(t => t.Tile.TypeId != cultTileDef.TileId)
                        .OrderBy(t => ((Vector2)t.GridIndices - worldPos).LengthSquared())
                        .Take(tilesPerTick);

                    foreach (var tileRef in toConvert)
                    {
                        _mapSystem.SetTile(gridUid, grid, tileRef.GridIndices,
                            new Tile(cultTileDef.TileId));
                    }
                }
            }
        }
    }

    private void OnCultAirlockOpenAttempt(EntityUid uid, CultStructureComponent comp, BeforeDoorOpenedEvent args)
    {
        if (comp.StructureType != CultStructureType.RunedAirlock) return;
        if (args.User == null) return;
        if (IsCultAligned(args.User.Value)) return;

        // Блокируем открытие
        args.Cancel();

        // Отталкиваем на 3 тайла
        var userPos = _xform.GetWorldPosition(Transform(args.User.Value));
        var doorPos = _xform.GetWorldPosition(Transform(uid));
        var pushDir = userPos - doorPos;
        if (pushDir.Length() > 0.01f)
            pushDir = pushDir.Normalized() * 3f;
        else
            pushDir = new Vector2(3f, 0f);

        _throwing.TryThrow(args.User.Value, pushDir, 8f);

        // 40 урона стамины
        _stamina.TakeStaminaDamage(args.User.Value, 40f);

        _popup.PopupEntity("Руна отвергает вас!", args.User.Value, args.User.Value, PopupType.MediumCaution);
    }

    private void OnCultistStartup(EntityUid uid, CultistComponent comp, ComponentStartup args)
    {
        var startupGrantedCultRole = false;

        foreach (var actionProto in CultistStartActions)
        {
            _actions.AddAction(uid, actionProto);
        }
        // Настраиваем радио культа для приёма-передачи Commune
        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        var activeRadio = EnsureComp<ActiveRadioComponent>(uid);
        if (!activeRadio.Channels.Contains("Cult"))
            activeRadio.Channels.Add("Cult");

        // Иконки антага — нужны и начальным культистам (via game rule), и конвертированным
        EnsureComp<ShowAntagIconsComponent>(uid);

        // Создаём держатель BUI для Blood Magic и Commune (без кинжала).
        // Выполняется здесь, чтобы работало и для начальных культистов
        // (AntagSelection добавляет компонент напрямую, минуя AddCultist).
        var holder = Spawn("CultBuiHolder", Transform(uid).Coordinates);
        _xform.SetParent(holder, uid);
        comp.BuiHolder = holder;

        if (_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            if (!_role.MindHasRole<CultistRoleComponent>(mindId))
            {
                _role.MindAddRole(mindId, "MindRoleCultist", mind: mind, silent: true);
                startupGrantedCultRole = true;
            }

            _mind.TryAddObjective(mindId, mind, "CultSacrificeTargetsObjective");
            _mind.TryAddObjective(mindId, mind, "CultSummonNarSieObjective");
        }

        if (startupGrantedCultRole)
        {
            Spawn("CultMarkEffect", Transform(uid).Coordinates);
        }

        // Перечищаем все существующие CultistComponent-state,
        // чтобы новый культист получил компоненты других культистов
        var query = EntityQueryEnumerator<CultistComponent>();
        while (query.MoveNext(out var cUid, out var cComp))
            Dirty(cUid, cComp);

        CheckCultGrowth();
    }

    // ─── State Filtering ─────────────────────────────────────────────────────

    //  API 

    /// <summary>
    /// Конвертирует существо в культиста. Возвращает false если невозможно.
    /// </summary>
    public bool TryConvertToCultist(EntityUid target, EntityUid? instigator = null)
    {
        if (!TryComp<MindContainerComponent>(target, out var mindContainer))
            return false;

        if (HasComp<MindShieldComponent>(target))
            return false;

        if (HasComp<CultistComponent>(target))
            return false;

        if (_mobState.IsDead(target))
            return false;

        AddCultist(target);
        return true;
    }

    /// <summary>
    /// Добавляет компонент культиста и выдаёт кинжал.
    /// </summary>
    public void AddCultist(EntityUid uid)
    {
        EnsureComp<CultistComponent>(uid);  // Запускает OnCultistStartup → создаёт BuiHolder, ShowAntagIcons

        // Radio настраивается через OnCultistStartup
        _audio.PlayPvs(CultMagicSound, uid);

        // Пентаграмма при обращении
        Spawn("CultMarkEffect", Transform(uid).Coordinates);

        var dagger = Spawn(CultDaggerProto, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, dagger);

        // Добавляем роль и цели (для ручного назначения через админа)
        if (_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            if (!_role.MindHasRole<CultistRoleComponent>(mindId))
                _role.MindAddRole(mindId, "MindRoleCultist", mind: mind, silent: true);
            _mind.TryAddObjective(mindId, mind, "CultSacrificeTargetsObjective");
            _mind.TryAddObjective(mindId, mind, "CultSummonNarSieObjective");
        }

        // Проверяем пороги роста культа
        CheckCultGrowth();
    }

    /// <summary>
    /// Удаляет культиста (деконверсия).
    /// </summary>
    public void RemoveCultist(EntityUid uid)
    {
        if (!TryComp<CultistComponent>(uid, out var cultist))
            return;

        if (cultist.ActiveNarSieRitualAudio.HasValue && Exists(cultist.ActiveNarSieRitualAudio.Value))
        {
            _audio.SetState(cultist.ActiveNarSieRitualAudio.Value, AudioState.Stopped);
            QueueDel(cultist.ActiveNarSieRitualAudio.Value);
        }

        foreach (var actEnt in cultist.GrantedActions)
        {
            if (Exists(actEnt))
                _actions.RemoveAction(uid, actEnt);
        }

        // Восстанавливаем оригинальный цвет глаз
        if (cultist.OriginalEyeColor.HasValue && TryComp<HumanoidAppearanceComponent>(uid, out var humanoidRestore))
        {
            humanoidRestore.EyeColor = cultist.OriginalEyeColor.Value;
            Dirty(uid, humanoidRestore);
        }

        if (cultist.BuiHolder.HasValue && Exists(cultist.BuiHolder.Value))
            QueueDel(cultist.BuiHolder.Value);

        RemComp<IntrinsicRadioReceiverComponent>(uid);
        RemComp<ActiveRadioComponent>(uid);
        RemComp<CultistComponent>(uid);
        _popup.PopupEntity(Loc.GetString("cult-deconverted"), uid, uid, PopupType.Large);
    }

    /// <summary>
    /// Перечислить всех культистов.
    /// </summary>
    public EntityQueryEnumerator<CultistComponent> GetAllCultists()
        => EntityQueryEnumerator<CultistComponent>();

    //  Blood Magic 

    private void OnBloodMagicAction(EntityUid uid, CultistComponent comp, CultBloodMagicActionEvent args)
    {
        args.Handled = true;

        if (comp.BuiHolder.HasValue)
            _ui.TryOpenUi(comp.BuiHolder.Value, CultBloodMagicBuiKey.Key, uid);
    }

    private void OnSpellSelected(EntityUid uid, CultBuiHolderComponent comp, CultSpellSelectedMessage args)
    {
        var cultist = args.Actor;
        if (!TryComp<CultistComponent>(cultist, out var cultistComp))
            return;

        var spellId = args.SpellId;

        // Проверяем что это допустимое заклинание
        if (!IsValidSpellId(spellId))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-invalid"), cultist, cultist);
            return;
        }

        // Проверяем лимит слотов
        var limit = cultistComp.OnEmpowerRune ? SpellLimitEmpowered : SpellLimitNormal;
        if (cultistComp.ActiveSpellCount >= limit)
        {
            // Показываем окно замены заклинания
            _ui.SetUiState(uid, CultBloodMagicBuiKey.Key,
                new CultBloodMagicSwapState(spellId, new List<string>(cultistComp.PreparedSpells)));
            _ui.TryOpenUi(uid, CultBloodMagicBuiKey.Key, cultist);
            return;
        }

        // Проверяем что заклинание ещё не подготовлено
        if (cultistComp.PreparedSpells.Contains(spellId))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-already-prepared"), cultist, cultist);
            return;
        }

        // Запускаем подготовку (DoAfter)
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, cultist, SpellPrepTime,
            new PrepareSpellDoAfterEvent { SpellId = spellId },
            eventTarget: cultist)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });

        _popup.PopupEntity(
            Loc.GetString("cult-preparing-spell", ("spell", Loc.GetString(GetSpellLocKey(spellId)))),
            cultist, cultist);
        _audio.PlayPvs(CultMagicSound, cultist, AudioParams.Default.WithVolume(-6));
    }

    private void OnPrepareSpellDoAfter(EntityUid uid, CultistComponent comp, PrepareSpellDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            if (TryComp<CultistComponent>(uid, out var cComp) && cComp.BuiHolder.HasValue)
                _ui.TryOpenUi(cComp.BuiHolder.Value, CultBloodMagicBuiKey.Key, uid);
            return;
        }

        if (args.Handled)
            return;
        args.Handled = true;

        var spellId = args.SpellId;
        if (spellId == null)
            return;

        // Финальная проверка
        if (!IsValidSpellId(spellId))
            return;
        if (comp.PreparedSpells.Contains(spellId))
            return;
        var limit = comp.OnEmpowerRune ? SpellLimitEmpowered : SpellLimitNormal;
        if (comp.ActiveSpellCount >= limit)
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-limit-reached"), uid, uid);
            return;
        }

        // Оплата кровью
        DealSelfDamage(uid, SpellBloodCost);

        // Выдаём заклинание
        _actions.AddAction(uid, spellId);

        comp.ActiveSpellCount++;
        comp.PreparedSpells.Add(spellId);
        if (ShouldTrackPreparedUses(spellId))
            comp.PreparedSpellUses[spellId] = GetInitialSpellUses(spellId);

        _popup.PopupEntity(
            Loc.GetString("cult-spell-ready", ("spell", Loc.GetString(GetSpellLocKey(spellId)))),
            uid, uid, PopupType.Medium);
        _audio.PlayPvs(CultMagicSound, uid);
    }

    // ─── Spell Swap (когда слоты заняты) ─────────────────────────────────────

    private void OnSpellSwap(EntityUid uid, CultBuiHolderComponent comp, CultSpellSwapMessage args)
    {
        var cultist = args.Actor;
        if (!TryComp<CultistComponent>(cultist, out var cultistComp))
            return;

        var oldSpellId = args.OldSpellId;
        var newSpellId = args.NewSpellId;

        if (!IsValidSpellId(newSpellId) || !cultistComp.PreparedSpells.Contains(oldSpellId))
            return;

        RemovePreparedSpellAction(cultist, cultistComp, oldSpellId);

        // Проверяем что новое заклинание можно добавить
        if (!IsValidSpellId(newSpellId) || cultistComp.PreparedSpells.Contains(newSpellId))
            return;

        var limit = cultistComp.OnEmpowerRune ? SpellLimitEmpowered : SpellLimitNormal;
        if (cultistComp.ActiveSpellCount >= limit)
            return;

        // Оплата кровью и мгновенная выдача нового заклинания (без DoAfter)
        DealSelfDamage(cultist, SpellBloodCost);
        _actions.AddAction(cultist, newSpellId);
        cultistComp.ActiveSpellCount++;
        cultistComp.PreparedSpells.Add(newSpellId);
        if (ShouldTrackPreparedUses(newSpellId))
            cultistComp.PreparedSpellUses[newSpellId] = GetInitialSpellUses(newSpellId);

        _popup.PopupEntity(
            Loc.GetString("cult-spell-ready", ("spell", Loc.GetString(GetSpellLocKey(newSpellId)))),
            cultist, cultist, PopupType.Medium);
        _audio.PlayPvs(CultMagicSound, cultist);

        // Закрываем BUI после успешной замены — следующее открытие покажет свежее окно выбора
        _ui.CloseUi(uid, CultBloodMagicBuiKey.Key, cultist);
    }

    private static bool ShouldTrackPreparedUses(string actionId)
        => actionId != "ActionCultBloodRites";

    private static int GetInitialSpellUses(string actionId) => actionId switch
    {
        "ActionCultStun" => 4,
        "ActionCultShackles" => 4,
        "ActionCultTeleport" => 1,
        "ActionCultEmp" => 4,
        "ActionCultTwistedConstruction" => 1,
        "ActionCultSummonDagger" => 1,
        "ActionCultSummonEquipment" => 1,
        "ActionCultConcealPresence" => 10,
        _ => 1,
    };

    private void RemovePreparedSpellAction(EntityUid cultist, CultistComponent comp, string actionId)
    {
        foreach (var action in _actions.GetActions(cultist))
        {
            if (MetaData(action.Owner).EntityPrototype?.ID != actionId)
                continue;

            _actions.RemoveAction(cultist, action.Owner);
            break;
        }

        comp.PreparedSpells.Remove(actionId);
        comp.PreparedSpellUses.Remove(actionId);
        comp.ActiveSpellCount = Math.Max(0, comp.PreparedSpells.Count);
    }

    private bool TryConsumePreparedSpellUse(EntityUid cultist, CultistComponent comp, string actionId)
    {
        if (!ShouldTrackPreparedUses(actionId))
            return true;

        if (!comp.PreparedSpellUses.TryGetValue(actionId, out var uses) || uses <= 0)
        {
            RemovePreparedSpellAction(cultist, comp, actionId);
            _popup.PopupEntity(Loc.GetString("cult-spell-no-uses"), cultist, cultist, PopupType.SmallCaution);
            return false;
        }

        uses--;
        if (uses <= 0)
        {
            RemovePreparedSpellAction(cultist, comp, actionId);
            _popup.PopupEntity(Loc.GetString("cult-spell-expended"), cultist, cultist, PopupType.Small);
            return true;
        }

        comp.PreparedSpellUses[actionId] = uses;
        _popup.PopupEntity(Loc.GetString("cult-spell-uses-remaining", ("charges", uses)), cultist, cultist, PopupType.Small);
        return true;
    }

    private bool TryConsumePreparedSpellUseFromItem(EntityUid spellItem, EntityUid cultist, CultistComponent comp)
    {
        if (!TryComp<CultSpellItemComponent>(spellItem, out var spellItemComp) || string.IsNullOrWhiteSpace(spellItemComp.PreparedActionId))
            return true;

        return TryConsumePreparedSpellUse(cultist, comp, spellItemComp.PreparedActionId);
    }

    //  Blood Spells 

    private void OnCommune(EntityUid uid, CultistComponent comp, CultCommuneActionEvent args)
    {
        args.Handled = true;

        if (comp.BuiHolder.HasValue)
            _ui.TryOpenUi(comp.BuiHolder.Value, CultCommuneBuiKey.Key, uid);
    }

    private void OnCommuneTextMessage(EntityUid uid, CultBuiHolderComponent comp, CultCommuneTextMessage args)
    {
        var cultist = args.Actor;
        var text = args.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Культист шёпотом произносит сообщение (слышно рядом — для атмосферы)
        _chat.TrySendInGameICMessage(cultist, text, InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        // Отправляем через рацию (канал Cult) — видно всем культистам в чате
        _radio.SendRadioMessage(cultist, text, "Cult", cultist);
    }

    private void OnStun(EntityUid uid, CultistComponent comp, CultStunActionEvent args)
    {
        args.Handled = true;
        // Если уже держит предмет-стан — отменить (удалить предмет)
        if (TryFindAndDeleteSpellItem(uid, "stun"))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-cancelled"), uid, uid);
            return;
        }

        if (!comp.PreparedSpellUses.ContainsKey("ActionCultStun"))
        {
            RemovePreparedSpellAction(uid, comp, "ActionCultStun");
            _popup.PopupEntity(Loc.GetString("cult-spell-no-uses"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var item = Spawn("CultSpellItemStun", Transform(uid).Coordinates);
        Comp<CultSpellItemComponent>(item).PreparedActionId = "ActionCultStun";
        _hands.TryPickupAnyHand(uid, item);
    }

    private void OnShackles(EntityUid uid, CultistComponent comp, CultShacklesActionEvent args)
    {
        args.Handled = true;
        // Если уже держит предмет-оковы — отменить
        if (TryFindAndDeleteSpellItem(uid, "shackles"))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-cancelled"), uid, uid);
            return;
        }

        if (!comp.PreparedSpellUses.ContainsKey("ActionCultShackles"))
        {
            RemovePreparedSpellAction(uid, comp, "ActionCultShackles");
            _popup.PopupEntity(Loc.GetString("cult-spell-no-uses"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var item = Spawn("CultSpellItemShackles", Transform(uid).Coordinates);
        Comp<CultSpellItemComponent>(item).PreparedActionId = "ActionCultShackles";
        _hands.TryPickupAnyHand(uid, item);
    }

    private void OnTeleport(EntityUid uid, CultistComponent comp, CultTeleportActionEvent args)
    {
        args.Handled = true;
        // Если уже держит предмет-телепорт — отменить
        if (TryFindAndDeleteSpellItem(uid, "teleport"))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-cancelled"), uid, uid);
            return;
        }

        if (!comp.PreparedSpellUses.ContainsKey("ActionCultTeleport"))
        {
            RemovePreparedSpellAction(uid, comp, "ActionCultTeleport");
            _popup.PopupEntity(Loc.GetString("cult-spell-no-uses"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var item = Spawn("CultSpellItemTeleport", Transform(uid).Coordinates);
        Comp<CultSpellItemComponent>(item).PreparedActionId = "ActionCultTeleport";
        _hands.TryPickupAnyHand(uid, item);
    }

    private void OnTeleportRune(EntityUid uid, CultDaggerComponent comp, CultTeleportSelectMessage args)
    {
        // Оставлен для обратной совместимости с кинжалом
        var cultist = args.Actor;
        var runeEntity = GetEntity(args.RuneEntity);

        if (!TryComp<CultRuneComponent>(runeEntity, out var runeComp) || runeComp.RuneType != CultRuneType.Teleport)
        {
            _popup.PopupEntity(Loc.GetString("cult-teleport-no-rune"), cultist, cultist);
            return;
        }

        PerformTeleport(cultist, runeEntity);
    }

    private void OnEmp(EntityUid uid, CultistComponent comp, CultEmpActionEvent args)
    {
        args.Handled = true;

        if (!TryConsumePreparedSpellUse(uid, comp, "ActionCultEmp"))
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("cult-incantation-emp"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        var pos = _xform.GetMapCoordinates(uid);
        _emp.EmpPulse(pos, 5f, 100_000f, TimeSpan.FromSeconds(30));
        Spawn("CultEffectBloodSparkles", Transform(uid).Coordinates);
        _audio.PlayPvs("/Audio/Effects/emp.ogg", uid);
    }

    private void OnTwistedConstruction(EntityUid uid, CultistComponent comp, CultTwistedConstructionActionEvent args)
    {
        args.Handled = true;
        // Если уже держит предмет-строительство — отменить
        if (TryFindAndDeleteSpellItem(uid, "construction"))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-cancelled"), uid, uid);
            return;
        }

        if (!comp.PreparedSpellUses.ContainsKey("ActionCultTwistedConstruction"))
        {
            RemovePreparedSpellAction(uid, comp, "ActionCultTwistedConstruction");
            _popup.PopupEntity(Loc.GetString("cult-spell-no-uses"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var item = Spawn("CultSpellItemConstruction", Transform(uid).Coordinates);
        Comp<CultSpellItemComponent>(item).PreparedActionId = "ActionCultTwistedConstruction";
        _hands.TryPickupAnyHand(uid, item);
    }

    private void OnSummonDagger(EntityUid uid, CultistComponent comp, CultSummonDaggerActionEvent args)
    {
        args.Handled = true;

        if (!TryConsumePreparedSpellUse(uid, comp, "ActionCultSummonDagger"))
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("cult-incantation-dagger"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        var dagger = Spawn(CultDaggerProto, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, dagger);
    }

    private void OnSummonEquipment(EntityUid uid, CultistComponent comp, CultSummonEquipmentActionEvent args)
    {
        args.Handled = true;
        var coords = Transform(uid).Coordinates;

        if (!TryConsumePreparedSpellUse(uid, comp, "ActionCultSummonEquipment"))
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("cult-incantation-equipment"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        // Снимаем текущую одежду (выбросить старую)
        _inventory.TryUnequip(uid, "outerClothing", true, true);
        _inventory.TryUnequip(uid, "head", true, true);
        _inventory.TryUnequip(uid, "shoes", true, true);

        // Спавним и надеваем культистское снаряжение
        var robes = Spawn("ClothingOuterCultRobes", coords);
        var helm  = Spawn("ClothingHeadCultHelmet", coords);
        var shoes = Spawn("ClothingShoesCult", coords);
        _inventory.TryEquip(uid, robes, "outerClothing", true);
        _inventory.TryEquip(uid, helm, "head", true);
        _inventory.TryEquip(uid, shoes, "shoes", true);

        // Оружие в руки
        var blade = Spawn("CultBlade", coords);
        var bola  = Spawn("CultRunedBola", coords);
        _hands.TryPickupAnyHand(uid, blade);
        _hands.TryPickupAnyHand(uid, bola);

        _popup.PopupEntity(Loc.GetString("cult-equipment-summoned"), uid, uid);
    }

    // Маскировка присутствия — удаляет руны и структуры в радиусе 4 тайлов,
    // шлюзы возвращает к оригиналу с доступом культистов, руны-телепорты скрывает
    private void OnConcealPresence(EntityUid uid, CultistComponent comp, CultConcealPresenceActionEvent args)
    {
        args.Handled = true;
        var pos = _xform.GetWorldPosition(uid);
        const float range = 4f;

        if (!TryConsumePreparedSpellUse(uid, comp, "ActionCultConcealPresence"))
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("cult-incantation-conceal"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        // Обрабатываем руны
        var runeQuery = EntityQueryEnumerator<CultRuneComponent, TransformComponent>();
        while (runeQuery.MoveNext(out var runeUid, out var rune, out var runeXform))
        {
            if ((_xform.GetWorldPosition(runeXform) - pos).Length() > range) continue;

            rune.Concealed = !rune.Concealed;
            Dirty(runeUid, rune);
            SetCultConcealed(runeUid, rune.Concealed);
        }

        // Обрабатываем структуры
        var structQuery = EntityQueryEnumerator<CultStructureComponent, TransformComponent>();
        while (structQuery.MoveNext(out var structUid, out var structure, out var structXform))
        {
            if ((_xform.GetWorldPosition(structXform) - pos).Length() > range) continue;

            structure.Concealed = !structure.Concealed;
            Dirty(structUid, structure);

            // Невидимые двери дают слишком грязный UX, поэтому маскируем все остальные культовые объекты.
            if (structure.StructureType != CultStructureType.RunedAirlock)
                SetCultConcealed(structUid, structure.Concealed);
        }

        _popup.PopupEntity(Loc.GetString("cult-conceal-toggled"), uid, uid);
    }

    // ─── Предметы-заклинания (hand items) ─────────────────────────────────────

    private void OnSpellItemAfterInteract(EntityUid uid, CultSpellItemComponent comp, AfterInteractEvent args)
    {
        // Защита: если объект уже удаляется — не обрабатываем повторно
        if (TerminatingOrDeleted(uid))
            return;

        if (!args.CanReach)
            return;

        var caster = args.User;
        if (!TryComp<CultistComponent>(caster, out var cultistComp))
            return;

        if (args.Target == null && comp.SpellType != "bloodrites")
            return;

        var target = args.Target ?? caster;
        if (caster == target && comp.SpellType != "bloodrites")
            return;

        args.Handled = true;

        var success = false;

        switch (comp.SpellType)
        {
            case "stun":
                success = CastStun(uid, caster, cultistComp, target);
                break;
            case "shackles":
                success = CastShackles(uid, caster, cultistComp, target);
                break;
            case "construction":
                success = CastConstruction(uid, caster, cultistComp, target);
                break;
            case "bloodrites":
                success = CastBloodRites(uid, caster, cultistComp, target);
                break;
        }

        if (!success)
            return;

        if (comp.SpellType == "bloodrites")
            return;

        RemComp<UnremoveableComponent>(uid);
        QueueDel(uid);
    }

    private void OnSpellItemUseInHand(EntityUid uid, CultSpellItemComponent comp, UseInHandEvent args)
    {
        var caster = args.User;
        if (!TryComp<CultistComponent>(caster, out var cultistComp))
            return;

        args.Handled = true;

        if (comp.SpellType == "teleport")
        {
            // Открываем BUI через предмет-заклинание
            var myMap = Transform(caster).MapID;
            var runes = new List<(NetEntity, string)>();

            var runeQuery = EntityQueryEnumerator<CultRuneComponent, TransformComponent>();
            while (runeQuery.MoveNext(out var runeUid, out var rune, out var runeXform))
            {
                if (rune.RuneType != CultRuneType.Teleport) continue;
                if (runeXform.MapID != myMap) continue;
                var tag = string.IsNullOrWhiteSpace(rune.TeleportTag)
                    ? Loc.GetString("cult-teleport-name-default")
                    : rune.TeleportTag;
                runes.Add((GetNetEntity(runeUid), tag));
            }

            if (runes.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("cult-teleport-no-rune"), caster, caster);
                return;
            }

            _ui.SetUiState(uid, CultTeleportBuiKey.Key, new CultTeleportBuiState(runes));
            _ui.TryOpenUi(uid, CultTeleportBuiKey.Key, caster);
            return;
        }

        if (comp.SpellType == "bloodrites")
        {
            _ui.SetUiState(uid, CultBloodRitesBuiKey.Key, new CultBloodRitesBuiState(comp.BloodRitesMode, cultistComp.BloodRitesCharges));
            _ui.TryOpenUi(uid, CultBloodRitesBuiKey.Key, caster);
            return;
        }

        // Для остальных предметов активация в руке отменяет заклинание.
        _popup.PopupEntity(Loc.GetString("cult-spell-cancelled"), args.User, args.User);
        RemComp<UnremoveableComponent>(uid);
        QueueDel(uid);
    }

    private void OnSpellItemTeleportSelect(EntityUid uid, CultSpellItemComponent comp, CultTeleportSelectMessage args)
    {
        if (comp.SpellType != "teleport")
            return;

        var cultist = args.Actor;
        if (!TryComp<CultistComponent>(cultist, out var cultistComp))
            return;

        var runeEntity = GetEntity(args.RuneEntity);

        if (!TryComp<CultRuneComponent>(runeEntity, out var runeComp) || runeComp.RuneType != CultRuneType.Teleport)
        {
            _popup.PopupEntity(Loc.GetString("cult-teleport-no-rune"), cultist, cultist);
            return;
        }

        PerformTeleport(cultist, runeEntity);
        TryConsumePreparedSpellUseFromItem(uid, cultist, cultistComp);
        RemComp<UnremoveableComponent>(uid);
        QueueDel(uid); // удаляем предмет-заклинание
    }

    private void OnBloodRitesChoice(EntityUid uid, CultSpellItemComponent comp, CultBloodRitesChoiceMessage args)
    {
        if (comp.SpellType != "bloodrites")
            return;

        comp.BloodRitesMode = args.Mode;
        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString("cult-blood-rites-mode-set", ("mode", Loc.GetString(GetBloodRitesModeLocKey(args.Mode)))), uid, uid, PopupType.Small);
    }

    private static string GetBloodRitesModeLocKey(CultBloodRitesMode mode) => mode switch
    {
        CultBloodRitesMode.Gather => "cult-blood-rites-mode-gather",
        CultBloodRitesMode.Heal => "cult-blood-rites-mode-heal",
        CultBloodRitesMode.Recharge => "cult-blood-rites-mode-recharge",
        CultBloodRitesMode.Orb => "cult-blood-rites-mode-orb",
        CultBloodRitesMode.Spear => "cult-blood-rites-mode-spear",
        _ => "cult-blood-rites-mode-gather",
    };

    private bool CastBloodRites(EntityUid spellItem, EntityUid caster, CultistComponent comp, EntityUid target)
    {
        var success = Comp<CultSpellItemComponent>(spellItem).BloodRitesMode switch
        {
            CultBloodRitesMode.Gather => ExecuteBloodRitesGather(caster, comp, target),
            CultBloodRitesMode.Heal => ExecuteBloodRitesHeal(caster, comp, target),
            CultBloodRitesMode.Recharge => ExecuteBloodRitesRecharge(caster, comp, target),
            CultBloodRitesMode.Orb => ExecuteBloodRitesOrb(caster, comp),
            CultBloodRitesMode.Spear => ExecuteBloodRitesSpear(caster, comp),
            _ => false,
        };

        if (!success)
            return false;

        return TryConsumePreparedSpellUseFromItem(spellItem, caster, comp);
    }

    private bool ExecuteBloodRitesGather(EntityUid caster, CultistComponent comp, EntityUid target)
    {
        var gained = 0;

        // Кража крови у живых не-культистов: 1 унция крови = 1 заряд.
        if (target != caster
            && !HasComp<CultistComponent>(target)
            && TryComp<BloodstreamComponent>(target, out var targetBlood)
            && !_mobState.IsDead(target))
        {
            if (_bloodstream.TryModifyBloodLevel((target, targetBlood), -FixedPoint2.New(50)))
                gained += 50;
        }

        // Сбор крови с пола в зоне 5x5 (квадрат): забираем всю кровь из каждой лужи.
        var nearby = new HashSet<EntityUid>();
        var center = Transform(caster).Coordinates;
        _lookup.GetEntitiesInRange(center, 2.6f, nearby);

        var casterPos = Transform(caster).WorldPosition;
        foreach (var ent in nearby)
        {
            if (!TryComp<PuddleComponent>(ent, out var puddle))
                continue;

            var pos = Transform(ent).WorldPosition;
            if (MathF.Abs(pos.X - casterPos.X) > 2.5f || MathF.Abs(pos.Y - casterPos.Y) > 2.5f)
                continue;

            if (!_solutionContainer.ResolveSolution(ent, puddle.SolutionName, ref puddle.Solution, out var puddleSolution))
                continue;

            var bloodInPuddle = puddleSolution.GetTotalPrototypeQuantity("Blood");
            if (bloodInPuddle <= FixedPoint2.Zero)
                continue;

            _solutionContainer.RemoveReagent(puddle.Solution!.Value, "Blood", bloodInPuddle);
            _solutionContainer.UpdateChemicals(puddle.Solution.Value);

            QueueDel(ent);

            gained += (int)MathF.Floor(bloodInPuddle.Float());
        }

        if (gained <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-blood-rites-gather-none"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        comp.BloodRitesCharges += gained;
        _popup.PopupEntity(Loc.GetString("cult-blood-rites-gathered", ("charges", gained), ("total", comp.BloodRitesCharges)), caster, caster, PopupType.Small);
        return true;
    }

    private bool ExecuteBloodRitesHeal(EntityUid caster, CultistComponent comp, EntityUid target)
    {
        if (!TryComp<CultistComponent>(target, out _))
        {
            _popup.PopupEntity(Loc.GetString("cult-blood-rites-heal-target-invalid"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (comp.BloodRitesCharges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-no-charges"), caster, caster);
            return false;
        }

        if (!TryComp<DamageableComponent>(target, out _))
            return false;

        var spent = 0;
        while (comp.BloodRitesCharges > 0)
        {
            var before = Comp<DamageableComponent>(target).Damage.GetTotal().Float();
            if (before <= 0f)
                break;

            var healing = new DamageSpecifier();
            healing.DamageDict.Add("Brute", -1f);
            healing.DamageDict.Add("Burn", -1f);
            healing.DamageDict.Add("Poison", -1f);
            healing.DamageDict.Add("Asphyxiation", -1f);

            _damage.TryChangeDamage(target, healing, true);
            var after = Comp<DamageableComponent>(target).Damage.GetTotal().Float();
            if (after >= before)
                break;

            comp.BloodRitesCharges--;
            spent++;
        }

        if (spent <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-blood-rites-heal-no-damage"), caster, caster, PopupType.Small);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("cult-blood-rites-healed", ("spent", spent), ("left", comp.BloodRitesCharges)), caster, caster, PopupType.Small);
        return true;
    }

    private bool ExecuteBloodRitesRecharge(EntityUid caster, CultistComponent comp, EntityUid target)
    {
        const int cost = 75;
        if (comp.BloodRitesCharges < cost)
        {
            _popup.PopupEntity(Loc.GetString("cult-no-charges"), caster, caster);
            return false;
        }

        var receiver = HasComp<CultistComponent>(target) ? target : caster;
        var changed = false;

        _cultShield.RestoreShield(receiver);

        // Заряжаем сдвигатель вуали в руках.
        foreach (var held in _hands.EnumerateHeld(receiver))
        {
            if (!TryComp<CultVeilShifterComponent>(held, out var shifter))
                continue;

            shifter.Charges = 4;
            _appearance.SetData(held, CultVeilShifterVisuals.Depleted, false);
            changed = true;
        }

        // Заряжаем щит на доспехе в слоте.
        if (_inventory.TryGetSlotEntity(receiver, "outerClothing", out var armor)
            && TryComp<CultArmorComponent>(armor, out var armorComp))
        {
            armorComp.StoredShieldCharges = 3;
            changed = true;
        }

        if (!changed)
        {
            _popup.PopupEntity(Loc.GetString("cult-blood-rites-recharge-nothing"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        comp.BloodRitesCharges -= cost;
        _popup.PopupEntity(Loc.GetString("cult-blood-rites-recharge-done", ("left", comp.BloodRitesCharges)), caster, caster, PopupType.Small);
        return true;
    }

    private bool ExecuteBloodRitesOrb(EntityUid caster, CultistComponent comp)
    {
        const int cost = 50;
        if (comp.BloodRitesCharges < cost)
        {
            _popup.PopupEntity(Loc.GetString("cult-no-charges"), caster, caster);
            return false;
        }

        comp.BloodRitesCharges -= cost;
        var orb = Spawn("CultBloodOrb", Transform(caster).Coordinates);
        EnsureComp<CultBloodOrbComponent>(orb).Charges = cost;
        _hands.TryPickupAnyHand(caster, orb);

        _popup.PopupEntity(Loc.GetString("cult-blood-rites-orb-created", ("left", comp.BloodRitesCharges)), caster, caster, PopupType.Small);
        return true;
    }

    private bool ExecuteBloodRitesSpear(EntityUid caster, CultistComponent comp)
    {
        const int cost = 150;
        if (comp.BloodRitesCharges < cost)
        {
            _popup.PopupEntity(Loc.GetString("cult-no-charges"), caster, caster);
            return false;
        }

        comp.BloodRitesCharges -= cost;
        var spear = Spawn("CultHalberd", Transform(caster).Coordinates);
        var spearComp = EnsureComp<CultBloodSpearComponent>(spear);
        spearComp.OwnerUid = caster;

        RemoveRecallBloodSpearAction(caster);
        spearComp.RecallAction = _actions.AddAction(caster, ActionCultRecallBloodSpear);

        _hands.TryPickupAnyHand(caster, spear);

        _popup.PopupEntity(Loc.GetString("cult-blood-rites-spear-created", ("left", comp.BloodRitesCharges)), caster, caster, PopupType.Medium);
        return true;
    }

    // Выполняет телепортацию культиста к руне, включая групповой телепорт
    private void PerformTeleport(EntityUid cultist, EntityUid runeEntity)
    {
        if (!TryComp<CultistComponent>(cultist, out var cultistComp))
            return;

        _chat.TrySendInGameICMessage(cultist, Loc.GetString("cult-incantation-teleport"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        DealSelfDamage(cultist, 7f);

        var sourcePos = Transform(cultist).WorldPosition;
        var destPos = Transform(runeEntity).WorldPosition;

        Spawn("CultEffectTeleportOut", Transform(cultist).Coordinates);
        _xform.SetWorldPosition(cultist, destPos);
        Spawn("CultEffectTeleportIn", Transform(runeEntity).Coordinates);
        _audio.PlayPvs("/Audio/Effects/teleport_arrival.ogg", cultist);

        // Групповой телепорт: культисты в радиусе 2 тайлов телепортируются тоже
        var nearbyQuery = EntityQueryEnumerator<CultistComponent, TransformComponent>();
        while (nearbyQuery.MoveNext(out var cUid, out _, out var cXform))
        {
            if (cUid == cultist) continue;
            if ((cXform.WorldPosition - sourcePos).Length() > 2f) continue;
            _xform.SetWorldPosition(cUid, destPos);
        }
    }

    // Снимает заклинание-предмет из рук кастера, возвращает true если нашёл и удалил
    private bool TryFindAndDeleteSpellItem(EntityUid uid, string spellType)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (TryComp<CultSpellItemComponent>(held, out var spellComp) && spellComp.SpellType == spellType)
            {
                RemComp<UnremoveableComponent>(held);
                QueueDel(held);
                return true;
            }
        }
        return false;
    }

    private void OnBloodSpearShutdown(EntityUid uid, CultBloodSpearComponent comp, ComponentShutdown args)
    {
        if (comp.OwnerUid != EntityUid.Invalid)
            RemoveRecallBloodSpearAction(comp.OwnerUid, comp.RecallAction);
    }

    private void RemoveRecallBloodSpearAction(EntityUid owner, EntityUid? actionUid = null)
    {
        if (actionUid != null && !TerminatingOrDeleted(actionUid.Value))
        {
            _actions.RemoveAction(owner, actionUid.Value);
            return;
        }

        foreach (var action in _actions.GetActions(owner))
        {
            var actionUidValue = action.Owner;
            if (TerminatingOrDeleted(actionUidValue))
                continue;

            var protoId = MetaData(actionUidValue).EntityPrototype?.ID;
            if (protoId == null || (EntProtoId)protoId != ActionCultRecallBloodSpear)
                continue;

            _actions.RemoveAction(owner, actionUidValue);
        }
    }

    // ─── Cast методы ───────────────────────────────────────────────────────────

    private bool CastStun(EntityUid spellItem, EntityUid caster, CultistComponent comp, EntityUid target)
    {
        if (HasComp<CultistComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-target-is-cultist"), caster, caster);
            return false;
        }

        _chat.TrySendInGameICMessage(caster, Loc.GetString("cult-incantation-stun"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        DealSelfDamage(caster, 10f);

        // Нокдаун 3 сек + тишина (стан) 6 сек
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(3), true);
        _stun.TryAddStunDuration(target, TimeSpan.FromSeconds(6));

        // Культовая речь на 20 сек: жертва выкрикивает фразы в общее радио
        var speechComp = EnsureComp<CultSpeechAffectedComponent>(target);
        speechComp.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(20);
        speechComp.NextPhrase = _timing.CurTime + TimeSpan.FromSeconds(6.5);

        _audio.PlayPvs("/Audio/Magic/magic_wand.ogg", caster);
        _popup.PopupEntity(Loc.GetString("cult-stun-hit", ("target", MetaData(target).EntityName)), caster, caster);
        return TryConsumePreparedSpellUseFromItem(spellItem, caster, comp);
    }

    private bool CastShackles(EntityUid spellItem, EntityUid caster, CultistComponent comp, EntityUid target)
    {
        if (HasComp<CultistComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-target-is-cultist"), caster, caster);
            return false;
        }

        // Требуется: цель оглушена, лежит, недееспособна или в стамина-крите
        var isStunned = _statusEffects.HasStatusEffect(target, "Stun")
                     || _statusEffects.HasStatusEffect(target, "KnockedDown")
                     || _mobState.IsIncapacitated(target)
                     || (TryComp<StaminaComponent>(target, out var staminaComp) && staminaComp.Critical);
        if (!isStunned)
        {
            _popup.PopupEntity(Loc.GetString("cult-shackles-needs-stunned"), caster, caster);
            return false;
        }

        _chat.TrySendInGameICMessage(caster, Loc.GetString("cult-incantation-shackles"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        var shackles = Spawn("ShadowShackles", Transform(target).Coordinates);
        _cuffs.TryAddNewCuffs(target, caster, shackles);
        // Немота 12 сек
        if (TryComp<StatusEffectsComponent>(target, out var targetStatusEffects))
        {
            _statusEffects.TryAddStatusEffect<MutedComponent>(
                target,
                "Muted",
                TimeSpan.FromSeconds(12),
                true,
                targetStatusEffects);
        }

        _audio.PlayPvs("/Audio/Effects/shadowshackles.ogg", caster, AudioParams.Default.WithVolume(-4));
        _popup.PopupEntity(Loc.GetString("cult-shackles-applied", ("target", MetaData(target).EntityName)), caster, caster);
        return TryConsumePreparedSpellUseFromItem(spellItem, caster, comp);
    }

    private bool CastConstruction(EntityUid spellItem, EntityUid caster, CultistComponent comp, EntityUid target)
    {
        _chat.TrySendInGameICMessage(caster, Loc.GetString("cult-incantation-twisted"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        DealSelfDamage(caster, 12f);

        var meta = MetaData(target);
        var protoId = meta.EntityPrototype?.ID;

        if (protoId == "IngotSteel" || protoId == "SheetSteel"
            || protoId == "SheetPlasteel" || protoId == "SheetPlasteel1" || protoId == "SheetPlasteel10"
            || protoId == "SheetPlastic"
            || protoId == "IngotIron")
        {
            // Конвертируем весь стак в рунный металл одной стакованной сущностью
            var coords = Transform(target).Coordinates;
            int count = 1;
            if (TryComp<StackComponent>(target, out var stackComp))
                count = stackComp.Count;
            QueueDel(target);
            var metal = Spawn(RunedMetalProto, coords);
            if (count > 1)
                _stack.SetCount(metal, count);
            _popup.PopupEntity(Loc.GetString("cult-twisted-construction-metal"), caster, caster);
            return TryConsumePreparedSpellUseFromItem(spellItem, caster, comp);
        }
        else if (TryComp<AirlockComponent>(target, out _))
        {
            // Преобразуем шлюз в рунный культовый шлюз
            var coords = Transform(target).Coordinates;
            var originalProto = MetaData(target).EntityPrototype?.ID;
            QueueDel(target);
            var cultAirlock = Spawn("CultRunedAirlock", coords);
            if (TryComp<CultStructureComponent>(cultAirlock, out var cultStructComp))
            {
                cultStructComp.OriginalProto = originalProto;
                Dirty(cultAirlock, cultStructComp);
            }
            _popup.PopupEntity(Loc.GetString("cult-twisted-construction-airlock"), caster, caster);
            return TryConsumePreparedSpellUseFromItem(spellItem, caster, comp);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("cult-twisted-construction-fail"), caster, caster);
            return false;
        }
    }

    // ─── Повязка фанатика ──────────────────────────────────────────────────────

    private void OnFanaticBandageEquipped(EntityUid uid, CultFanaticBandageComponent comp, GotEquippedEvent args)
    {
        if (args.Slot != "eyes")
            return;

        EnsureComp<ShowHealthBarsComponent>(args.Equipee);
        EnsureComp<CanSeeConcealedComponent>(args.Equipee);

        if (TryComp<EyeComponent>(args.Equipee, out var eye))
            _eye.SetDrawLight((args.Equipee, eye), false);

        _eye.RefreshVisibilityMask(args.Equipee);
    }

    private void OnFanaticBandageUnequipped(EntityUid uid, CultFanaticBandageComponent comp, GotUnequippedEvent args)
    {
        if (args.Slot != "eyes")
            return;

        RemComp<ShowHealthBarsComponent>(args.Equipee);
        RemComp<CanSeeConcealedComponent>(args.Equipee);

        if (TryComp<EyeComponent>(args.Equipee, out var eye))
            _eye.SetDrawLight((args.Equipee, eye), true);

        _eye.RefreshVisibilityMask(args.Equipee);
    }

    private void OnCanSeeConcealedGetVis(Entity<CanSeeConcealedComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Admin;
    }

    private void SetCultConcealed(EntityUid uid, bool concealed)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);
        _visibility.SetLayer(uid, concealed ? (ushort) VisibilityFlags.Admin : (ushort) VisibilityFlags.Normal);
        _visibility.RefreshVisibility(uid, visibilityComponent: visibility);

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetCanCollide(uid, !concealed, body: physics);
    }

    private void OnBloodRites(EntityUid uid, CultistComponent comp, CultBloodRitesActionEvent args)
    {
        args.Handled = true;

        // Повторное нажатие убирает тёмную энергию из руки.
        if (TryFindAndDeleteSpellItem(uid, "bloodrites"))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-cancelled"), uid, uid);
            return;
        }

        if (!comp.PreparedSpells.Contains("ActionCultBloodRites"))
        {
            _popup.PopupEntity(Loc.GetString("cult-spell-no-uses"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var item = Spawn("CultSpellItemBloodRites", Transform(uid).Coordinates);
        Comp<CultSpellItemComponent>(item).PreparedActionId = "ActionCultBloodRites";
        _hands.TryPickupAnyHand(uid, item);

        _chat.TrySendInGameICMessage(uid, Loc.GetString("cult-incantation-blood-rites"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
    }

    private void OnRecallBloodSpear(EntityUid uid, CultistComponent comp, CultRecallBloodSpearActionEvent args)
    {
        args.Handled = true;

        var query = EntityQueryEnumerator<CultBloodSpearComponent>();
        while (query.MoveNext(out var spearUid, out var spearComp))
        {
            if (spearComp.OwnerUid != uid)
                continue;

            if (TerminatingOrDeleted(spearUid))
                continue;

            _xform.SetCoordinates(spearUid, Transform(uid).Coordinates);
            _hands.TryPickupAnyHand(uid, spearUid);
            _audio.PlayPvs("/Audio/Magic/teleport_arrival.ogg", uid);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cult-blood-spear-not-found"), uid, uid, PopupType.SmallCaution);
    }

    //  Structure Interaction (Altar / Forge / Archives)

    private static readonly Dictionary<CultStructureType, (string ItemId, string LocKey)[]> StructureItems = new()
    {
        [CultStructureType.Altar] =
        [
            ("CultWhetstone",   "cult-structure-item-whetstone"),
            ("CultJuggernautShell",  "cult-structure-item-juggernaut-shell"),
            ("CultWraithShell",     "cult-structure-item-wraith-shell"),
            ("CultArtificerShell",  "cult-structure-item-artificer-shell"),
            ("CultUnholyFlask", "cult-structure-item-flask"),
        ],
        [CultStructureType.Forge] =
        [
            ("CultHeavyRobe",                    "cult-structure-item-heavy-robe"),
            ("ClothingOuterCultFlagellantRobes", "cult-structure-item-flagellant-robes"),
            ("CultMirrorShield",                 "cult-structure-item-mirror-shield"),
        ],
        [CultStructureType.Archives] =
        [
            ("CultFanaticBandage", "cult-structure-item-fanatic-bandage"),
            ("CultVeilShifter",     "cult-structure-item-veil-shifter"),
            ("CultCursedOrb",       "cult-structure-item-cursed-orb"),
        ],
    };

    private void OnStructureActivate(EntityUid uid, CultStructureComponent comp, ActivateInWorldEvent args)
    {
        if (!HasComp<CultistComponent>(args.User))
            return;
        if (!StructureItems.ContainsKey(comp.StructureType))
            return;

        args.Handled = true;

        var now = _timing.CurTime;
        if (comp.NextUse.HasValue && now < comp.NextUse.Value)
        {
            var remaining = (int)(comp.NextUse.Value - now).TotalSeconds;
            _popup.PopupEntity(
                Loc.GetString("cult-structure-cooldown", ("seconds", remaining)),
                uid, args.User);
            return;
        }

        var items = StructureItems[comp.StructureType]
            .Select(i => (i.ItemId, i.LocKey))
            .ToList();
        _ui.SetUiState(uid, CultStructureBuiKey.Key, new CultStructureBuiState(items));
        _ui.TryOpenUi(uid, CultStructureBuiKey.Key, args.User);
    }

    private void OnStructureCreate(EntityUid uid, CultStructureComponent comp, CultStructureCreateMessage args)
    {
        if (!HasComp<CultistComponent>(args.Actor))
            return;
        if (!StructureItems.TryGetValue(comp.StructureType, out var items))
            return;

        if (!items.Any(i => i.ItemId == args.ItemId))
            return;

        var now = _timing.CurTime;
        if (comp.NextUse.HasValue && now < comp.NextUse.Value)
            return;

        comp.NextUse = now + comp.Cooldown;

        var item = Spawn(args.ItemId, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(args.Actor, item);
        _audio.PlayPvs(CultMagicSound, uid, AudioParams.Default.WithVolume(-4));
        _popup.PopupEntity(Loc.GetString("cult-structure-item-created"), args.Actor, args.Actor);
    }


    //  Runed Metal Construction

    private void OnRunedMetalUseInHand(EntityUid uid, CultRunedMetalComponent comp, UseInHandEvent args)
    {
        if (!HasComp<CultistComponent>(args.User))
            return;

        args.Handled = true;
        _ui.TryOpenUi(uid, CultConstructionBuiKey.Key, args.User);
    }

    //  Rune Drawing 

    private static readonly Dictionary<string, (CultRuneType type, string name)> RuneChoices = new()
    {
        ["teleport"]  = (CultRuneType.Teleport,  "cult-rune-teleport"),
        ["empower"]   = (CultRuneType.Empowering,"cult-rune-empower"),
        ["offering"]  = (CultRuneType.Offering,  "cult-rune-offering"),
        ["revive"]    = (CultRuneType.Revive,     "cult-rune-revive"),
        ["barrier"]   = (CultRuneType.Barrier,    "cult-rune-barrier"),
        ["summoning"] = (CultRuneType.Summoning,  "cult-rune-summoning"),
        ["bloodboil"]   = (CultRuneType.BloodBoil,   "cult-rune-bloodboil"),
        ["spiritRealm"] = (CultRuneType.SpiritRealm, "cult-rune-spirit-realm"),
        ["narsie"]      = (CultRuneType.NarSie,       "cult-rune-narsie"),
    };

    private void OnDaggerUseInHand(EntityUid uid, CultDaggerComponent comp, UseInHandEvent args)
    {
        if (!HasComp<CultistComponent>(args.User))
            return;

        args.Handled = true;
        _ui.TryOpenUi(uid, CultRuneDrawBuiKey.Key, args.User);
    }

    private void OnRuneSelected(EntityUid uid, CultDaggerComponent comp, CultSelectRuneMessage args)
    {
        var cultist = args.Actor;

        if (!HasComp<CultistComponent>(cultist))
            return;

        var runeId = args.RuneId;
        if (!RuneChoices.TryGetValue(runeId, out var info))
            return;

        StartRuneDrawing(cultist, uid, runeId, info.type, args.Label);
    }

    private void StartRuneDrawing(EntityUid cultist, EntityUid dagger, string runeKey, CultRuneType runeType, string? label = null)
    {
        if (!TryComp<CultDaggerComponent>(dagger, out var dagComp))
            return;

        var drawTime = dagComp.DrawTime;
        if (runeType == CultRuneType.NarSie)
        {
            if (!CanStartNarSieRitual(cultist, out var failureReason))
            {
                _popup.PopupEntity(failureReason, cultist, cultist, PopupType.LargeCaution);
                return;
            }

            drawTime = NarSieDrawTime;
            BeginNarSieRitual(cultist);
        }

        _chat.TrySendInGameICMessage(cultist, Loc.GetString($"cult-rune-incantation-{runeKey}"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, cultist, drawTime,
            new DrawRuneDoAfterEvent { StringData = runeKey, RuneLabel = label },
            eventTarget: cultist,
            used: dagger)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });

        _popup.PopupEntity(
            Loc.GetString("cult-drawing-rune", ("rune", Loc.GetString($"cult-rune-{runeKey}"))),
            cultist, cultist);
    }

    private void OnDrawRuneDoAfter(EntityUid uid, CultistComponent comp, DrawRuneDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            if (args.StringData == "narsie")
                EndNarSieRitual(uid);
            return;
        }
        args.Handled = true;

        var runeKey = args.StringData ?? "teleport";
        if (!RuneChoices.TryGetValue(runeKey, out var info))
            return;

        if (TryComp<CultDaggerComponent>(args.Used, out var dagComp))
            DealSelfDamage(uid, dagComp.SelfDamage);

        var protoId = GetRunePrototype(info.type);
        var rune = Spawn(protoId, Transform(uid).Coordinates);

        if (info.type == CultRuneType.NarSie)
            EndNarSieRitual(uid);

        if (info.type == CultRuneType.Teleport && TryComp<CultRuneComponent>(rune, out var runeComp))
            runeComp.TeleportTag = !string.IsNullOrWhiteSpace(args.RuneLabel)
                ? args.RuneLabel
                : Loc.GetString("cult-teleport-name-default");

        _audio.PlayPvs("/Audio/Effects/desecration-01.ogg", uid, AudioParams.Default.WithVolume(-4));
        _popup.PopupEntity(
            Loc.GetString("cult-rune-drawn", ("rune", Loc.GetString($"cult-rune-{runeKey}"))),
            uid, uid);
    }

    private static string GetRunePrototype(CultRuneType type) => type switch
    {
        CultRuneType.Teleport   => "CultRuneTeleport",
        CultRuneType.Empowering => "CultRuneEmpower",
        CultRuneType.Offering   => "CultRuneOffering",
        CultRuneType.Revive     => "CultRuneRevive",
        CultRuneType.Barrier    => "CultRuneBarrier",
        CultRuneType.Summoning  => "CultRuneSummoning",
        CultRuneType.BloodBoil   => "CultRuneBloodBoil",
        CultRuneType.SpiritRealm => "CultRuneSpiritRealm",
        CultRuneType.NarSie      => "CultRuneNarSie",
        _                       => "CultRuneTeleport",
    };

    private bool CanStartNarSieRitual(EntityUid cultist, out string failureReason)
    {
        failureReason = string.Empty;

        if (!_cultRule.AreRequiredSacrificesComplete(out _, out _))
        {
            failureReason = Loc.GetString("cult-commune-sacrifices-required-first");
            return false;
        }

        var station = _station.GetOwningStation(cultist);
        var mapCoords = _xform.GetMapCoordinates(cultist);
        if (_cultRule.IsNearNarSieBeacon(mapCoords, station, NarSieBeaconRange))
            return true;

        failureReason = Loc.GetString("cult-narsie-no-beacon",
            ("beacons", _cultRule.GetNarSieBeaconSummary(station)));
        return false;
    }

    private void BeginNarSieRitual(EntityUid cultist)
    {
        EndNarSieRitual(cultist);

        var barriers = new List<EntityUid>();
        var mapCoords = _xform.GetMapCoordinates(cultist);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var barrierCoords = new MapCoordinates(mapCoords.Position + new Vector2(x, y), mapCoords.MapId);
                var barrier = Spawn("CultNarSieRitualBarrier", barrierCoords);
                barriers.Add(barrier);
            }
        }

        _activeNarSieBarriers[cultist] = barriers;

        if (TryComp<CultistComponent>(cultist, out var cultistComp))
        {
            var ritualAudio = _audio.PlayGlobal(
                NarSieRitualMusic,
                Filter.Broadcast(),
                true,
                AudioParams.Default.WithLoop(true).WithVolume(-4f));
            cultistComp.ActiveNarSieRitualAudio = ritualAudio?.Entity;
        }

        if (_station.GetOwningStation(cultist) is { } station)
        {
            var ritualCoords = _xform.GetMapCoordinates(cultist);
            var location = _cultRule.TryGetNearestNarSieBeaconLabel(ritualCoords, station, out var label)
                ? label
                : _navMap.GetNearestBeaconString(ritualCoords, true);

            _chat.DispatchStationAnnouncement(
                station,
                Loc.GetString("cult-narsie-centcomm-warning", ("location", location)),
                Loc.GetString("comms-console-announcement-title-centcom"),
                colorOverride: Color.Red);

            _alertLevel.SetLevel(station, "delta", true, true, true, true);
        }
    }

    private void EndNarSieRitual(EntityUid cultist)
    {
        if (TryComp<CultistComponent>(cultist, out var cultistComp)
            && cultistComp.ActiveNarSieRitualAudio.HasValue
            && Exists(cultistComp.ActiveNarSieRitualAudio.Value))
        {
            _audio.SetState(cultistComp.ActiveNarSieRitualAudio.Value, AudioState.Stopped);
            QueueDel(cultistComp.ActiveNarSieRitualAudio.Value);
            cultistComp.ActiveNarSieRitualAudio = null;
        }

        if (!_activeNarSieBarriers.Remove(cultist, out var barriers))
            return;

        foreach (var barrier in barriers)
        {
            if (Exists(barrier))
                QueueDel(barrier);
        }
    }

    //  Helpers 

    private static bool IsValidSpellId(string spellId)
    {
        foreach (var spell in CultSpells)
        {
            if ((string)spell == spellId)
                return true;
        }
        return false;
    }

    private static string GetSpellLocKey(string spellId) => spellId switch
    {
        "ActionCultStun"               => "cult-spell-stun",
        "ActionCultShackles"           => "cult-spell-shackles",
        "ActionCultTeleport"           => "cult-spell-teleport",
        "ActionCultEmp"                => "cult-spell-emp",
        "ActionCultTwistedConstruction"=> "cult-spell-twisted-construction",
        "ActionCultSummonDagger"       => "cult-spell-summon-dagger",
        "ActionCultSummonEquipment"    => "cult-spell-summon-equipment",
        "ActionCultConcealPresence"    => "cult-spell-conceal-presence",
        "ActionCultBloodRites"         => "cult-spell-blood-rites",
        _                              => "cult-spell-unknown",
    };

    public void DealSelfDamage(EntityUid uid, float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict.Add("Brute", amount);
        _damage.TryChangeDamage(uid, spec, true);
    }

    // Отправляет сообщение общины всем культистам
    private void BroadcastCommune(string message)
    {
        var chatMsg = new ChatMessage(
            ChatChannel.Radio,
            message,
            message,
            NetEntity.Invalid,
            null);
        var netMsg = new MsgChatMessage { Message = chatMsg };
        var recipients = new HashSet<ICommonSession>();

        var query = EntityQueryEnumerator<CultistComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (TryComp<ActorComponent>(uid, out var actor))
                recipients.Add(actor.PlayerSession);
        }

        var constructQuery = EntityQueryEnumerator<CultConstructComponent>();
        while (constructQuery.MoveNext(out var uid, out _))
        {
            if (TryComp<ActorComponent>(uid, out var actor))
                recipients.Add(actor.PlayerSession);
        }

        foreach (var session in recipients)
        {
            _netMan.ServerSendMessage(netMsg, session.Channel);
        }
    }

    // Проверка порогов роста культа (красные глаза при 10%, ореол при 20%)
    private const float RedEyesThresholdRatio = 0.10f;
    private const float BloodHaloThresholdRatio = 0.20f;

    private void CheckCultGrowth()
    {
        var cultists = new List<(EntityUid uid, CultistComponent comp)>();
        var cultQuery = EntityQueryEnumerator<CultistComponent>();
        while (cultQuery.MoveNext(out var uid, out var comp))
            cultists.Add((uid, comp));

        if (cultists.Count == 0)
            return;

        var constructCount = 0;
        var constructQuery = EntityQueryEnumerator<CultConstructComponent>();
        while (constructQuery.MoveNext(out var constructUid, out _))
        {
            if (_mobState.IsDead(constructUid))
                continue;

            constructCount++;
        }

        var totalCultForces = cultists.Count + constructCount;

        var activePlayers = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.InGame)
                activePlayers++;
        }

        activePlayers = Math.Max(activePlayers, 1);
        var redEyesThreshold = Math.Max(1, (int) Math.Ceiling(activePlayers * RedEyesThresholdRatio));
        var bloodHaloThreshold = Math.Max(1, (int) Math.Ceiling(activePlayers * BloodHaloThresholdRatio));

        if (totalCultForces >= bloodHaloThreshold)
        {
            bool anyNew = false;
            foreach (var (uid, comp) in cultists)
            {
                if (!comp.RedEyes)
                {
                    comp.RedEyes = true;
                    if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
                    {
                        comp.OriginalEyeColor ??= humanoid.EyeColor;
                        humanoid.EyeColor = Color.Red;
                        Dirty(uid, humanoid);
                    }
                }

                if (!comp.BloodHalo)
                {
                    comp.BloodHalo = true;
                    Dirty(uid, comp);

                    // Ореол — спавним визуальную сущность
                    var halo = Spawn("CultHaloEffect", Transform(uid).Coordinates);
                    _xform.SetParent(halo, uid);
                    anyNew = true;
                }
            }
            if (anyNew && _cultRule.TryAnnounceVeilBroken())
            {
                BroadcastCommune(Loc.GetString("cult-veil-broken-halo"));
            }
        }
        else if (totalCultForces >= redEyesThreshold)
        {
            bool anyNew = false;
            foreach (var (uid, comp) in cultists)
            {
                if (!comp.RedEyes)
                {
                    comp.RedEyes = true;
                    Dirty(uid, comp);

                    // Меняем цвет глаз на красный
                    if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
                    {
                        comp.OriginalEyeColor = humanoid.EyeColor;
                        humanoid.EyeColor = Color.Red;
                        Dirty(uid, humanoid);
                    }

                    anyNew = true;
                }
            }
            if (anyNew && _cultRule.TryAnnounceVeilWeakens())
            {
                BroadcastCommune(Loc.GetString("cult-veil-weakens-eyes"));
            }
        }
    }

    private void OnCultistExamined(EntityUid uid, CultistComponent comp, ExaminedEvent args)
    {
        if (comp.RedEyes && AreCultistEyesVisible(uid))
            args.PushMarkup(Loc.GetString("cult-examine-red-eyes"));
    }

    private bool AreCultistEyesVisible(EntityUid uid)
    {
        if (_inventory.TryGetSlotEntity(uid, "eyes", out _))
            return false;

        if (_inventory.TryGetSlotEntity(uid, "mask", out _))
            return false;

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return true;

        return !humanoid.HiddenLayers.ContainsKey(HumanoidVisualLayers.Eyes)
            && !humanoid.HiddenLayers.ContainsKey(HumanoidVisualLayers.Head);
    }

    private void OnBuiHolderRangeCheck(EntityUid uid, CultBuiHolderComponent comp, ref BoundUserInterfaceCheckRangeEvent args)
    {
        // Держатель BUI культиста всегда доступен — проверка расстояния не нужна
        args.Result = BoundUserInterfaceRangeResult.Pass;
    }
}

