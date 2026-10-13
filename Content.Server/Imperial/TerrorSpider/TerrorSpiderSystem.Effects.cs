using Robust.Shared.Random;
using Content.Server.Atmos.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.TerrorSpider;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Tools.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

public sealed partial class TerrorSpiderSystem
{
    [Dependency] private readonly BlurryVisionSystem _blurry = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    private static readonly ProtoId<ReagentPrototype> BlackToxin = "TerrorBlackToxin";
    private static readonly ProtoId<ReagentPrototype> Saline = "Saline";
    private const string SlicingQuality = "Slicing";

    /// <summary>Сила размытия на время AdjustEyeBlurry.</summary>
    private const float BlurMagnitude = 4f;

    private static readonly TimeSpan EggsRemovalTime = TimeSpan.FromSeconds(10);

    private TimeSpan _nextEffectTick;

    private void InitializeEffects()
    {
        SubscribeLocalEvent<TerrorWebComponent, StartCollideEvent>(OnWebCollide);
        SubscribeLocalEvent<TerrorSpiderGrayComponent, MoveEvent>(OnGrayMove);
        SubscribeLocalEvent<TerrorEyeBlurComponent, GetBlurEvent>(OnGetBlur);
        SubscribeLocalEvent<TerrorEggsInfectionComponent, InteractUsingEvent>(OnEggsInteractUsing);
        SubscribeLocalEvent<TerrorEggsInfectionComponent, TerrorEggsRemovalDoAfterEvent>(OnEggsRemoval);
    }

    /// <summary>can_inject(): плотный костюм (скафандр) не прокусить.</summary>
    private bool CanInject(EntityUid target)
    {
        return !(_inventory.TryGetSlotEntity(target, "outerClothing", out var suit) && HasComp<PressureProtectionComponent>(suit));
    }

    /// <summary>HAS_TRAIT(TRAIT_HANDS_BLOCKED) && HAS_TRAIT(TRAIT_IMMOBILIZED): связанная лежачая жертва.</summary>
    private bool Restrained(EntityUid target)
    {
        return TryComp<CuffableComponent>(target, out var cuffs) && cuffs.CuffedHandCount > 0 && _standing.IsDown(target);
    }

    private bool Poisonable(EntityUid target)
    {
        return HasComp<BloodstreamComponent>(target) && HasComp<BodyComponent>(target);
    }

    #region Особые атаки

