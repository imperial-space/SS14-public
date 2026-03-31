using Content.Server.Actions;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Cult.Components;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Imperial.Cult;
using Content.Server.Body.Systems;
using Content.Server.Damage.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.Stealth;
using Content.Shared.Actions;
using Content.Shared.Administration.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Обработка взаимодействия с рунами культа (активация, эффекты, деактивация).
/// </summary>
public sealed class CultRuneSystem : EntitySystem
{
    private const string CultFaction = "Cult";
    private const string CultMagicSound = "/Audio/Effects/desecration-01.ogg";
    private static readonly EntProtoId NarSieSpawnProto = "MobNarsieSpawn";
    private static readonly EntProtoId ActionDarkSpiritReturn = "ActionCultDarkSpiritReturn";
    private static readonly EntProtoId ActionDarkSpiritCommune = "ActionCultDarkSpiritCommune";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly CultRuleSystem _cultRule = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly StealthSystem _stealth = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private bool IsCultAligned(EntityUid uid)
    {
        return HasComp<CultistComponent>(uid) || HasComp<CultConstructComponent>(uid);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultRuneComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CultRuneComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CultRuneComponent, CultSpiritRealmChoiceMessage>(OnSpiritRealmChoice);
        SubscribeLocalEvent<CultDarkSpiritComponent, CultDarkSpiritReturnActionEvent>(OnDarkSpiritReturn);
        SubscribeLocalEvent<CultDarkSpiritComponent, CultDarkSpiritCommuneActionEvent>(OnDarkSpiritCommune);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CultSpiritTetherComponent, TransformComponent>();
        while (query.MoveNext(out var cultistUid, out var tether, out var xform))
        {
            // Чистим список от удалённых слуг.
            tether.Homunculi.RemoveAll(h => !EntityManager.EntityExists(h) || TerminatingOrDeleted(h));

            // Если слуг не осталось, привязка больше не нужна.
            if (tether.Homunculi.Count == 0)
            {
                RemCompDeferred<CultSpiritTetherComponent>(cultistUid);
                continue;
            }

            // Разрыв связи: руна удалена или культист отошёл слишком далеко.
            if (!EntityManager.EntityExists(tether.RuneUid) || TerminatingOrDeleted(tether.RuneUid))
            {
                BreakSpiritTether(cultistUid, tether);
                continue;
            }

            var runeCoords = _xform.GetMapCoordinates(tether.RuneUid);
            var cultCoords = _xform.GetMapCoordinates(cultistUid, xform);
            if (runeCoords.MapId != cultCoords.MapId ||
                (runeCoords.Position - cultCoords.Position).Length() > CultSpiritTetherComponent.TetherRange)
            {
                BreakSpiritTether(cultistUid, tether);
                continue;
            }

            // Тик урона от каждого активного слуги.
            tether.DrainAccumulator += frameTime;
            while (tether.DrainAccumulator >= CultSpiritTetherComponent.DrainInterval)
            {
                tether.DrainAccumulator -= CultSpiritTetherComponent.DrainInterval;
                _cult.DealSelfDamage(cultistUid, tether.Homunculi.Count * CultSpiritTetherComponent.BrutePerHomunculus);
            }
        }
    }

    // ──────────────────────── Rune Interaction ──────────────────────────

    private void OnInteractHand(EntityUid uid, CultRuneComponent rune, InteractHandEvent args)
    {
        if (!IsCultAligned(args.User))
        {
            // Не культист касается руны — не даём использовать
            return;
        }

        args.Handled = true;
        TryActivateRune(uid, rune, args.User);
    }

