using Content.Server.Popups;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Tools.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// SS13-style rust tile effects:
/// - All heretics on rust: multi-type healing + stamina recovery
/// - Non-heretics on rust: silicons take blunt damage, organics vomit and lose blood chemicals
/// - Lit welder on rust: 1 second DoAfter then rust is removed
/// </summary>
public sealed class HereticRustEffectsSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem    _lookup    = default!;
    [Dependency] private readonly SharedTransformSystem _xform     = default!;
    [Dependency] private readonly DamageableSystem      _damage    = default!;
    [Dependency] private readonly MobStateSystem        _mobs      = default!;
    [Dependency] private readonly VomitSystem           _vomit     = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedStaminaSystem   _stamina   = default!;
    [Dependency] private readonly SharedDoAfterSystem   _doAfter   = default!;
    [Dependency] private readonly PopupSystem           _popup     = default!;

    // Corruption tick: every 2 seconds (matches SS13 rust_corruption)
    private const float CorruptionInterval = 2f;
    // Healing tick: every 1 second (matches SS13 rust_healing 3/s)
    private const float HealInterval = 1f;

    private float _corruptionAccum;
    private float _healAccum;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRustOverlayComponent, InteractUsingEvent>(OnWelderInteract);
        SubscribeLocalEvent<HereticRustOverlayComponent, HereticRustWeldDoAfterEvent>(OnWeldDoAfter);
    }

    private void OnWelderInteract(EntityUid uid, HereticRustOverlayComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(args.Used, out WelderComponent? _))
            return;

        if (!TryComp(args.Used, out ItemToggleComponent? toggle) || !toggle.Activated)
            return;

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 1f,
            new HereticRustWeldDoAfterEvent(),
            uid,
            target: uid,
            used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("heretic-rust-weld-start"), args.User, args.User);
    }

    private void OnWeldDoAfter(EntityUid uid, HereticRustOverlayComponent comp, HereticRustWeldDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        QueueDel(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _corruptionAccum += frameTime;
        _healAccum += frameTime;

        var doCorruption = _corruptionAccum >= CorruptionInterval;
        var doHeal = _healAccum >= HealInterval;

        if (!doCorruption && !doHeal)
            return;

        if (doCorruption)
            _corruptionAccum = 0f;
        if (doHeal)
            _healAccum = 0f;

        // Iterate every living mob, check if on rust
        var query = EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var uid, out var mobState))
        {
            if (!_mobs.IsAlive(uid, mobState))
                continue;

            var onRust = _lookup.GetEntitiesInRange<HereticRustOverlayComponent>(
                _xform.GetMapCoordinates(uid), 0.6f).Count > 0;

            if (!onRust)
                continue;

            if (HasComp<HereticComponent>(uid))
            {
                // SS13 rust_healing: 3 HP/s each damage type + 10 stamina/s
                if (doHeal)
                    ApplyRustHealing(uid);
            }
            else
            {
                // SS13 rust_corruption
                if (doCorruption)
                    ApplyRustCorruption(uid);
            }
        }
    }

    private void ApplyRustHealing(EntityUid uid)
    {
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"]        = FixedPoint2.New(-3);
        heal.DamageDict["Heat"]         = FixedPoint2.New(-3);
        heal.DamageDict["Poison"]       = FixedPoint2.New(-3);
        heal.DamageDict["Asphyxiation"] = FixedPoint2.New(-3);
        _damage.TryChangeDamage(uid, heal, ignoreResistances: true);

        _stamina.TakeStaminaDamage(uid, -10f, visual: false);
    }

    private void ApplyRustCorruption(EntityUid uid)
    {
        if (HasComp<BorgChassisComponent>(uid))
        {
            // Silicons: 20 brute per 2 seconds
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Blunt"] = FixedPoint2.New(20);
            _damage.TryChangeDamage(uid, dmg, ignoreResistances: true);
        }
        else if (TryComp<BloodstreamComponent>(uid, out var bloodstream))
        {
            // Organics: forced vomit + flush 1.5 units of blood chemicals
            _vomit.Vomit(uid, force: true);
            _bloodstream.FlushChemicals((uid, bloodstream), FixedPoint2.New(1.5f));
        }
    }
}
