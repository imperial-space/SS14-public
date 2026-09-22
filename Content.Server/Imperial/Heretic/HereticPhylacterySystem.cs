using Content.Server.Popups;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticPhylacterySystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticPhylacteryComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HereticPhylacteryComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HereticPhylacteryComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(EntityUid uid, HereticPhylacteryComponent comp, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != "phylactery")
            return;

        if (!_solution.TryGetSolution(uid, "phylactery", out _, out var sol))
            return;

        UpdateVisuals(uid, sol.Volume, sol.MaxVolume);
    }

    private void OnAfterInteract(EntityUid uid, HereticPhylacteryComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled)
            return;

        var target = args.Target.Value;

        if (!HasComp<MobStateComponent>(target))
            return;

        if (!TryComp<BloodstreamComponent>(target, out var blood))
        {
            _popup.PopupEntity(Loc.GetString("heretic-phylactery-no-blood"), uid, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        TryAbsorbBlood(uid, comp, target, blood, args.User);
        args.Handled = true;
    }

    private void OnMeleeHit(EntityUid uid, HereticPhylacteryComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!TryComp<BloodstreamComponent>(target, out var blood))
                continue;

            TryAbsorbBlood(uid, comp, target, blood, args.User);
            break;
        }
    }

    private void TryAbsorbBlood(EntityUid uid, HereticPhylacteryComponent comp, EntityUid target, BloodstreamComponent blood, EntityUid user)
    {
        if (!_solution.TryGetSolution(uid, "phylactery", out var solEnt, out var sol))
            return;

        var maxVol = sol.MaxVolume;

        if (sol.Volume >= maxVol)
        {
            _popup.PopupEntity(Loc.GetString("heretic-phylactery-full"), uid, user, PopupType.SmallCaution);
            return;
        }

        var draw = FixedPoint2.Min(FixedPoint2.New(comp.BloodPerClick), maxVol - sol.Volume);
        _bloodstream.TryModifyBloodLevel((target, blood), -draw);
        _solution.TryAddReagent(solEnt.Value, "Blood", draw, out _);

        if (!_solution.TryGetSolution(uid, "phylactery", out _, out var updated))
            return;

        UpdateVisuals(uid, updated.Volume, maxVol);
        _popup.PopupEntity(
            Loc.GetString("heretic-phylactery-absorbed", ("amount", (int)updated.Volume.Float()), ("max", (int)maxVol.Float())),
            uid, user, PopupType.Small);
    }

    private void UpdateVisuals(EntityUid uid, FixedPoint2 volume, FixedPoint2 maxVolume)
    {
        HereticPhylacteryFillLevel level;
        if (volume <= FixedPoint2.Zero)
            level = HereticPhylacteryFillLevel.Empty;
        else if (volume < maxVolume)
            level = HereticPhylacteryFillLevel.Half;
        else
            level = HereticPhylacteryFillLevel.Full;

        _appearance.SetData(uid, HereticPhylacteryVisuals.FillLevel, level);
    }
}
