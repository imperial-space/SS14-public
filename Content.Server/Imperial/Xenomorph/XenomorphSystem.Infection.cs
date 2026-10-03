using Content.Server.Chat.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Temperature.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Item;
using Content.Shared.Temperature.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Xenomorph;

public sealed partial class XenomorphSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly Content.Shared.Tools.Systems.SharedToolSystem _tool = default!;
    [Dependency] private readonly Content.Shared.Standing.StandingStateSystem _standing = default!;

    private const string SlicingQuality = "Slicing";

    /// <summary>Сколько длится извлечение эмбриона.</summary>
    private static readonly TimeSpan EmbryoRemovalTime = TimeSpan.FromSeconds(10);
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    [Dependency] private readonly Content.Shared.Chemistry.EntitySystems.SharedSolutionContainerSystem _solutions = default!;

    private const string Spaceacillin = "Spaceacillin";

    private static readonly ProtoId<Content.Shared.Alert.AlertPrototype> NestSustenanceAlert = "XenoNestSustenance";

    private HashSet<EntityUid> _nestSustained = new();

    /// <summary>BODYTEMP_NORMAL.</summary>
    private const float NormalBodyTemperature = 310.15f;

    private const string MaskSlot = "mask";
    private const string HeadSlot = "head";
    private static readonly EntProtoId BurstSpawner = "ImperialXenoBurstSpawner";

    /// <summary>poll_time: сколько ждать призрака на роль личинки, прежде чем эмбрион откатится на стадию.</summary>
    private static readonly TimeSpan BurstPollTime = TimeSpan.FromSeconds(20);

    private const float HuggerDeathTemperature = 300;

    private TimeSpan _nextHuggerCheck;
    private TimeSpan _nextEmbryoLife;

    private void InitializeInfection()
    {
        SubscribeLocalEvent<XenoFacehuggerComponent, MapInitEvent>(OnHuggerMapInit);
        SubscribeLocalEvent<XenoFacehuggerComponent, ThrowDoHitEvent>(OnHuggerThrowHit);
        SubscribeLocalEvent<XenoFacehuggerComponent, GettingPickedUpAttemptEvent>(OnHuggerPickupAttempt);
        SubscribeLocalEvent<XenoFacehuggerComponent, Content.Shared.Interaction.AfterInteractEvent>(OnHuggerAttack);
        SubscribeLocalEvent<XenoFacehuggerComponent, DamageChangedEvent>(OnHuggerDamaged);
        SubscribeLocalEvent<XenoEmbryoComponent, ComponentStartup>(OnEmbryoStartup);
        SubscribeLocalEvent<XenoEmbryoComponent, InteractUsingEvent>(OnEmbryoInteractUsing);
        SubscribeLocalEvent<XenoEmbryoComponent, XenoEmbryoRemovalDoAfterEvent>(OnEmbryoRemoval);
        SubscribeLocalEvent<XenoLarvaComponent, GhostRoleSpawnerUsedEvent>(OnLarvaSpawned);
    }

    /// <summary>valid_to_attach: живой носитель со слотом маски, без TRAIT_XENO_IMMUNE (ксеноморф или уже носит эмбрион), без лицехвата на лице.</summary>
    public bool IsValidHost(EntityUid uid)
    {
        if (HasComp<XenomorphComponent>(uid) || HasComp<XenoEmbryoComponent>(uid) || _mobState.IsDead(uid))
            return false;

        if (!_inventory.HasSlot(uid, MaskSlot))
            return false;

        return !(_inventory.TryGetSlotEntity(uid, MaskSlot, out var mask) && HasComp<XenoFacehuggerComponent>(mask));
    }

    /// <summary>CanHug: как valid_to_attach, но шлем, закрывающий рот, отпугивает лицехвата заранее.</summary>
    public bool CanHug(EntityUid uid)
    {
        return IsValidHost(uid) && !IsMouthCoveredByHead(uid, out _);
    }

    private bool IsMouthCoveredByHead(EntityUid uid, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EntityUid? head)
    {
        return _inventory.TryGetSlotEntity(uid, HeadSlot, out head) && HasComp<IngestionBlockerComponent>(head);
    }

    private void UpdateInfection(TimeSpan now)
    {
        if (now >= _nextHuggerCheck)
        {
            _nextHuggerCheck = now + TimeSpan.FromSeconds(0.5);
            var huggers = EntityQueryEnumerator<XenoFacehuggerComponent>();
            while (huggers.MoveNext(out var uid, out var hugger))
            {
                UpdateHugger((uid, hugger), now);
            }
        }

        if (now < _nextEmbryoLife)
            return;

        _nextEmbryoLife = now + LifeInterval;
        var embryos = EntityQueryEnumerator<XenoEmbryoComponent>();
        while (embryos.MoveNext(out var uid, out var embryo))
        {
            UpdateEmbryo((uid, embryo), now);
        }

        var sustained = new HashSet<EntityUid>();
        var nests = EntityQueryEnumerator<XenoNestComponent, StrapComponent>();
        while (nests.MoveNext(out _, out _, out var strap))
        {
            foreach (var buckled in strap.BuckledEntities)
            {
                if (!HasNestSustenance(buckled))
                    continue;

                NestSustenance(buckled);
                sustained.Add(buckled);
                _alerts.ShowAlert(buckled, NestSustenanceAlert);
            }
        }

        // Алерт «Nest Vitalization» снимается, когда пленник покинул гнездо.
        foreach (var uid in _nestSustained)
        {
            if (!sustained.Contains(uid) && !TerminatingOrDeleted(uid))
                _alerts.ClearAlert(uid, NestSustenanceAlert);
        }

        _nestSustained = sustained;
    }

    /// <summary>post_buckle_mob: живой носитель эмбриона или лицехвата в гнезде получает nest_sustenance.</summary>
    private bool HasNestSustenance(EntityUid uid)
    {
        if (!_mobState.IsAlive(uid) && !_mobState.IsCritical(uid))
            return false;

        if (TryComp<BuckleComponent>(uid, out var buckle) && buckle.BuckledTo is { } strap && !HasComp<XenoNestComponent>(strap))
            return false;

        return HasComp<XenoEmbryoComponent>(uid)
            || _inventory.TryGetSlotEntity(uid, MaskSlot, out var mask) && HasComp<XenoFacehuggerComponent>(mask);
    }

    /// <summary>nest_sustenance/tick: в секунду −2 brute, −2 burn, −4 oxy, −4 выносливости; температура тела в норме.</summary>
    private void NestSustenance(EntityUid uid)
    {
        var seconds = (float) LifeInterval.TotalSeconds;
        _damageable.HealEvenly(uid, -2 * seconds, BruteGroup);
        _damageable.HealEvenly(uid, -2 * seconds, BurnGroup);
        _damageable.HealEvenly(uid, -4 * seconds, AirlossGroup);
        _stamina.TakeStaminaDamage(uid, -4 * seconds, visual: false);

        if (TryComp<TemperatureComponent>(uid, out var temperature) && temperature.CurrentTemperature < NormalBodyTemperature)
            _temperature.ForceChangeTemperature(uid, NormalBodyTemperature, temperature);
    }

    #region Лицехват

    private void OnHuggerMapInit(Entity<XenoFacehuggerComponent> ent, ref MapInitEvent args)
    {
        SetHuggerState(ent, XenoFacehuggerState.Active);
    }

    private void SetHuggerState(Entity<XenoFacehuggerComponent> ent, XenoFacehuggerState state)
    {
        ent.Comp.State = state;
        if (state is XenoFacehuggerState.Active or XenoFacehuggerState.Idle)
        {
            ent.Comp.NextStateAt = _timing.CurTime + TimeSpan.FromSeconds(
                _random.NextFloat((float) ent.Comp.MinActive.TotalSeconds, (float) ent.Comp.MaxActive.TotalSeconds));
        }

        _appearance.SetData(ent, XenoVisuals.FacehuggerState, state);
        _clothing.SetEquippedPrefix(ent, state switch
        {
            XenoFacehuggerState.Idle => "inactive",
            XenoFacehuggerState.Dead => "dead",
            XenoFacehuggerState.Impregnated => "impregnated",
            _ => null,
        });
    }

    private void UpdateHugger(Entity<XenoFacehuggerComponent> ent, TimeSpan now)
    {
        var hugger = ent.Comp;

        // Присосался: ждём оплодотворения.
        if (hugger.Victim is { } victim)
        {
            if (now >= hugger.ImpregnateAt)
                Impregnate(ent, victim);
            return;
        }

        if (hugger.State is XenoFacehuggerState.Dead or XenoFacehuggerState.Impregnated)
            return;

        // GoIdle/GoActive: без добычи засыпает на 20–40 с и снова просыпается.
        if (now >= hugger.NextStateAt)
        {
            SetHuggerState(ent, hugger.State == XenoFacehuggerState.Active ? XenoFacehuggerState.Idle : XenoFacehuggerState.Active);
            return;
        }

        // В руках или в контейнере не прыгает.
        if (hugger.State != XenoFacehuggerState.Active || _container.IsEntityInContainer(ent.Owner))
            return;

        // atmos_expose: выше 300 K лицехват погибает.
        if (_atmos.GetContainingMixture(ent.Owner) is { } air && air.Temperature > HuggerDeathTemperature)
        {
            Die(ent);
            return;
        }

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, hugger.LeapRange))
        {
            if (CanHug(mob) && Leap(ent, mob))
                return;
        }
    }

    /// <summary>react_to_mob / attack_hand: живой лицехват прыгает на того, кто пытается его взять.</summary>
    private void OnHuggerPickupAttempt(Entity<XenoFacehuggerComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.State != XenoFacehuggerState.Active || ent.Comp.Sterile || ent.Comp.Victim != null
            || HasComp<XenomorphComponent>(args.User))
        {
            return;
        }

        if (Leap(ent, args.User))
            args.Cancel();
    }

    /// <summary>facehugger/attack: ударить лицехватом — он перебирается на лицо цели.</summary>
    private void OnHuggerAttack(Entity<XenoFacehuggerComponent> ent, ref Content.Shared.Interaction.AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !IsValidHost(target))
            return;

        args.Handled = true;
        Leap(ent, target);
    }

    /// <summary>take_damage: при прочности ниже 90 из 100 лицехват погибает.</summary>
    private void OnHuggerDamaged(Entity<XenoFacehuggerComponent> ent, ref DamageChangedEvent args)
    {
        if (_damageable.GetTotalDamage(ent.Owner) > 10 && ent.Comp.State is XenoFacehuggerState.Active or XenoFacehuggerState.Idle)
            Die(ent);
    }

    /// <summary>throw_impact: брошенный активный лицехват бросается на лицо.</summary>
    private void OnHuggerThrowHit(Entity<XenoFacehuggerComponent> ent, ref ThrowDoHitEvent args)
    {
        if (ent.Comp.State == XenoFacehuggerState.Active && ent.Comp.Victim == null && IsValidHost(args.Target))
            Leap(ent, args.Target);
    }

    /// <summary>Leap: шлем, закрывающий рот, убивает лицехвата; маску срывает; затем Attach.</summary>
    private bool Leap(Entity<XenoFacehuggerComponent> ent, EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString("xeno-hugger-leap", ("hugger", ent.Owner), ("target", target)), target, PopupType.LargeCaution);

        if (IsMouthCoveredByHead(target, out var head))
        {
            _popup.PopupEntity(Loc.GetString("xeno-hugger-smash", ("hugger", ent.Owner), ("head", head.Value)), target, PopupType.LargeCaution);
            Die(ent);
            return false;
        }

        if (_inventory.TryGetSlotEntity(target, MaskSlot, out var mask))
        {
            if (!_inventory.TryUnequip(target, MaskSlot, force: true))
                return false;

            _popup.PopupEntity(Loc.GetString("xeno-hugger-tear-mask", ("hugger", ent.Owner), ("mask", mask.Value), ("target", target)), target, PopupType.MediumCaution);
        }

        if (_container.IsEntityInContainer(ent.Owner))
            _container.TryRemoveFromContainer(ent.Owner, true);

        if (!_inventory.TryEquip(target, ent, MaskSlot, silent: true, force: true))
            return false;

        Attach(ent, target);
        return true;
    }

    /// <summary>Attach: 5 урона и паралич на 1 с, через 10–15 с — оплодотворение.</summary>
    private void Attach(Entity<XenoFacehuggerComponent> ent, EntityUid victim)
    {
        ent.Comp.Victim = victim;
        ent.Comp.ImpregnateAt = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextFloat((float) ent.Comp.MinImpregnation.TotalSeconds, (float) ent.Comp.MaxImpregnation.TotalSeconds));
        EnsureComp<XenoLatchedComponent>(ent);

        if (!ent.Comp.Sterile)
        {
            _damageable.TryChangeDamage(victim, ent.Comp.AttachDamage);
            _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(1));
            _stun.TryKnockdown(victim, ent.Comp.AttachKnockdown, true, force: true);
        }

        // GoIdle: чтобы не прыгнуть на того, кто его сорвёт.
        SetHuggerState(ent, XenoFacehuggerState.Idle);
    }

    private void Impregnate(Entity<XenoFacehuggerComponent> ent, EntityUid victim)
    {
        ent.Comp.Victim = null;
        RemComp<XenoLatchedComponent>(ent);

        if (TerminatingOrDeleted(victim) || _mobState.IsDead(victim)
            || !_inventory.TryGetSlotEntity(victim, MaskSlot, out var mask) || mask != ent.Owner)
        {
            return;
        }

        // Стерильный лишь «насилует лицо» и остаётся жив.
        if (ent.Comp.Sterile)
        {
            _popup.PopupEntity(Loc.GetString("xeno-hugger-violates", ("hugger", ent.Owner), ("target", victim)), victim, PopupType.MediumCaution);
            return;
        }

        Die(ent);
        SetHuggerState(ent, XenoFacehuggerState.Impregnated);
        if (!HasComp<XenoEmbryoComponent>(victim))
        {
            EnsureComp<XenoEmbryoComponent>(victim);
            _popup.PopupEntity(Loc.GetString("xeno-hugger-impregnated", ("hugger", ent.Owner)), victim, victim, PopupType.MediumCaution);
        }
    }

    private void Die(Entity<XenoFacehuggerComponent> ent)
    {
        ent.Comp.Victim = null;
        RemComp<XenoLatchedComponent>(ent);
        SetHuggerState(ent, XenoFacehuggerState.Dead);
        _popup.PopupEntity(Loc.GetString("xeno-hugger-dies", ("hugger", ent.Owner)), ent, PopupType.Small);
    }

    #endregion

    #region Эмбрион

    private void OnEmbryoStartup(Entity<XenoEmbryoComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Stage = 1;
        ent.Comp.NextStage = _timing.CurTime + EmbryoGrowthTime(ent);
        Dirty(ent);
    }

    /// <summary>advance_embryo_stage: спейсациллин в крови замедляет рост вдвое, в гнезде эмбрион растёт на 20 % быстрее.</summary>
    private TimeSpan EmbryoGrowthTime(Entity<XenoEmbryoComponent> ent)
    {
        var time = ent.Comp.GrowthTime;
        if (TryComp<Content.Shared.Body.Components.BloodstreamComponent>(ent, out var blood)
            && _solutions.TryGetSolution(ent.Owner, blood.BloodSolutionName, out _, out var solution)
            && solution.ContainsReagent(Spaceacillin, null))
        {
            time *= 2;
        }

        if (HasNestSustenance(ent) && TryComp<BuckleComponent>(ent, out var buckle) && buckle.BuckledTo != null)
            time *= 0.8;
        return time;
    }

    private void UpdateEmbryo(Entity<XenoEmbryoComponent> ent, TimeSpan now)
    {
        var embryo = ent.Comp;
        if (TerminatingOrDeleted(ent))
            return;

        if (embryo.Stage < 6 && now >= embryo.NextStage)
        {
            embryo.Stage++;
            embryo.NextStage = now + EmbryoGrowthTime(ent);
            Dirty(ent);
        }

        // Личинка, которая выползает при извлечении, ждёт призрака и на 5-й стадии.
        if (embryo.Bursting && embryo.Stage < 6)
        {
            TryBurst(ent, now);
            return;
        }

        // body_egg/on_death: в трупе эмбрион растёт и вырывается, но симптомов нет.
        if (_mobState.IsDead(ent))
        {
            if (embryo.Stage == 6)
                TryBurst(ent, now);
            return;
        }

        // on_life: симптомы (вероятности SS13 за секунду, тик здесь — 2 секунды).
        switch (embryo.Stage)
        {
            case 3 or 4:
                if (_random.Prob(0.02f))
                    _chat.TryEmoteWithChat(ent, "Sneeze");
                if (_random.Prob(0.02f))
                    _chat.TryEmoteWithChat(ent, "Cough");
                if (_random.Prob(0.02f))
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-throat"), ent, ent, PopupType.SmallCaution);
                if (_random.Prob(0.02f))
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-mucous"), ent, ent, PopupType.SmallCaution);
                break;
            case 5:
                if (_random.Prob(0.02f))
                    _chat.TryEmoteWithChat(ent, "Sneeze");
                if (_random.Prob(0.02f))
                    _chat.TryEmoteWithChat(ent, "Cough");
                if (_random.Prob(0.04f))
                {
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-muscles"), ent, ent, PopupType.SmallCaution);
                    if (_random.Prob(0.2f))
                        _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } });
                }
                if (_random.Prob(0.04f))
                {
                    _popup.PopupEntity(Loc.GetString("xeno-embryo-stomach"), ent, ent, PopupType.SmallCaution);
                    if (_random.Prob(0.2f))
                        _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Poison"] = 1 } });
                }
                break;
            case 6:
                _popup.PopupEntity(Loc.GetString("xeno-embryo-tearing"), ent, ent, PopupType.MediumCaution);
                _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Poison"] = 10 } }, interruptsDoAfters: false);
                TryBurst(ent, now);
                break;
        }
    }

    /// <summary>attempt_grow: опрос призраков; никто не взял роль — откат на одну стадию.</summary>
    private void TryBurst(Entity<XenoEmbryoComponent> ent, TimeSpan now)
    {
        var embryo = ent.Comp;
        if (embryo.Bursting)
        {
            if (now < embryo.BurstDeadline)
                return;

            if (embryo.BurstSpawner is { } spawner)
                QueueDel(spawner);

            embryo.BurstSpawner = null;
            embryo.Bursting = false;
            embryo.NoGibBurst = false;
            embryo.Stage = 5;
            embryo.NextStage = now + embryo.GrowthTime;
            Dirty(ent);
            return;
        }

        if (!_random.Prob(0.5f))
            return;

        StartBurst(ent, now);
    }

    private void StartBurst(Entity<XenoEmbryoComponent> ent, TimeSpan now)
    {
        var embryo = ent.Comp;
        embryo.Bursting = true;
        embryo.BurstDeadline = now + BurstPollTime;
        var burstSpawner = SpawnAttachedTo(BurstSpawner, Transform(ent).Coordinates);
        EnsureComp<XenoBurstSpawnerComponent>(burstSpawner).Host = ent;
        embryo.BurstSpawner = burstSpawner;
    }

    /// <summary>Призрак взял роль: личинка вырывается из груди носителя, носитель погибает.</summary>
    private void OnLarvaSpawned(Entity<XenoLarvaComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<XenoBurstSpawnerComponent>(args.Spawner, out var spawner) || spawner.Host is not { } host)
            return;

        QueueDel(args.Spawner);
        if (TerminatingOrDeleted(host))
            return;

        _xform.SetCoordinates(ent, Transform(host).Coordinates);
        _xform.AttachToGridOrMap(ent);

        // Без разрыва: личинка выползает, носитель получает 40 урона.
        if (TryComp<XenoEmbryoComponent>(host, out var embryo) && embryo.NoGibBurst)
        {
            RemComp<XenoEmbryoComponent>(host);
            _popup.PopupEntity(Loc.GetString("xeno-burst-wriggle", ("larva", ent.Owner), ("host", host)), ent, PopupType.LargeCaution);
            _damageable.TryChangeDamage(host, new DamageSpecifier { DamageDict = { ["Slash"] = 40 } });
            return;
        }

        RemComp<XenoEmbryoComponent>(host);
        _popup.PopupEntity(Loc.GetString("xeno-burst", ("larva", ent.Owner), ("host", host)), ent, PopupType.LargeCaution);
        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Imperial/Xenomorph/alien_explode.ogg"), ent);
        _gibbing.Gib(host);
    }

    #endregion

    #region Извлечение эмбриона

    /// <summary>
    /// Упрощённая замена хирургии SS13: режущим инструментом по лежащему носителю — 10 секунд, 15 урона.
    /// </summary>
    private void OnEmbryoInteractUsing(Entity<XenoEmbryoComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || args.User == args.Target || !_tool.HasQuality(args.Used, SlicingQuality))
            return;

        args.Handled = true;
        if (!_standing.IsDown(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("xeno-embryo-removal-standing", ("target", ent.Owner)), ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("xeno-embryo-removal-start", ("user", args.User), ("target", ent.Owner)), ent, PopupType.MediumCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, EmbryoRemovalTime, new XenoEmbryoRemovalDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnEmbryoRemoval(Entity<XenoEmbryoComponent> ent, ref XenoEmbryoRemovalDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Slash"] = 15 } }, origin: args.User);

        // body_egg/alien_embryo/on_find: созревший эмбрион с шансом 10 % выползает сам, не разрывая носителя.
        if (ent.Comp.Stage >= 5 && !ent.Comp.Bursting && _random.Prob(0.1f))
        {
            _popup.PopupEntity(Loc.GetString("xeno-embryo-removal-writhes"), ent, args.User, PopupType.LargeCaution);
            ent.Comp.NoGibBurst = true;
            StartBurst(ent, _timing.CurTime);
            return;
        }

        if (ent.Comp.BurstSpawner is { } spawner)
            QueueDel(spawner);

        RemComp<XenoEmbryoComponent>(ent);
        _popup.PopupEntity(Loc.GetString("xeno-embryo-removal-done", ("user", args.User), ("target", ent.Owner)), ent, PopupType.Medium);
    }

    #endregion
}
