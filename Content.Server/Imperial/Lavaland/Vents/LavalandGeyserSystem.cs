using System.Linq;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.Imperial.Lavaland.OrePoints;
using Content.Shared.Access.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Interaction;
using Content.Shared.Mining.Components;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Vents;

/// <summary>Перенос structure/geyser из SS13: восполнение реагента и обнаружение сканером.</summary>
public sealed class LavalandGeyserSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandGeyserComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LavalandGeyserComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<LavalandGeyserComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<LavalandGeyserComponent> ent, ref MapInitEvent args)
    {
        // geyser/random: get_random_reagent_id()
        if (ent.Comp.Reagent == null)
        {
            var reagents = _proto.EnumeratePrototypes<ReagentPrototype>().ToList();
            ent.Comp.Reagent = _random.Pick(reagents).ID;
        }

        if (ent.Comp.Reagent is not { } reagentId)
            return;

        var reagent = _proto.Index(reagentId);
        if (_solutions.EnsureSolution(ent.Owner, ent.Comp.Solution, out _, ent.Comp.MaxVolume) &&
            _solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out _))
        {
            _solutions.TryAddReagent(soln.Value, reagent.ID, ent.Comp.MaxVolume);
        }

        _appearance.SetData(ent, MegafaunaVisuals.OverlayColor, reagent.SubstanceColor);
        ent.Comp.NextRefill = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Interval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandGeyserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextRefill || comp.Reagent == null)
                continue;
            comp.NextRefill = now + TimeSpan.FromSeconds(comp.Interval);

            if (!_solutions.TryGetSolution(uid, comp.Solution, out var soln, out var solution) ||
                solution.Volume >= solution.MaxVolume)
                continue;

            _solutions.TryAddReagent(soln.Value, comp.Reagent.Value.Id, comp.Potency);
        }
    }

    private void OnExamined(Entity<LavalandGeyserComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Discovered)
            args.PushMarkup(Loc.GetString("lavaland-geyser-undiscovered"));
    }

    /// <summary>attackby(mining_scanner): открыть гейзер, получить очки.</summary>
    private void OnInteractUsing(Entity<LavalandGeyserComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // attackby: любой другой предмет — звук промышленного сканирования, дальше обычная атака.
        if (!HasComp<MiningScannerComponent>(args.Used))
        {
            _ai.PlaySound(ent.Comp.ScanSound, Transform(ent).Coordinates, -8f);
            return;
        }

        args.Handled = true;

        if (ent.Comp.Discovered)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-geyser-already-discovered"), ent, args.User);
            return;
        }

        ent.Comp.Discovered = true;
        _popup.PopupEntity(Loc.GetString("lavaland-geyser-discovered"), ent, args.User);
        _ai.PlaySound(ent.Comp.DiscoverSound, Transform(ent).Coordinates, -5f);
        if (ent.Comp.DiscoveryMessage is { } msg)
            _popup.PopupEntity(Loc.GetString(msg), ent, args.User, PopupType.Medium);

        if (ent.Comp.TrueName is { } trueName)
            _meta.SetEntityName(ent, Loc.GetString(trueName));
        else if (ent.Comp.Reagent is { } reagent)
            _meta.SetEntityName(ent, Loc.GetString("lavaland-geyser-random-name", ("reagent", _proto.Index(reagent).LocalizedName)));

        // AddComponent(/datum/component/gps, true_name)
        EnsureComp<Gps.ImperialGpsComponent>(ent).Tag = MetaData(ent).EntityName;
        AwardPoints(args.User, ent.Comp.PointValue);
    }

    /// <summary>registered_account.mining_points += value.</summary>
    public bool AwardPoints(EntityUid user, int points)
    {
        if (points <= 0 || !_idCard.TryFindIdCard(user, out var idCard))
            return false;

        EnsureComp<OrePointsAccountComponent>(idCard.Owner).Points += points;
        _popup.PopupEntity(Loc.GetString("lavaland-points-awarded", ("points", points)), user, user);
        return true;
    }
}
