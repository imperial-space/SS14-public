using System.Linq;
using System.Text;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Research.Items;
using Content.Shared.Interaction;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Metabolism;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Pinpointer;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research.Items;

/// <summary>
/// Медицинские предметы из исследований TG: сканеры, инструменты, капельница, дисплеи жизненных показателей,
/// рефиллер медипенов, хроматограф, пинпоинтер экипажа, дым-машина и стазисный мешок.
/// </summary>
public sealed class ImperialTgMedicalItemsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly MetabolizerSystem _metabolizer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialMedScannerComponent, AfterInteractEvent>(OnScannerInteract);
        SubscribeLocalEvent<ImperialMedScannerComponent, ImperialMedScanDoAfterEvent>(OnScannerDoAfter);

        SubscribeLocalEvent<ImperialMedToolComponent, AfterInteractEvent>(OnToolInteract);
        SubscribeLocalEvent<ImperialMedToolComponent, ImperialMedToolDoAfterEvent>(OnToolDoAfter);

        SubscribeLocalEvent<ImperialIvBagComponent, AfterInteractEvent>(OnIvInteract);
        SubscribeLocalEvent<ImperialIvBagComponent, ExaminedEvent>(OnIvExamined);

        SubscribeLocalEvent<ImperialVitalsDisplayComponent, ExaminedEvent>(OnVitalsExamined);
        SubscribeLocalEvent<ImperialVitalsDisplayComponent, ActivateInWorldEvent>(OnVitalsActivate);

        SubscribeLocalEvent<ImperialMedipenRefillerComponent, InteractUsingEvent>(OnRefillerInteract);
        SubscribeLocalEvent<ImperialMedipenRefillerComponent, ImperialMedipenRefillDoAfterEvent>(OnRefillerDoAfter);

        SubscribeLocalEvent<ImperialChromatographComponent, GetVerbsEvent<Verb>>(OnChromatographVerbs);

        SubscribeLocalEvent<ImperialCrewPinpointerComponent, GetVerbsEvent<Verb>>(OnPinpointerVerbs);

        SubscribeLocalEvent<ImperialSmokeMachineComponent, GetVerbsEvent<AlternativeVerb>>(OnSmokeVerbs);

        SubscribeLocalEvent<ImperialStasisBagComponent, StorageAfterCloseEvent>(OnStasisClosed);
        SubscribeLocalEvent<ImperialStasisBagComponent, StorageAfterOpenEvent>(OnStasisOpened);
        SubscribeLocalEvent<ImperialInStasisBagComponent, GetMetabolicMultiplierEvent>(OnStasisMultiplier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var ivs = EntityQueryEnumerator<ImperialIvBagComponent>();
        while (ivs.MoveNext(out var uid, out var iv))
        {
            if (iv.Patient is not { } patient || now < iv.NextDrip)
                continue;

            iv.NextDrip = now + TimeSpan.FromSeconds(1);
            Drip(uid, iv, patient);
        }

        var smokes = EntityQueryEnumerator<ImperialSmokeMachineComponent>();
        while (smokes.MoveNext(out var uid, out var smoke))
        {
            if (!smoke.Enabled || now < smoke.NextPuff)
                continue;

            smoke.NextPuff = now + TimeSpan.FromSeconds(smoke.Interval);
            Puff(uid, smoke);
        }
    }

    #region Отчёты

    /// <summary>
    /// Текст состояния пациента: состояние, урон по группам, кровь, температура.
    /// </summary>
    private string HealthReport(EntityUid target, bool detailed)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("imperial-med-report-name", ("target", target)));

        if (TryComp<MobStateComponent>(target, out var state))
        {
            MobState current = state.CurrentState;
            sb.AppendLine(Loc.GetString("imperial-med-report-state", ("state", Loc.GetString($"imperial-med-state-{current.ToString().ToLowerInvariant()}"))));
        }

        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            sb.AppendLine(Loc.GetString("imperial-med-report-total", ("damage", _damageable.GetTotalDamage((target, damageable)))));
            var damage = _damageable.GetAllDamage((target, damageable));
            foreach (var (type, amount) in damage.DamageDict.OrderByDescending(p => p.Value))
            {
                if (amount <= 0)
                    continue;
                var name = _proto.TryIndex<DamageTypePrototype>(type, out var proto) ? proto.LocalizedName : type.ToString();
                if (detailed || amount >= 5)
                    sb.AppendLine($"  {name}: {amount}");
            }
        }

        if (TryComp<BloodstreamComponent>(target, out var blood))
            sb.AppendLine(Loc.GetString("imperial-med-report-blood", ("level", $"{_bloodstream.GetBloodLevel((target, blood)) * 100:0}")));

        if (detailed && TryComp<TemperatureComponent>(target, out var temp))
            sb.AppendLine(Loc.GetString("imperial-med-report-temp", ("temp", $"{temp.CurrentTemperature - 273.15f:0.#}")));

        return sb.ToString().TrimEnd();
    }

    private IEnumerable<(string Name, FixedPoint2 Amount)> ForeignBloodReagents(EntityUid target, BloodstreamComponent blood)
    {
        if (!_solution.TryGetSolution(target, blood.BloodSolutionName, out _, out var solution))
            yield break;

        var reference = blood.BloodReferenceSolution;
        foreach (var reagent in solution.Contents)
        {
            if (reference.ContainsPrototype(reagent.Reagent.Prototype))
                continue;

            var name = _proto.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var proto) ? proto.LocalizedName : reagent.Reagent.Prototype;
            yield return (name, reagent.Quantity);
        }
    }

    #endregion

    #region Сканеры

    private void OnScannerInteract(Entity<ImperialMedScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        var mode = ent.Comp.Mode;
        if (mode == ImperialMedScanMode.Chemical ? !HasComp<Content.Shared.Chemistry.Components.SolutionManager.SolutionContainerManagerComponent>(target) : !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new ImperialMedScanDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnScannerDoAfter(Entity<ImperialMedScannerComponent> ent, ref ImperialMedScanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        var user = args.User;

        switch (ent.Comp.Mode)
        {
            case ImperialMedScanMode.Autopsy:
            {
                if (!TryComp<MobStateComponent>(target, out var state) || state.CurrentState != MobState.Dead)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-autopsy-alive"), user, user);
                    return;
                }

                var text = Loc.GetString("imperial-autopsy-header") + "\n" + HealthReport(target, true);
                if (TryComp<BloodstreamComponent>(target, out var blood))
                {
                    var chems = ForeignBloodReagents(target, blood).ToList();
                    if (chems.Count > 0)
                        text += "\n" + Loc.GetString("imperial-med-report-chems") + "\n" + string.Join("\n", chems.Select(c => $"  {c.Name}: {c.Amount}u"));
                }

                var paper = Spawn("Paper", _transform.GetMapCoordinates(user));
                if (TryComp<PaperComponent>(paper, out var paperComp))
                    _paper.SetContent((paper, paperComp), text);
                _hands.TryPickupAnyHand(user, paper);
                _popup.PopupEntity(Loc.GetString("imperial-autopsy-done"), user, user);
                break;
            }
            case ImperialMedScanMode.Chemical:
            {
                var lines = new List<string>();
                foreach (var (name, _) in _solution.EnumerateSolutions(target))
                {
                    if (!_solution.TryGetSolution(target, name, out _, out var sol))
                        continue;

                    lines.Add(Loc.GetString("imperial-ph-solution", ("volume", sol.Volume), ("max", sol.MaxVolume), ("temp", $"{sol.Temperature - 273.15f:0}")));
                    foreach (var reagent in sol.Contents)
                    {
                        var rname = _proto.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var proto) ? proto.LocalizedName : reagent.Reagent.Prototype;
                        lines.Add($"  {rname}: {reagent.Quantity}u");
                    }
                }

                _popup.PopupEntity(lines.Count == 0 ? Loc.GetString("imperial-ph-empty") : string.Join("\n", lines), user, user);
                break;
            }
            case ImperialMedScanMode.Blood:
            {
                if (!TryComp<BloodstreamComponent>(target, out var blood))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-blood-none"), user, user);
                    return;
                }

                var text = Loc.GetString("imperial-med-report-blood", ("level", $"{_bloodstream.GetBloodLevel((target, blood)) * 100:0}"));
                var chems = ForeignBloodReagents(target, blood).ToList();
                text += "\n" + (chems.Count == 0
                    ? Loc.GetString("imperial-blood-clean")
                    : Loc.GetString("imperial-med-report-chems") + "\n" + string.Join("\n", chems.Select(c => $"  {c.Name}: {c.Amount}u")));
                _popup.PopupEntity(text, user, user);
                break;
            }
            case ImperialMedScanMode.Pupils:
            case ImperialMedScanMode.Reflex:
            {
                var state = CompOrNull<MobStateComponent>(target)?.CurrentState ?? MobState.Alive;
                var key = ent.Comp.Mode == ImperialMedScanMode.Pupils ? "imperial-penlight" : "imperial-reflex";
                _popup.PopupEntity(Loc.GetString($"{key}-{state.ToString().ToLowerInvariant()}", ("target", target)), user, user);
                break;
            }
        }
    }

    #endregion

    #region Инструменты

    private void OnToolInteract(Entity<ImperialMedToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new ImperialMedToolDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnToolDoAfter(Entity<ImperialMedToolComponent> ent, ref ImperialMedToolDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (ent.Comp.FilterAmount > 0 && TryComp<BloodstreamComponent>(target, out var blood) &&
            _solution.TryGetSolution(target, blood.BloodSolutionName, out var soln, out var solution))
        {
            var left = ent.Comp.FilterAmount;
            var reference = blood.BloodReferenceSolution;
            foreach (var reagent in solution.Contents.ToList())
            {
                if (left <= 0)
                    break;
                if (reference.ContainsPrototype(reagent.Reagent.Prototype))
                    continue;

                left -= _solution.RemoveReagent(soln.Value, reagent.Reagent, FixedPoint2.Min(left, reagent.Quantity));
            }

            _popup.PopupEntity(Loc.GetString("imperial-blood-filter-done", ("target", target)), target, args.User);
        }

        if (ent.Comp.HealBlunt > 0)
        {
            var heal = new DamageSpecifier();
            heal.DamageDict["Blunt"] = -ent.Comp.HealBlunt;
            _damageable.TryChangeDamage(target, heal, true, false);
            _popup.PopupEntity(Loc.GetString("imperial-bonesetter-done", ("target", target)), target, args.User);
        }

        args.Repeat = ent.Comp.HealBlunt > 0 && _damageable.GetAllDamage(target).DamageDict.TryGetValue("Blunt", out var blunt) && blunt > 0;
    }

    #endregion

    #region Капельница

    private void OnIvInteract(Entity<ImperialIvBagComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<BloodstreamComponent>(target))
            return;

        args.Handled = true;
        if (ent.Comp.Patient == target)
        {
            ent.Comp.Patient = null;
            _popup.PopupEntity(Loc.GetString("imperial-iv-detached", ("target", target)), target, args.User);
            return;
        }

        ent.Comp.Patient = target;
        _popup.PopupEntity(Loc.GetString("imperial-iv-attached", ("target", target)), target, args.User);
    }

    private void Drip(EntityUid uid, ImperialIvBagComponent iv, EntityUid patient)
    {
        var from = _transform.GetMapCoordinates(uid);
        var to = _transform.GetMapCoordinates(patient);
        if (TerminatingOrDeleted(patient) || from.MapId != to.MapId || (from.Position - to.Position).Length() > iv.MaxDistance)
        {
            iv.Patient = null;
            _popup.PopupEntity(Loc.GetString("imperial-iv-ripped"), patient);
            return;
        }

        if (!_solution.TryGetSolution(uid, iv.Solution, out var soln, out var solution) || solution.Volume <= 0)
            return;

        var drip = _solution.SplitSolution(soln.Value, iv.Rate);
        _bloodstream.TryAddToBloodstream(patient, drip);
    }

    private void OnIvExamined(Entity<ImperialIvBagComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Patient is { } patient)
            args.PushMarkup(Loc.GetString("imperial-iv-examine", ("target", patient)));
    }

    #endregion

    #region Дисплеи

    private EntityUid? FindPatient(EntityUid display, float range)
    {
        foreach (var strap in _lookup.GetEntitiesInRange<StrapComponent>(_transform.GetMapCoordinates(display), range))
        {
            foreach (var buckled in strap.Comp.BuckledEntities)
            {
                if (HasComp<MobStateComponent>(buckled))
                    return buckled;
            }
        }

        return null;
    }

    private void OnVitalsExamined(Entity<ImperialVitalsDisplayComponent> ent, ref ExaminedEvent args)
    {
        var patient = FindPatient(ent, ent.Comp.Range);
        args.PushMarkup(patient == null ? Loc.GetString("imperial-vitals-no-patient") : HealthReport(patient.Value, ent.Comp.Advanced));
    }

    private void OnVitalsActivate(Entity<ImperialVitalsDisplayComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var patient = FindPatient(ent, ent.Comp.Range);
        _popup.PopupEntity(patient == null ? Loc.GetString("imperial-vitals-no-patient") : HealthReport(patient.Value, ent.Comp.Advanced), args.User, args.User);
    }

    #endregion

    #region Рефиллер медипенов

    private void OnRefillerInteract(Entity<ImperialMedipenRefillerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsMedipen(args.Used))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.RefillTime,
            new ImperialMedipenRefillDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private bool IsMedipen(EntityUid uid)
    {
        var proto = MetaData(uid).EntityPrototype?.ID;
        return proto != null && proto.Contains("Medipen") && _solution.TryGetSolution(uid, "pen", out _, out _);
    }

    private void OnRefillerDoAfter(Entity<ImperialMedipenRefillerComponent> ent, ref ImperialMedipenRefillDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } pen)
            return;

        args.Handled = true;
        var protoId = MetaData(pen).EntityPrototype?.ID;
        if (protoId == null || !_proto.TryIndex<EntityPrototype>(protoId, out var proto) ||
            !proto.TryGetComponent<Content.Shared.Chemistry.Components.SolutionManager.SolutionContainerManagerComponent>(out var manager, EntityManager.ComponentFactory) ||
            manager.Solutions is not { } protoSolutions || !protoSolutions.TryGetValue("pen", out var original) ||
            !_solution.TryGetSolution(pen, "pen", out var penSoln, out var penSolution) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var tankSoln, out var tank))
            return;

        // Пополняем исходными реагентами медипена из бака машины.
        var refilled = FixedPoint2.Zero;
        foreach (var reagent in original.Contents)
        {
            var have = penSolution.GetReagentQuantity(reagent.Reagent);
            var need = reagent.Quantity - have;
            if (need <= 0)
                continue;

            var avail = tank.GetReagentQuantity(reagent.Reagent);
            var take = FixedPoint2.Min(need, avail);
            if (take <= 0)
                continue;

            _solution.RemoveReagent(tankSoln.Value, reagent.Reagent, take);
            _solution.TryAddReagent(penSoln.Value, reagent.Reagent, take, out _);
            refilled += take;
        }

        _popup.PopupEntity(Loc.GetString(refilled > 0 ? "imperial-refiller-done" : "imperial-refiller-no-reagents"), ent, args.User);
    }

    #endregion

    #region Хроматограф

    private void OnChromatographVerbs(Entity<ImperialChromatographComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var input = _slots.GetItemOrNull(ent, ent.Comp.InputSlot);
        var output = _slots.GetItemOrNull(ent, ent.Comp.OutputSlot);
        if (input == null || output == null || !_power.IsPowered(ent.Owner))
            return;

        Entity<SolutionComponent>? inSoln = null;
        Solution? inSolution = null;
        foreach (var (name, _) in _solution.EnumerateSolutions(input.Value))
        {
            if (_solution.TryGetSolution(input.Value, name, out inSoln, out inSolution))
                break;
        }

        if (inSoln == null || inSolution == null)
            return;

        var user = args.User;
        foreach (var reagent in inSolution.Contents.ToList())
        {
            var r = reagent;
            var rname = _proto.TryIndex<ReagentPrototype>(r.Reagent.Prototype, out var proto) ? proto.LocalizedName : r.Reagent.Prototype;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("imperial-hplc-verb", ("reagent", rname), ("amount", r.Quantity)),
                Category = VerbCategory.Eject,
                Act = () => Separate(ent, user, input.Value, output.Value, r.Reagent),
            });
        }
    }

    private void Separate(Entity<ImperialChromatographComponent> ent, EntityUid user, EntityUid input, EntityUid output, ReagentId reagent)
    {
        Entity<SolutionComponent>? inSoln = null, outSoln = null;
        Solution? inSol = null, outSol = null;
        foreach (var (name, _) in _solution.EnumerateSolutions(input))
            if (_solution.TryGetSolution(input, name, out inSoln, out inSol))
                break;
        foreach (var (name, _) in _solution.EnumerateSolutions(output))
            if (_solution.TryGetSolution(output, name, out outSoln, out outSol))
                break;

        if (inSoln == null || outSoln == null || inSol == null || outSol == null)
            return;

        var amount = inSol.GetReagentQuantity(reagent);
        var space = outSol.AvailableVolume;
        var move = FixedPoint2.Min(amount, space);
        if (move <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-hplc-full"), ent, user);
            return;
        }

        _solution.RemoveReagent(inSoln.Value, reagent, move);
        _solution.TryAddReagent(outSoln.Value, reagent, move, out _);
        _popup.PopupEntity(Loc.GetString("imperial-hplc-done", ("amount", move)), ent, user);
    }

    #endregion

    #region Пинпоинтер экипажа

    private void OnPinpointerVerbs(Entity<ImperialCrewPinpointerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<PinpointerComponent>(ent, out var pinpointer))
            return;

        var query = EntityQueryEnumerator<SuitSensorComponent>();
        while (query.MoveNext(out _, out var sensor))
        {
            if (sensor.Mode != SuitSensorMode.SensorCords || sensor.User is not { } wearer || TerminatingOrDeleted(wearer))
                continue;

            var target = wearer;
            args.Verbs.Add(new Verb
            {
                Text = Name(target),
                Category = VerbCategory.SelectType,
                Act = () =>
                {
                    _pinpointer.SetTarget((ent, pinpointer), target);
                    _pinpointer.SetActive((ent, pinpointer), true);
                },
            });
        }
    }

    #endregion

    #region Дым-машина

    private void OnSmokeVerbs(Entity<ImperialSmokeMachineComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Enabled ? "imperial-smoke-off" : "imperial-smoke-on"),
            Act = () =>
            {
                ent.Comp.Enabled = !ent.Comp.Enabled;
                _popup.PopupEntity(Loc.GetString(ent.Comp.Enabled ? "imperial-smoke-started" : "imperial-smoke-stopped"), ent, user);
            },
        });
    }

    private void Puff(EntityUid uid, ImperialSmokeMachineComponent smoke)
    {
        if (!_power.IsPowered(uid) ||
            !_solution.TryGetSolution(uid, smoke.Solution, out var soln, out var solution) || solution.Volume <= 0)
            return;

        var portion = _solution.SplitSolution(soln.Value, smoke.PerPuff);
        var cloud = Spawn("Smoke", _transform.GetMapCoordinates(uid));
        _smoke.StartSmoke(cloud, portion, smoke.Interval * 2, smoke.Spread);
    }

    #endregion

    #region Стазисный мешок

    private void OnStasisClosed(Entity<ImperialStasisBagComponent> ent, ref StorageAfterCloseEvent args)
    {
        if (!TryComp<EntityStorageComponent>(ent, out var storage))
            return;

        foreach (var inside in storage.Contents.ContainedEntities)
        {
            if (!HasComp<MobStateComponent>(inside))
                continue;

            EnsureComp<ImperialInStasisBagComponent>(inside).Multiplier = ent.Comp.Multiplier;
            _metabolizer.UpdateMetabolicMultiplier(inside);
        }
    }

    private void OnStasisOpened(Entity<ImperialStasisBagComponent> ent, ref StorageAfterOpenEvent args)
    {
        var query = EntityQueryEnumerator<ImperialInStasisBagComponent>();
        var toClear = new List<EntityUid>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_container.IsEntityInContainer(uid))
                toClear.Add(uid);
        }

        foreach (var uid in toClear)
        {
            RemComp<ImperialInStasisBagComponent>(uid);
            _metabolizer.UpdateMetabolicMultiplier(uid);
        }
    }

    private void OnStasisMultiplier(Entity<ImperialInStasisBagComponent> ent, ref GetMetabolicMultiplierEvent args)
    {
        args.Multiplier *= ent.Comp.Multiplier;
    }

    [Dependency] private readonly SharedContainerSystem _container = default!;

    #endregion
}
