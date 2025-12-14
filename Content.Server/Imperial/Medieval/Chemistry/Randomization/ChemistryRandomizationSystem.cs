using Content.Server.GameTicking.Events;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Robust.Server.Player;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;
using System.Linq;
using Robust.Shared.Utility;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Examine;
using Content.Server.MedievalPotionChecker.Components;
using Robust.Shared.Containers;
using Content.Shared.Chemistry.Components;

namespace Content.Server.Imperial.Medieval.ChemistryRandomization;

public sealed partial class ChemistryRandomizationSystem : EntitySystem
{
    [Dependency] private readonly SharedChemistryRandomizationSystem _chemRandom = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IEntitySystemManager _ent = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeNetworkEvent<RequestChemistryRandomizationSeedMessage>(OnRequestSeed);
        SubscribeLocalEvent<MedievalRandomChemistryRecipeComponent, MapInitEvent>(RecipeInit);
        SubscribeLocalEvent<SolutionContainerManagerComponent, ExaminedEvent>(OnExamine);
    }
    public void OnExamine(EntityUid uid, SolutionContainerManagerComponent component, ExaminedEvent args)
    {
        if (!HasComp<MedievalPotionCheckerComponent>(args.Examiner))
            return;
        if (!TryComp<ContainerManagerComponent>(uid, out var contman))
            return;

        if (component.Containers == null)
            return;

        var str = $"{Loc.GetString("imperial-medieval-chemistry-examine")}{Environment.NewLine}";
        var addedsomething = false;
        var addedeffects = new List<string>();
        foreach (var key in component.Containers)
        {
            if (!contman.TryGetContainer($"solution@{key}", out var container))
                continue;
            var solution = EnsureComp<SolutionComponent>(((ContainerSlot)container).ContainedEntity!.Value).Solution;
            foreach (var reagent in solution.Contents)
            {
                var proto = _prototype.Index<ReagentPrototype>(reagent.Reagent.Prototype);
                if (proto.ShowInBook)
                    continue;

                if (proto.Metabolisms == null)
                    continue;

                foreach (var (_, effectentry) in proto.Metabolisms)
                {
                    var entry = effectentry.MakeGuideEntry(_prototype, _ent);
                    var effects = string.Empty;
                    foreach (var effect in entry.EffectDescriptions)
                    {
                        if (addedeffects.Contains(effect))
                            continue;
                        effects = $"{effects}{Environment.NewLine}- {effect}";
                        addedeffects.Add(effect);
                    }
                    str = $"{str}{effects}{Environment.NewLine}";
                    addedsomething = true;
                }
            }
        }
        if (!addedsomething)
            return;
        args.PushMarkup(str);
    }
    private void RecipeInit(EntityUid uid, MedievalRandomChemistryRecipeComponent component, MapInitEvent args)
    {
        if (component.Weights.Count == 0)
            return;
        var type = _random.Pick(component.Weights);
        var reagent = _random.Pick(_chemRandom.GetReagentsFromGroup(type));
        var recipe = SharedChemistryRandomizationSystem.GetReactionsOrNull(reagent);
        if (recipe == null || recipe.Count == 0)
            return; // ???? kinda impossible but who knows
        var str = Loc.GetString("imperial-medieval-recipewritten");
        foreach (var reactant in recipe.First().Reactants)
        {
            str = $"{str}{Environment.NewLine}- {Loc.GetString(_prototype.Index<ReagentPrototype>(reactant.Key).LocalizedName)}";
        }
        _meta.SetEntityDescription(uid, str);
        component.Reagent = reagent;
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        _chemRandom.Seed = _random.Next();

        var ev = new SetChemistryRandomizationSeedMessage(_chemRandom.Seed);
        RaiseNetworkEvent(ev);

        _chemRandom.GeneratePotions();
    }

    private void OnRequestSeed(RequestChemistryRandomizationSeedMessage args)
    {
        if (args.Seed == _chemRandom.Seed)
            return;
        if (!_player.TryGetSessionByUsername(args.Username, out var session))
            return;

        var ev = new SetChemistryRandomizationSeedMessage(_chemRandom.Seed);
        RaiseNetworkEvent(ev, session);
    }
}
