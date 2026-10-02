using System.Linq;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Temperature.Components;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>
/// Ксеноморфы SS13 (code/modules/mob/living/carbon/alien): плазма и регенерация на сорняках,
/// шёпот, передача плазмы, эволюция каст и последствия смерти королевы.
/// </summary>
public sealed partial class XenomorphSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>Life() у мобов SS13 — раз в 2 секунды.</summary>
    private static readonly TimeSpan LifeInterval = TimeSpan.FromSeconds(2);

    private static readonly SoundSpecifier DeathSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/hiss6.ogg");
    private static readonly SoundSpecifier RoarSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/hiss5.ogg");

    /// <summary>hivenode/queen_death: Stun(200) — оглушение улья на 20 секунд.</summary>
    private static readonly TimeSpan QueenDeathStun = TimeSpan.FromSeconds(20);

    /// <summary>QUEEN_DEATH_DEBUFF_DURATION: 2400 децисекунд без эволюции.</summary>
    private static readonly TimeSpan QueenDeathDebuff = TimeSpan.FromSeconds(240);

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<DamageGroupPrototype> AirlossGroup = "Airloss";

    private static readonly ProtoId<AlertPrototype> HealthAlert = "XenoHealth";
    private static readonly ProtoId<AlertPrototype> QueenFinderAlert = "XenoQueenFinder";
    private static readonly ProtoId<AlertPrototype> FireAlert = "XenoFire";
    private static readonly ProtoId<AlertPrototype> PlasmaAirAlert = "XenoPlasmaAir";
    private static readonly ProtoId<AlertPrototype> NoQueenAlert = "XenoNoQueen";

    /// <summary>plas_detect_threshold (кПа).</summary>
    private const float PlasmaDetectThreshold = 0.02f;

    /// <summary>BODYTEMP_HEAT_DAMAGE_LIMIT у ксеноморфов.</summary>
    private const float HeatDamageLimit = 360f;

    private TimeSpan _nextLife;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoPlasmaComponent, MapInitEvent>(OnPlasmaMapInit);
        SubscribeLocalEvent<XenomorphComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<XenomorphComponent, MapInitEvent>(OnXenoMapInit);
        SubscribeLocalEvent<XenomorphComponent, DamageChangedEvent>(OnXenoDamaged);
        SubscribeLocalEvent<XenomorphComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<XenomorphComponent, XenoWhisperActionEvent>(OnWhisper);
        SubscribeLocalEvent<XenomorphComponent, XenoTransferPlasmaActionEvent>(OnTransferPlasma);
        SubscribeLocalEvent<XenomorphComponent, XenoEvolveActionEvent>(OnEvolve);
        SubscribeLocalEvent<XenomorphComponent, XenoRadialPickMessage>(OnRadialPick);

        InitializeAbilities();
        InitializeHive();
        InitializeInfection();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateAbilities(now);
        UpdateHive(now);
        UpdateInfection(now);

        if (now < _nextLife)
            return;

        _nextLife = now + LifeInterval;

        var query = EntityQueryEnumerator<XenomorphComponent, XenoPlasmaComponent>();
        while (query.MoveNext(out var uid, out var xeno, out var plasma))
        {
            if (_mobState.IsDead(uid))
                continue;

            var gained = Life((uid, plasma));
            DigestStomach(uid, now);

            if (xeno.NoQueenUntil != TimeSpan.Zero && now >= xeno.NoQueenUntil)
            {
                xeno.NoQueenUntil = TimeSpan.Zero;
                _alerts.ClearAlert(uid, NoQueenAlert);
                _popup.PopupEntity(Loc.GetString("xeno-queen-death-cleared"), uid, uid, PopupType.Medium);
            }

            // alien/handle_environment: выше BODYTEMP_HEAT_DAMAGE_LIMIT (360 K) — алерт alien_fire.
            SetAlert(uid, FireAlert, TryComp<TemperatureComponent>(uid, out var temperature)
                && temperature.CurrentTemperature > HeatDamageLimit);

            UpdateQueenFinder((uid, xeno));

            // larva/Life: +0.5 в секунду, и ещё +1 за каждое пополнение плазмы (larva/adjustPlasma).
            if (TryComp<XenoLarvaComponent>(uid, out var larva) && larva.Growth < larva.MaxGrowth)
            {
                var growth = 0.5f * (float) LifeInterval.TotalSeconds + (gained ? 1 : 0);
                larva.Growth = Math.Min(larva.MaxGrowth, larva.Growth + growth);
                UpdateLarvaVisuals((uid, larva));
                if (larva.Growth >= larva.MaxGrowth)
                    _popup.PopupEntity(Loc.GetString("xeno-larva-grown"), uid, uid, PopupType.Medium);
            }
        }
    }

    #region Плазма

    private void OnPlasmaMapInit(Entity<XenoPlasmaComponent> ent, ref MapInitEvent args)
    {
        UpdatePlasmaAlert(ent);
    }

    #region HUD

    private void OnXenoMapInit(Entity<XenomorphComponent> ent, ref MapInitEvent args)
    {
        UpdateHealthAlert(ent);
        if (ent.Comp.Caste != XenoCaste.Queen)
            _alerts.ShowAlert(ent.Owner, QueenFinderAlert);
    }

    private void OnXenoDamaged(Entity<XenomorphComponent> ent, ref DamageChangedEvent args)
    {
        UpdateHealthAlert(ent);
    }

    /// <summary>
    /// carbon/update_health_hud: health0 при полном здоровье, health[6 − ceil(health / (maxHealth × 0.2))] пока жив,
    /// health6 в крите, health7 мёртв. Здоровье SS13 = maxHealth − урон, maxHealth — порог крита.
    /// </summary>
    private void UpdateHealthAlert(EntityUid uid)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        short severity;
        if (_mobState.IsDead(uid))
        {
            severity = 7;
        }
        else
        {
            var maxHealth = _thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit, thresholds)
                ? crit.Value.Float()
                : 100f;
            var health = maxHealth - _damageable.GetTotalDamage(uid).Float();
            if (health >= maxHealth)
                severity = 0;
            else if (health > 0)
                severity = (short) (6 - MathF.Ceiling(health / (maxHealth * 0.2f)));
            else
                severity = 6;
        }

        _alerts.ShowAlert(uid, HealthAlert, severity);
    }

    /// <summary>findQueen: направление и дальность до королевы на том же Z-уровне (рядом — center, 2–7 — near, 8–20 — med, дальше — far).</summary>
    private void UpdateQueenFinder(Entity<XenomorphComponent> ent)
    {
        if (ent.Comp.Caste == XenoCaste.Queen)
            return;

        sbyte distance = -1;
        var direction = Direction.South;
        var own = _xform.GetMapCoordinates(ent);
        var queens = EntityQueryEnumerator<XenomorphComponent>();
        while (queens.MoveNext(out var queen, out var xeno))
        {
            if (xeno.Caste != XenoCaste.Queen || _mobState.IsDead(queen) || !_mind.TryGetMind(queen, out _, out _))
                continue;

            var target = _xform.GetMapCoordinates(queen);
            if (target.MapId != own.MapId)
                break;

            var delta = target.Position - own.Position;
            var tiles = (int) MathF.Round(Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y)));
            distance = tiles switch
            {
                <= 1 => 0,
                <= 7 => 1,
                <= 20 => 2,
                _ => 3,
            };
            direction = delta.GetDir();
            break;
        }

        if (ent.Comp.QueenDistance == distance && ent.Comp.QueenDirection == direction)
            return;

        ent.Comp.QueenDistance = distance;
        ent.Comp.QueenDirection = direction;
        Dirty(ent);
    }

    private void SetAlert(EntityUid uid, ProtoId<AlertPrototype> alert, bool shown)
    {
        if (shown)
            _alerts.ShowAlert(uid, alert);
        else
            _alerts.ClearAlert(uid, alert);
    }

    #endregion
    /// <summary>
    /// organ/alien/plasmavessel/on_life: вне сорняков — 0.1 × plasma_rate; на сорняках при полном здоровье — plasma_rate,
    /// раненым — 0.5 × plasma_rate и лечение heal_rate отдельно по brute, burn и oxy (всё в секунду).
    /// Возвращает, пополнилась ли плазма.
    /// </summary>
    private bool Life(Entity<XenoPlasmaComponent> ent)
    {
        var seconds = (float) LifeInterval.TotalSeconds;
        var gain = 0.1f * ent.Comp.PlasmaRate * seconds;

        if (IsOnWeeds(ent))
        {
            var damaged = _damageable.GetTotalDamage(ent.Owner) > 0;
            if (!damaged)
            {
                gain = ent.Comp.PlasmaRate * seconds;
            }
            else
            {
                gain = 0.5f * ent.Comp.PlasmaRate * seconds;
                var heal = -ent.Comp.HealRate * seconds;
                _damageable.HealEvenly(ent.Owner, heal, BruteGroup);
                _damageable.HealEvenly(ent.Owner, heal, BurnGroup);
                _damageable.HealEvenly(ent.Owner, heal, AirlossGroup);
            }
        }

        // alien/check_breath: плазма с парциальным давлением выше 0.02 кПа (moles × 250) переходит в запас,
        // взамен выдыхается кислород, и висит алерт alien_plas.
        var plasmaInAir = false;
        if (_atmos.GetContainingMixture(ent.Owner, excite: true) is { } air && air.TotalMoles > 0)
        {
            var plasmaPressure = air.Pressure * air.GetMoles(Gas.Plasma) / air.TotalMoles;
            var breathPlasma = air.GetMoles(Gas.Plasma) * Atmospherics.BreathPercentage;
            if (plasmaPressure > PlasmaDetectThreshold && breathPlasma > 0)
            {
                plasmaInAir = true;
                gain += breathPlasma * 250;
                air.AdjustMoles(Gas.Plasma, -breathPlasma);
                air.AdjustMoles(Gas.Oxygen, breathPlasma);
            }
        }

        SetAlert(ent, PlasmaAirAlert, plasmaInAir);

        AdjustPlasma(ent, gain);
        return gain > 0;
    }

    public void AdjustPlasma(Entity<XenoPlasmaComponent> ent, float amount)
    {
        ent.Comp.Plasma = Math.Clamp(ent.Comp.Plasma + amount, 0, ent.Comp.MaxPlasma);
        Dirty(ent);
    }

    private void UpdatePlasmaAlert(Entity<XenoPlasmaComponent> ent)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert);
    }

    /// <summary>Проверяет и списывает плазму (cooldown/alien/IsAvailable + Activate).</summary>
    public bool TrySpendPlasma(EntityUid uid, float cost, bool popup = true)
    {
        if (cost <= 0)
            return true;

        if (!TryComp<XenoPlasmaComponent>(uid, out var plasma) || plasma.Plasma < cost)
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("xeno-not-enough-plasma"), uid, uid);
            return false;
        }

        AdjustPlasma((uid, plasma), -cost);
        return true;
    }

    public bool HasPlasma(EntityUid uid, float cost)
    {
        return cost <= 0 || TryComp<XenoPlasmaComponent>(uid, out var plasma) && plasma.Plasma >= cost;
    }

    /// <summary>Стоит ли сущность на сорняках улья.</summary>
    public bool IsOnWeeds(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (HasComp<XenoWeedsComponent>(anchored))
                return true;
        }

        return false;
    }

    #endregion

    #region Осмотр, смерть, королева

    private void OnExamined(Entity<XenomorphComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<XenoPlasmaComponent>(ent, out var plasma) && HasComp<XenomorphComponent>(args.Examiner))
            args.PushMarkup(Loc.GetString("xeno-examine-plasma", ("plasma", (int) plasma.Plasma), ("max", (int) plasma.MaxPlasma)));
    }

    private void OnMobStateChanged(Entity<XenomorphComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateHealthAlert(ent);
        if (args.NewMobState != MobState.Dead)
            return;

        if (ent.Comp.Caste != XenoCaste.Larva)
            _audio.PlayPvs(DeathSound, ent);

        if (ent.Comp.Caste == XenoCaste.Queen)
            OnQueenDeath(ent);
    }

    /// <summary>hivenode/queen_death: агония и оглушение всего улья.</summary>
    private void OnQueenDeath(EntityUid queen)
    {
        var query = EntityQueryEnumerator<XenomorphComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid == queen || _mobState.IsDead(uid))
                continue;

            _popup.PopupEntity(Loc.GetString("xeno-queen-died"), uid, uid, PopupType.LargeCaution);
            _audio.PlayPvs(RoarSound, uid);
            _stun.TryUpdateStunDuration(uid, QueenDeathStun);
            _jitter.DoJitter(uid, TimeSpan.FromMinutes(1), true);
            _stutter.DoStutter(uid, TimeSpan.FromMinutes(1), true);
            Comp<XenomorphComponent>(uid).NoQueenUntil = _timing.CurTime + QueenDeathDebuff;
            _alerts.ShowAlert(uid, NoQueenAlert);
        }
    }

    /// <summary>get_alien_type: живой ксеноморф этой касты с игроком.</summary>
    public bool IsCasteAlive(XenoCaste caste, EntityUid? ignored = null)
    {
        var query = EntityQueryEnumerator<XenomorphComponent>();
        while (query.MoveNext(out var uid, out var xeno))
        {
            if (uid != ignored && xeno.Caste == caste && !_mobState.IsDead(uid) && _mind.TryGetMind(uid, out _, out _))
                return true;
        }

        return false;
    }

    #endregion

    #region Шёпот и передача плазмы

    private void OnWhisper(Entity<XenomorphComponent> ent, ref XenoWhisperActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        var target = args.Target;
        var cost = args.PlasmaCost;
        if (!HasPlasma(ent, cost))
        {
            _popup.PopupEntity(Loc.GetString("xeno-not-enough-plasma"), ent, ent);
            return;
        }

        args.Handled = true;
        _quickDialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("xeno-whisper-title"), Loc.GetString("xeno-whisper-prompt"),
            message =>
            {
                if (string.IsNullOrWhiteSpace(message) || TerminatingOrDeleted(target) || !TrySpendPlasma(ent, cost))
                    return;

                message = FormattedMessage.EscapeText(message.Trim());
                if (TryComp<ActorComponent>(target, out var targetActor))
                    _chatManager.DispatchServerMessage(targetActor.PlayerSession, Loc.GetString("xeno-whisper-received", ("message", message)));

                _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("xeno-whisper-sent", ("target", target), ("message", message)));
            });
    }

    private void OnTransferPlasma(Entity<XenomorphComponent> ent, ref XenoTransferPlasmaActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        var target = args.Target;
        if (!HasComp<XenoPlasmaComponent>(target) || target == ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("xeno-transfer-invalid"), ent, ent);
            return;
        }

        args.Handled = true;
        _quickDialog.OpenDialog<int>(actor.PlayerSession, Loc.GetString("xeno-transfer-title"), Loc.GetString("xeno-transfer-prompt"),
            amount =>
            {
                if (amount <= 0 || TerminatingOrDeleted(target)
                    || !TryComp<XenoPlasmaComponent>(ent, out var own) || !TryComp<XenoPlasmaComponent>(target, out var other))
                {
                    return;
                }

                var transfer = Math.Min(amount, own.Plasma);
                AdjustPlasma((ent, own), -transfer);
                AdjustPlasma((target, other), transfer);
                _popup.PopupEntity(Loc.GetString("xeno-transfer-done", ("amount", (int) transfer), ("target", target)), ent, ent);
                _popup.PopupEntity(Loc.GetString("xeno-transfer-received", ("amount", (int) transfer), ("source", ent.Owner)), target, target);
            });
    }

    #endregion

    #region Эволюция

    private void OnEvolve(Entity<XenomorphComponent> ent, ref XenoEvolveActionEvent args)
    {
        if (args.Handled)
            return;

        // IsAvailable: только на полу (не в трубах и не в желудке).
        if (_container.IsEntityInContainer(ent.Owner))
            return;

        // Личинка выбирает касту в радиальном меню.
        if (TryComp<XenoLarvaComponent>(ent, out var larva))
        {
            if (larva.Growth < larva.MaxGrowth)
            {
                _popup.PopupEntity(Loc.GetString("xeno-larva-not-grown", ("growth", (int) larva.Growth), ("max", (int) larva.MaxGrowth)), ent, ent);
                return;
            }

            args.Handled = true;
            var options = larva.Castes.Select(c => RadialOption(c)).ToList();
            OpenRadial(ent, XenoRadialUiKey.Evolve, options);
            return;
        }

        if (ent.Comp.EvolvesTo is not { } next)
            return;

        // Преторианец: если королевы нет. Трутень: если нет ни преторианца, ни королевы.
        if (ent.Comp.Caste == XenoCaste.Praetorian && IsCasteAlive(XenoCaste.Queen)
            || ent.Comp.Caste == XenoCaste.Drone && (IsCasteAlive(XenoCaste.Queen) || IsCasteAlive(XenoCaste.Praetorian)))
        {
            _popup.PopupEntity(Loc.GetString("xeno-evolve-royal-exists"), ent, ent);
            return;
        }

        // node.recent_queen_death: после гибели королевы улей 4 минуты не может вырастить новую.
        if (_timing.CurTime < ent.Comp.NoQueenUntil)
        {
            _popup.PopupEntity(Loc.GetString("xeno-evolve-queen-death"), ent, ent);
            return;
        }

        if (!TrySpendPlasma(ent, ent.Comp.EvolveCost))
            return;

        args.Handled = true;
        Evolve(ent, next);
    }

    private XenoRadialOption RadialOption(EntProtoId proto)
    {
        return new XenoRadialOption(proto, _proto.Index(proto).Name, new SpriteSpecifier.EntityPrototype(proto));
    }

    public void OpenRadial(EntityUid uid, XenoRadialUiKey key, List<XenoRadialOption> options)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        _ui.SetUiState(uid, key, new XenoRadialState(options));
        _ui.OpenUi(uid, key, actor.PlayerSession);
    }

    private void OnRadialPick(Entity<XenomorphComponent> ent, ref XenoRadialPickMessage args)
    {
        if (args.Actor != ent.Owner)
            return;

        var key = (XenoRadialUiKey) args.UiKey;
        var picked = args.Id;
        _ui.CloseUi(ent.Owner, key);

        switch (key)
        {
            case XenoRadialUiKey.Evolve:
                if (TryComp<XenoLarvaComponent>(ent, out var larva) && larva.Growth >= larva.MaxGrowth
                    && larva.Castes.Any(c => c.Id == picked))
                {
                    Evolve(ent, picked);
                }
                break;
            case XenoRadialUiKey.Resin:
                SecreteResin(ent, picked);
                break;
        }
    }

    /// <summary>Эволюция: новое тело на том же месте, разум переносится.</summary>
    public EntityUid Evolve(EntityUid uid, EntProtoId into)
    {
        var coords = Transform(uid).Coordinates;
        var evolved = Spawn(into, coords);
        _xform.AttachToGridOrMap(evolved);

        // adult/alien_evolve: drop_all_held_items; содержимое желудка переходит в новый желудок.
        foreach (var held in _hands.EnumerateHeld(uid).ToArray())
        {
            _hands.TryDrop(uid, held);
        }

        if (_container.TryGetContainer(uid, XenoStomachComponent.ContainerId, out var oldStomach)
            && HasComp<XenoStomachComponent>(evolved))
        {
            var newStomach = _container.EnsureContainer<Robust.Shared.Containers.Container>(evolved, XenoStomachComponent.ContainerId);
            foreach (var eaten in oldStomach.ContainedEntities.ToArray())
            {
                _container.Remove(eaten, oldStomach, force: true);
                _container.Insert(eaten, newStomach);
            }
        }

        if (_mind.TryGetMind(uid, out var mindId, out _))
            _mind.TransferTo(mindId, evolved);

        _popup.PopupEntity(Loc.GetString("xeno-evolved", ("xeno", uid), ("caste", evolved)), evolved, PopupType.Medium);
        _audio.PlayPvs(RoarSound, evolved);
        QueueDel(uid);
        return evolved;
    }

    private void UpdateLarvaVisuals(Entity<XenoLarvaComponent> ent)
    {
        // larva_update_icons: amount_grown > 80 — larva2, > 50 — larva1.
        var stage = ent.Comp.Growth > 80 ? 2 : ent.Comp.Growth > 50 ? 1 : 0;
        _appearance.SetData(ent, XenoVisuals.LarvaStage, stage);
    }

    #endregion
}