    /// <summary>spider_specialattack() разных типов.</summary>
    private void SpecialAttack(Entity<TerrorSpiderComponent> ent, EntityUid target)
    {
        var poisonable = Poisonable(target);
        switch (ent.Comp.Attack)
        {
            case TerrorSpiderAttackType.Green:
                if (!poisonable)
                    break;
                if (HasComp<StunnedComponent>(target) || CanInject(target))
                {
                    AdjustEyeBlurry(target, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(120));
                    _popup.PopupEntity(Loc.GetString("terror-spider-bite-fangs", ("spider", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("terror-spider-bite-blocked", ("spider", ent.Owner), ("target", target)), ent);
                }
                break;

            case TerrorSpiderAttackType.Black:
                if (!poisonable || ToxinAmount(target) >= 100)
                    break;
                _stamina.TakeStaminaDamage(target, 30, source: ent);
                if (CanInject(target) || Restrained(target))
                {
                    InjectBlackToxin(target, 30);
                    _popup.PopupEntity(Loc.GetString("terror-spider-bite-long-fangs", ("spider", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("terror-spider-bite-blocked", ("spider", ent.Owner), ("target", target)), ent);
                }

                // ИИ кусает и отпрыгивает, пока яд действует.
                if (!HasComp<Robust.Shared.Player.ActorComponent>(ent) && (!ent.Comp.Enemies.Contains(target) || ToxinAmount(target) >= 60))
                {
                    JumpAway(ent, target, 3);
                    _popup.PopupEntity(Loc.GetString("terror-spider-jumps-away", ("spider", ent.Owner), ("target", target)), ent);
                }
                break;

            case TerrorSpiderAttackType.White:
                if (!poisonable)
                    break;
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(10), true);
                _stamina.TakeStaminaDamage(target, 30, source: ent);
                if ((CanInject(target) || Restrained(target)) && !HasComp<TerrorEggsInfectionComponent>(target) && HasComp<HumanoidProfileComponent>(target))
                {
                    _popup.PopupEntity(Loc.GetString("terror-spider-bite-long-fangs", ("spider", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
                    EnsureComp<TerrorEggsInfectionComponent>(target);
                    if (!HasComp<Robust.Shared.Player.ActorComponent>(ent))
                        JumpAway(ent, target, 2);
                }
                break;

            case TerrorSpiderAttackType.Prince:
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(10), true);
                _stamina.TakeStaminaDamage(target, 40, source: ent);
                break;
        }
    }

    /// <summary>step_away() / move_away(): ИИ отскакивает от жертвы.</summary>
    private void JumpAway(EntityUid spider, EntityUid from, float distance)
    {
        var dir = _transform.GetWorldPosition(spider) - _transform.GetWorldPosition(from);
        if (dir.LengthSquared() < 0.001f)
            dir = _random.NextAngle().ToVec();
        _throwing.TryThrow(spider, dir.Normalized() * distance, 8f, spider, 0, doSpin: false, playSound: false);
    }

    #endregion

    #region Паутина

    /// <summary>terrorweb/on_atom_entered(): прилип на мгновение.</summary>
    private void OnWebCollide(Entity<TerrorWebComponent> ent, ref StartCollideEvent args)
    {
        var mob = args.OtherEntity;
        if (!HasComp<MobStateComponent>(mob) || HasComp<TerrorSpiderComponent>(mob) || HasComp<TerrorSpiderlingComponent>(mob))
            return;

        _popup.PopupEntity(Loc.GetString("terror-web-stuck", ("web", ent.Owner)), mob, mob, PopupType.LargeCaution);
        _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(8));

        if (!HasComp<BodyComponent>(mob))
            return;

        WebSpecialAbility(ent, mob);
        _webChecks.Add((_timing.CurTime + TimeSpan.FromSeconds(7), ent, mob));
    }

    private readonly List<(TimeSpan At, EntityUid Web, EntityUid Mob)> _webChecks = new();

    /// <summary>web_special_ability().</summary>
    private void WebSpecialAbility(Entity<TerrorWebComponent> ent, EntityUid victim)
    {
        switch (ent.Comp.Ability)
        {
            case TerrorWebAbility.Green:
                AdjustEyeBlurry(victim, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120));
                break;
            case TerrorWebAbility.Black:
                if (ToxinAmount(victim) < 60 && CanInject(victim))
                {
                    _popup.PopupEntity(Loc.GetString("terror-web-slices", ("web", ent.Owner)), victim, victim, PopupType.LargeCaution);
                    InjectBlackToxin(victim, 30);
                }
                break;
            case TerrorWebAbility.White:
                if (!HasComp<TerrorEggsInfectionComponent>(victim) && HasComp<HumanoidProfileComponent>(victim) && CanInject(victim))
                {
                    _popup.PopupEntity(Loc.GetString("terror-web-slices", ("web", ent.Owner)), victim, victim, PopupType.LargeCaution);
                    EnsureComp<TerrorEggsInfectionComponent>(victim);
                }
                break;
        }
    }

    #endregion

    /// <summary>gray/Move(): на паутине серый почти невидим (terror_gray_cloaked).</summary>
    private void OnGrayMove(Entity<TerrorSpiderGrayComponent> ent, ref MoveEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        var cloaked = args.NewPosition.IsValid(EntityManager) && WebAt(args.NewPosition) != null;
        var stage = cloaked ? 1 : 0;
        if (_appearance.TryGetData<int>(ent, TerrorSpiderVisuals.Stage, out var current) && current == stage)
            return;
        _appearance.SetData(ent, TerrorSpiderVisuals.Stage, stage);
    }

    #region Яд чёрного паука

    private void InjectBlackToxin(EntityUid target, float amount)
    {
        var solution = new Solution();
        solution.AddReagent(BlackToxin, FixedPoint2.New(amount));
        if (_bloodstream.TryAddToBloodstream(target, solution))
            EnsureComp<TerrorBlackVenomComponent>(target);
    }

    private float ToxinAmount(EntityUid target)
    {
        return ReagentAmount(target, BlackToxin);
    }

    private float ReagentAmount(EntityUid target, ProtoId<ReagentPrototype> reagent)
    {
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream)
            || !_solutions.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var solution))
            return 0;

        return (float) solution.GetTotalPrototypeQuantity(reagent);
    }