    private void OnInteractUsing(EntityUid uid, CultRuneComponent rune, InteractUsingEvent args)
    {
        // Ритуальный кинжал удаляет руну
        if (HasComp<CultDaggerComponent>(args.Used) && HasComp<CultistComponent>(args.User))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cult-rune-erased"), args.User, args.User);
            DestroyRune(uid, rune);
        }
    }

    // ──────────────────────── Rune Activation ──────────────────────────

    private void TryActivateRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Считаем сколько культистов стоит на месте руны
        var pos = _xform.GetMapCoordinates(uid);
        int invokers = CountInvokersNearby(pos, invoker);
        rune.InvokersOnRune = invokers;

        if (invokers < rune.RequiredInvokers)
        {
            _popup.PopupEntity(
                Loc.GetString("cult-rune-need-invokers",
                    ("have", invokers),
                    ("need", rune.RequiredInvokers)),
                invoker, invoker, PopupType.Medium);
            return;
        }

        switch (rune.RuneType)
        {
            case CultRuneType.Teleport:
                ActivateTeleportRune(uid, rune, invoker);
                break;
            case CultRuneType.Empowering:
                ActivateEmpowerRune(uid, rune, invoker);
                break;
            case CultRuneType.Offering:
                ActivateOfferingRune(uid, rune, invoker);
                break;
            case CultRuneType.Revive:
                ActivateReviveRune(uid, rune, invoker);
                break;
            case CultRuneType.Barrier:
                ActivateBarrierRune(uid, rune, invoker);
                break;
            case CultRuneType.Summoning:
                ActivateSummoningRune(uid, rune, invoker);
                break;
            case CultRuneType.BloodBoil:
                ActivateBloodBoilRune(uid, rune, invoker);
                break;
            case CultRuneType.SpiritRealm:
                ActivateSpiritRealmRune(uid, rune, invoker);
                break;
            case CultRuneType.NarSie:
                ActivateNarSieRune(uid, rune, invoker);
                break;
        }
    }

    // ──────────────────── Individual Rune Effects ────────────────────

    private void ActivateTeleportRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Руна телепортации сама по себе — просто пункт назначения
        // Активация — телепортируем всё, что стоит на ней, к другой руне с тегом
        _popup.PopupEntity(Loc.GetString("cult-teleport-rune-active"), invoker, invoker);
    }

    private void ActivateEmpowerRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Стоя на руне усиления — снижаем стоимость заклинаний
        if (TryComp<CultistComponent>(invoker, out var cultist))
        {
            cultist.OnEmpowerRune = true;
            _popup.PopupEntity(Loc.GetString("cult-empower-rune-active"), invoker, invoker);
        }
    }

    private void ActivateOfferingRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Ищем кого-то на руне (кого тащат или кто стоит рядом)
        var pos = _xform.GetMapCoordinates(uid);
        EntityUid? victim = FindAdjacentNonCultist(pos, invoker);

        if (victim == null)
        {
            _popup.PopupEntity(Loc.GetString("cult-offering-no-victim"), invoker, invoker);
            return;
        }

        var victimUid = victim.Value;

        // Отмеченные цели, мёртвые и носители ментального щита всегда идут в жертву.
        bool shouldSacrifice = _cultRule.IsSacrificeTarget(victimUid)
                       || _mobState.IsDead(victimUid)
                       || HasComp<MindShieldComponent>(victimUid);

        if (shouldSacrifice)
        {
            PerformSacrifice(uid, rune, victimUid, invoker);
        }
        else
        {
            AttemptConversion(uid, rune, victimUid, invoker);
        }
    }

    private void AttemptConversion(EntityUid runeUid, CultRuneComponent rune, EntityUid victim, EntityUid invoker)
    {
        int invokers = rune.InvokersOnRune;
        if (invokers < 2)
        {
            _popup.PopupEntity(Loc.GetString("cult-offering-need-two", ("have", invokers)), invoker, invoker);
            return;
        }

        if (_cult.TryConvertToCultist(victim))
        {
            // Лечим конвертированного на 90% bruте/burn
            var healSpec = new DamageSpecifier();
            healSpec.DamageDict.Add("Brute", -200);
            healSpec.DamageDict.Add("Burn", -200);
            _damage.TryChangeDamage(victim, healSpec, true);

            _audio.PlayPvs(CultMagicSound, runeUid);
            _popup.PopupEntity(Loc.GetString("cult-converted", ("name", MetaData(victim).EntityName)), invoker, invoker);

            // Оповещаем всех культистов
            BroadcastCultMessage(Loc.GetString("cult-commune-converted", ("name", MetaData(victim).EntityName)));
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("cult-convert-failed"), invoker, invoker);
        }
    }

    private void PerformSacrifice(EntityUid runeUid, CultRuneComponent rune, EntityUid victim, EntityUid invoker)
    {
        _mind.TryGetMind(victim, out var victimMindId, out _);

        // Создаём камень души до уничтожения жертвы
        var soulStone = Spawn("CultSoulStone", Transform(runeUid).Coordinates);

        // Гибируем жертву (части тела, мозг, вещи остаются на полу)
        var victimName = MetaData(victim).EntityName;
        _bodySystem.GibBody(victim, gibOrgans: true);

        _audio.PlayPvs("/Audio/Effects/gib.ogg", runeUid);
        _popup.PopupEntity(Loc.GetString("cult-sacrifice-complete", ("name", victimName)), invoker, invoker);
        BroadcastCultMessage(Loc.GetString("cult-commune-sacrifice", ("name", victimName)));

        // Уведомляем game rule о жертве
        var ev = new CultSacrificeCompletedEvent(victim, victimMindId);
        RaiseLocalEvent(ev);
    }

    private void ActivateReviveRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        if (rune.ReviveCharges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-revive-no-charges"), invoker, invoker);
            return;
        }

        // Ищем мёртвого культиста рядом
        var pos = _xform.GetMapCoordinates(uid);
        EntityUid? deadCultist = FindDeadCultistNearby(pos);
        if (deadCultist == null)
        {
            _popup.PopupEntity(Loc.GetString("cult-revive-no-target"), invoker, invoker);
            return;
        }

        rune.ReviveCharges -= 1;

        // Воскрешаем: полное исцеление + восстановление состояния через RejuvenateEvent
        _rejuvenate.PerformRejuvenate(deadCultist.Value);

        _audio.PlayPvs(CultMagicSound, uid);
        _popup.PopupEntity(Loc.GetString("cult-revive-success", ("name", MetaData(deadCultist.Value).EntityName)), invoker, invoker);
    }

    private void ActivateBarrierRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Собираем цепочку барьерных рун (эта + соседние в радиусе 3 тайлов)
        var chain = FindBarrierChain(uid);
        var engagedRunes = chain.Count;

        // Проверяем активен ли барьер этой руны
        bool isActive = TryComp<CultBarrierRuneComponent>(uid, out var barrierComp)
                        && barrierComp.BarrierEntity.HasValue
                        && Exists(barrierComp.BarrierEntity.Value);

        if (!isActive)
        {
            // Первая активация: спавним барьеры для всей цепочки
            var activatedRunes = 0;
            foreach (var runeUid in chain)
            {
                var bc = EnsureComp<CultBarrierRuneComponent>(runeUid);
                // Не дублируем если уже есть
                if (bc.BarrierEntity.HasValue && Exists(bc.BarrierEntity.Value))
                    continue;
                var barrier = Spawn("CultWallForce", Transform(runeUid).Coordinates);
                bc.BarrierEntity = barrier;
                activatedRunes++;
            }

            if (activatedRunes > 0 && engagedRunes > 0)
                _cult.DealSelfDamage(invoker, 2f * engagedRunes);

            _popup.PopupEntity(Loc.GetString("cult-barrier-rune-activated"), invoker, invoker);
        }
        else
        {
            // Переключение: деактивируем все барьеры цепочки
            foreach (var runeUid in chain)
            {
                if (!TryComp<CultBarrierRuneComponent>(runeUid, out var bc)) continue;
                if (bc.BarrierEntity.HasValue && Exists(bc.BarrierEntity.Value))
                    QueueDel(bc.BarrierEntity.Value);
                bc.BarrierEntity = null;
            }

            _popup.PopupEntity(Loc.GetString("cult-barrier-rune-deactivated"), invoker, invoker);
        }
        _audio.PlayPvs(CultMagicSound, uid);
    }

    // Возвращает все барьерные руны, соединённые цепью (в радиусе 3 тайлов)
    private List<EntityUid> FindBarrierChain(EntityUid uid)
    {
        var result = new List<EntityUid> { uid };
        var pos = _xform.GetMapCoordinates(uid);
        var query = EntityQueryEnumerator<CultRuneComponent, TransformComponent>();
        while (query.MoveNext(out var runeUid, out var r, out var xform))
        {
            if (runeUid == uid) continue;
            if (r.RuneType != CultRuneType.Barrier) continue;
            if ((_xform.GetMapCoordinates(runeUid, xform).Position - pos.Position).Length() > 3f) continue;
            result.Add(runeUid);
        }
        return result;
    }

    private void ActivateSummoningRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Телепортирует всех культистов, находящихся далеко от руны, к ней
        // (не трогает тех, кто уже рядом — радиус 3 плитки)
        const float nearRadius = 3f;
        var pos = _xform.GetMapCoordinates(uid);

        var teleported = new List<string>();
        var query = EntityQueryEnumerator<CultistComponent, TransformComponent>();
        while (query.MoveNext(out var cUid, out _, out var cXform))
        {
            if (cUid == invoker) continue;
            if (_mobState.IsDead(cUid)) continue;
            if (cXform.MapID != pos.MapId) continue;

            var dist = (cXform.WorldPosition - pos.Position).Length();
            if (dist <= nearRadius) continue; // уже рядом — не трогаем

            _xform.SetWorldPosition(cUid, pos.Position);
            teleported.Add(MetaData(cUid).EntityName);
        }

        if (teleported.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-summoning-no-target"), invoker, invoker);
            return;
        }

        DestroyRune(uid, rune);
        _audio.PlayPvs("/Audio/Magic/teleport_arrival.ogg", uid);
        var names = string.Join(", ", teleported);
        _popup.PopupEntity(Loc.GetString("cult-summoning-success-multi", ("names", names)), invoker, invoker);
    }

    private void ActivateBloodBoilRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Жжёт всех не-культистов в зоне видимости
        var pos = _xform.GetMapCoordinates(uid);

        var burnDamage = new DamageSpecifier();
        burnDamage.DamageDict.Add("Heat", 45);

        // Собираем цели заранее, чтобы не получить exception при изменении коллекции
        var targets = new List<EntityUid>();
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var eUid, out var eXform))
        {
            if (eXform.MapID != pos.MapId) continue;
            if ((eXform.WorldPosition - pos.Position).Length() > 8f) continue;
            if (IsCultAligned(eUid)) continue;
            if (!HasComp<DamageableComponent>(eUid)) continue;
            targets.Add(eUid);
        }
        foreach (var target in targets)
            _damage.TryChangeDamage(target, burnDamage, true);

        // Визуальный эффект: волны в несколько волн расходящимися кругами
        var runeCoords = Transform(uid).Coordinates;
        Spawn("FliptoniumWaveEffect", runeCoords);
        Timer.Spawn(200, () =>
        {
            if (!EntityManager.EntityExists(uid) || TerminatingOrDeleted(uid)) return;
            Spawn("FliptoniumWaveEffect", Transform(uid).Coordinates);
        });
        Timer.Spawn(400, () =>
        {
            if (!EntityManager.EntityExists(uid) || TerminatingOrDeleted(uid)) return;
            Spawn("FliptoniumWaveEffect", Transform(uid).Coordinates);
        });

        // Затраты HP для активаторов
        var costSpec = new DamageSpecifier();
        costSpec.DamageDict.Add("Brute", 10);
        _damage.TryChangeDamage(invoker, costSpec, true);

        _audio.PlayPvs("/Audio/Effects/fire.ogg", uid);
        DestroyRune(uid, rune);
    }

    private void ActivateSpiritRealmRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        // Открываем BUI выбора — руна не уничтожается немедленно
        _ui.TryOpenUi(uid, CultSpiritRealmBuiKey.Key, invoker);
    }

    private void OnSpiritRealmChoice(EntityUid uid, CultRuneComponent rune, CultSpiritRealmChoiceMessage msg)
    {
        var invoker = msg.Actor;

        if (!HasComp<CultistComponent>(invoker))
            return;

        if (msg.IsHomunculi)
            SummonHomunculi(uid, rune, invoker);
        else
            AscendAsDarkSpirit(uid, rune, invoker);
    }

    // ─── Summoning Homunculi ─────────────────────────────────────────────────

    private void SummonHomunculi(EntityUid runeUid, CultRuneComponent rune, EntityUid invoker)
    {
        // Проверяем, что мы на станции
        if (_station.GetOwningStation(runeUid) == null)
        {
            _popup.PopupEntity(Loc.GetString("cult-spirit-realm-no-station"), invoker, invoker, PopupType.MediumCaution);
            return;
        }

        // Уже привязан?
        if (TryComp<CultSpiritTetherComponent>(invoker, out var existingTether))
        {
            // Уже привязан — добавим ещё гомункула если можно
            if (existingTether.Homunculi.Count >= CultSpiritTetherComponent.MaxHomunculi)
            {
                _popup.PopupEntity(Loc.GetString("cult-spirit-realm-max-homunculi"), invoker, invoker, PopupType.MediumCaution);
                return;
            }

            var extra = Spawn("MobCultHomunculus", Transform(runeUid).Coordinates);
            existingTether.Homunculi.Add(extra);

            _popup.PopupEntity(Loc.GetString("cult-spirit-realm-homunculus-summoned"), invoker, invoker, PopupType.Medium);
            _audio.PlayPvs(CultMagicSound, runeUid);
            return;
        }

        // Первый гомункул — создаём привязку
        var homunculus = Spawn("MobCultHomunculus", Transform(runeUid).Coordinates);

        var tether = EnsureComp<CultSpiritTetherComponent>(invoker);
        tether.RuneUid = runeUid;
        tether.Homunculi.Add(homunculus);

        _audio.PlayPvs(CultMagicSound, runeUid);
        _popup.PopupEntity(Loc.GetString("cult-spirit-realm-homunculus-summoned"), invoker, invoker, PopupType.Large);
    }

    // ─── Dark Spirit Ascension ───────────────────────────────────────────────

    private void AscendAsDarkSpirit(EntityUid runeUid, CultRuneComponent rune, EntityUid invoker)
    {
        // Нужен разум для переноса
        if (!_mind.TryGetMind(invoker, out var mindId, out _))
            return;

        var coords = Transform(invoker).Coordinates;

        // Спавним тёмного духа (красный призрак с доступом к культ-каналу)
        var spirit = Spawn("MobCultDarkSpirit", coords);

        // Сохраняем ссылку на оригинальное тело в компоненте духа
        var spiritComp = EnsureComp<CultDarkSpiritComponent>(spirit);
        spiritComp.OriginalBody = invoker;

        // Выдаём actions духу
        _actions.AddAction(spirit, ActionDarkSpiritReturn);
        _actions.AddAction(spirit, ActionDarkSpiritCommune);

        EnsureComp<IntrinsicRadioReceiverComponent>(spirit);
        var activeRadio = EnsureComp<ActiveRadioComponent>(spirit);
        if (!activeRadio.Channels.Contains("Cult"))
            activeRadio.Channels.Add("Cult");

        // Переносим разум в сущность духа
        _mind.TransferTo(mindId, spirit);

        _cult.DealSelfDamage(invoker, 10f);
        _audio.PlayPvs(CultMagicSound, runeUid);
        _popup.PopupEntity(Loc.GetString("cult-spirit-realm-ascend-enter"), spirit, spirit, PopupType.Large);
        DestroyRune(runeUid, rune);

        // Автовозврат через 60 секунд
        Timer.Spawn(60_000, () =>
        {
            if (!EntityManager.EntityExists(spirit) || TerminatingOrDeleted(spirit))
                return;
            ReturnFromSpirit(spirit);
        });
    }

    private void ReturnFromSpirit(EntityUid spirit)
    {
        if (!TryComp<CultDarkSpiritComponent>(spirit, out var comp))
            return;
        if (!_mind.TryGetMind(spirit, out var mindId, out _))
        {
            QueueDel(spirit);
            return;
        }

        var body = comp.OriginalBody;
        if (EntityManager.EntityExists(body) && !TerminatingOrDeleted(body))
        {
            _mind.TransferTo(mindId, body);
            _stamina.TakeStaminaDamage(body, 100f);
            _popup.PopupEntity(Loc.GetString("cult-spirit-realm-ascend-exit"), body, body, PopupType.Medium);
        }
        QueueDel(spirit);
    }

    private void BreakSpiritTether(EntityUid cultistUid, CultSpiritTetherComponent tether)
    {
        foreach (var homunculus in tether.Homunculi)
        {
            if (EntityManager.EntityExists(homunculus) && !TerminatingOrDeleted(homunculus))
                QueueDel(homunculus);
        }

        _popup.PopupEntity(Loc.GetString("cult-spirit-realm-tether-break"), cultistUid, cultistUid, PopupType.MediumCaution);
        RemCompDeferred<CultSpiritTetherComponent>(cultistUid);
    }

    // ─── Dark Spirit Action Handlers ────────────────────────────────────────

    private void OnDarkSpiritReturn(EntityUid uid, CultDarkSpiritComponent comp, CultDarkSpiritReturnActionEvent args)
    {
        args.Handled = true;
        ReturnFromSpirit(uid);
    }

    private void OnDarkSpiritCommune(EntityUid uid, CultDarkSpiritComponent comp, CultDarkSpiritCommuneActionEvent args)
    {
        args.Handled = true;

        if (TryComp<CultistComponent>(comp.OriginalBody, out var cultist) && cultist.BuiHolder.HasValue)
        {
            _ui.TryOpenUi(cultist.BuiHolder.Value, CultCommuneBuiKey.Key, uid);
            return;
        }

        _ui.TryOpenUi(uid, CultCommuneBuiKey.Key, uid);
    }

    private void ActivateNarSieRune(EntityUid uid, CultRuneComponent rune, EntityUid invoker)
    {
        if (!_cultRule.AreRequiredSacrificesComplete(out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("cult-commune-sacrifices-required-first"), invoker, invoker, PopupType.LargeCaution);
            return;
        }

        // Финальный ритуал — нужно 9 культистов
        if (rune.InvokersOnRune < 9)
        {
            _popup.PopupEntity(
                Loc.GetString("cult-narsie-need-nine", ("have", rune.InvokersOnRune)),
                invoker, invoker, PopupType.LargeCaution);
            return;
        }

        // Вызов Нар'Си
        Spawn(NarSieSpawnProto, Transform(uid).Coordinates);
        BroadcastCultMessage(Loc.GetString("cult-commune-narsie-summoned"));
        var ev = new CultNarSieSummonedEvent();
        RaiseLocalEvent(ev);

        DestroyRune(uid, rune);
    }

    // ──────────────────────── Helpers ────────────────────────────────

    private int CountInvokersNearby(MapCoordinates pos, EntityUid mainInvoker)
    {
        int count = 0;
        var cultistQuery = EntityQueryEnumerator<CultistComponent, TransformComponent>();
        while (cultistQuery.MoveNext(out _, out _, out var cXform))
        {
            if (cXform.MapID != pos.MapId) continue;
            if ((cXform.WorldPosition - pos.Position).Length() > 1.5f) continue;
            count++;
        }

        var constructQuery = EntityQueryEnumerator<NpcFactionMemberComponent, TransformComponent>();
        while (constructQuery.MoveNext(out var cUid, out var faction, out var cXform))
        {
            if (HasComp<CultistComponent>(cUid)) continue;
            if (!_npcFaction.IsMember((cUid, faction), CultFaction)) continue;
            if (cXform.MapID != pos.MapId) continue;
            if ((cXform.WorldPosition - pos.Position).Length() > 1.5f) continue;
            count++;
        }

        return count;
    }

    private EntityUid? FindAdjacentNonCultist(MapCoordinates pos, EntityUid invoker)
    {
        var query = EntityQueryEnumerator<DamageableComponent, TransformComponent>();
        while (query.MoveNext(out var eUid, out _, out var eXform))
        {
            if (eUid == invoker) continue;
            if (IsCultAligned(eUid)) continue;
            if (!HasComp<MobStateComponent>(eUid)) continue; // только живые существа, не стены
            if (eXform.MapID != pos.MapId) continue;
            if ((eXform.WorldPosition - pos.Position).Length() > 1.5f) continue;
            return eUid;
        }
        return null;
    }

    private EntityUid? FindDeadCultistNearby(MapCoordinates pos)
    {
        var query = EntityQueryEnumerator<CultistComponent, TransformComponent>();
        while (query.MoveNext(out var cUid, out _, out var cXform))
        {
            if (cXform.MapID != pos.MapId) continue;
            if ((cXform.WorldPosition - pos.Position).Length() > 1.5f) continue;
            if (_mobState.IsDead(cUid)) return cUid;
        }
        return null;
    }

    /// <summary>
    /// Уничтожает руну: если задан DestructionState — спавнит визуальный эффект
    /// анимации разрушения, затем немедленно удаляет руну.
    /// </summary>
    private void DestroyRune(EntityUid uid, CultRuneComponent rune)
    {
        if (rune.DestructionState != null)
        {
            // Спавним визуальный эффект (анимированный спрайт) перед удалением
            Spawn("CultRuneDestructionEffect", Transform(uid).Coordinates);
        }
        QueueDel(uid);
    }

    public void BroadcastCultMessage(string message)
    {
        var query = EntityQueryEnumerator<CultistComponent>();
        while (query.MoveNext(out var cUid, out _))
        {
            _popup.PopupEntity(message, cUid, cUid, PopupType.Medium);
        }

        var constructQuery = EntityQueryEnumerator<CultConstructComponent>();
        while (constructQuery.MoveNext(out var cUid, out _))
        {
            _popup.PopupEntity(message, cUid, cUid, PopupType.Medium);
        }
    }

}

// ─── Local Events ───

/// <summary>Вызывается когда жертва принесена на руне подношения.</summary>
public sealed class CultSacrificeCompletedEvent : EntityEventArgs
{
    public EntityUid Victim { get; }
    public EntityUid? VictimMind { get; }

    public CultSacrificeCompletedEvent(EntityUid victim, EntityUid? victimMind = null)
    {
        Victim = victim;
        VictimMind = victimMind;
    }
}

/// <summary>Вызывается когда 9 культистов инвоцировали руну Нар'Си.</summary>
public sealed class CultNarSieSummonedEvent : EntityEventArgs { }