    /// <summary>terror_black_toxin/on_mob_life(): чем больше яда, тем сильнее.</summary>
    private void BlackVenomLife(EntityUid uid)
    {
        var volume = ToxinAmount(uid);
        if (volume <= 0)
        {
            RemCompDeferred<TerrorBlackVenomComponent>(uid);
            return;
        }

        if (volume < 30)
        {
            Damage(uid, "Poison", 1);
        }
        else if (volume < 60)
        {
            Damage(uid, "Poison", 2);
            AdjustEyeBlurry(uid, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }
        else if (volume < 90)
        {
            Damage(uid, "Poison", 4);
            AdjustEyeBlurry(uid, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(6));
            _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(12));
        }
        else
        {
            Damage(uid, "Poison", 8);
            AdjustEyeBlurry(uid, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(6));
            _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(10));
        }
    }

    #endregion

    #region Яйца белого паука

    /// <summary>terror_eggs/on_life().</summary>
    private void EggsLife(Entity<TerrorEggsInfectionComponent> ent)
    {
        if (_mobState.IsDead(ent))
            return;

        var eggs = ent.Comp;
        eggs.CycleNum++;
        eggs.EggProgress += _random.Next(0, 3) + CalcVariableProgress(ent);

        // После первого вылупления жертве нужна помощь, чтобы дойти до медбея.
        if (eggs.EggsHatched >= 1)
            _drunk.TryApplyDrunkenness(ent, TimeSpan.FromSeconds(4.5));

        if (eggs.EggProgress <= eggs.EggProgressPerHatch)
            return;

        eggs.EggProgress -= eggs.EggProgressPerHatch;
        HatchEgg(ent);
    }

    /// <summary>calc_variable_progress(): переедание и капельница ускоряют рост.</summary>
    private float CalcVariableProgress(EntityUid host)
    {
        var extra = 0f;
        if (TryComp<HungerComponent>(host, out var hunger) && hunger.CurrentThreshold == HungerThreshold.Overfed)
            extra += 1;
        if (ReagentAmount(host, Saline) > 1)
            extra += 1;
        return extra;
    }

    /// <summary>hatch_egg(): серый, красный, коричневый, зелёный, зелёный — и заражение окончено.</summary>
    private void HatchEgg(Entity<TerrorEggsInfectionComponent> ent)
    {
        var ling = Spawn(Spiderling, _transform.GetMoverCoordinates(ent));
        var comp = EnsureComp<TerrorSpiderlingComponent>(ling);
        var completed = false;
        comp.GrowAs = ent.Comp.EggsHatched switch
        {
            0 => Gray,
            1 => Red,
            2 => Brown,
            3 => Green,
            _ => Green,
        };
        if (ent.Comp.EggsHatched >= 4)
            completed = true;

        ent.Comp.EggsHatched++;
        _popup.PopupEntity(Loc.GetString("terror-eggs-hatch"), ent, ent, PopupType.LargeCaution);
        _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(20));
        if (completed)
            RemCompDeferred<TerrorEggsInfectionComponent>(ent);
    }

    /// <summary>Яйца извлекают острым инструментом, пока носитель лежит.</summary>
    private void OnEggsInteractUsing(Entity<TerrorEggsInfectionComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || args.User == args.Target || !_tool.HasQuality(args.Used, SlicingQuality))
            return;

        args.Handled = true;
        if (!_standing.IsDown(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("terror-eggs-removal-standing", ("target", ent.Owner)), ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("terror-eggs-removal-start", ("user", args.User), ("target", ent.Owner)), ent, PopupType.MediumCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, EggsRemovalTime, new TerrorEggsRemovalDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnEggsRemoval(Entity<TerrorEggsInfectionComponent> ent, ref TerrorEggsRemovalDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        Damage(ent, "Slash", 15, args.User);
        _popup.PopupEntity(Loc.GetString("terror-eggs-removed", ("user", args.User), ("target", ent.Owner)), ent, PopupType.Medium);
        RemCompDeferred<TerrorEggsInfectionComponent>(ent);
    }

    #endregion

    #region Размытие

    /// <summary>AdjustEyeBlurry(amount, 0, max).</summary>
    private void AdjustEyeBlurry(EntityUid uid, TimeSpan amount, TimeSpan max)
    {
        var now = _timing.CurTime;
        var blur = EnsureComp<TerrorEyeBlurComponent>(uid);
        var start = blur.Until > now ? blur.Until : now;
        var until = start + amount;
        if (until > now + max)
            until = now + max;
        blur.Until = until;
        _blurry.UpdateBlurMagnitude(uid);
    }

    private void OnGetBlur(EntityUid uid, TerrorEyeBlurComponent comp, GetBlurEvent args)
    {
        if (comp.Until > _timing.CurTime)
            args.Blur += BlurMagnitude;
    }

    #endregion

    private void UpdateEffects(TimeSpan now)
    {
        for (var i = _webChecks.Count - 1; i >= 0; i--)
        {
            var (at, web, mob) = _webChecks[i];
            if (now < at)
                continue;

            _webChecks.RemoveAt(i);
            // after_carbon_crossed(): если жертва всё ещё в паутине — паутина рвётся.
            if (Exists(web) && Exists(mob) && InRange(web, mob, 0.5f))
                QueueDel(web);
        }

        var blurs = EntityQueryEnumerator<TerrorEyeBlurComponent>();
        while (blurs.MoveNext(out var uid, out var blur))
        {
            if (now < blur.Until)
                continue;
            RemComp<TerrorEyeBlurComponent>(uid);
            _blurry.UpdateBlurMagnitude(uid);
        }

        if (now < _nextEffectTick)
            return;

        _nextEffectTick = now + LifeInterval;

        var venom = EntityQueryEnumerator<TerrorBlackVenomComponent>();
        while (venom.MoveNext(out var uid, out _))
        {
            BlackVenomLife(uid);
        }

        var eggs = EntityQueryEnumerator<TerrorEggsInfectionComponent>();
        while (eggs.MoveNext(out var uid, out var infection))
        {
            EggsLife((uid, infection));
        }
    }
}
