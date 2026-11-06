using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Kitchen;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Kitchen;

/// <summary>
/// Автомат мороженого TG: выбор вкуса и выдача рожков, шарик накладывается ударом рожком по автомату.
/// Каждый ингредиент вкуса или рожка тратится по 1 ед. (CONE_REAGENT_NEEDED).
/// </summary>
public sealed class ImperialIcecreamSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ImperialFoodQualitySystem _quality = default!;

    /// <summary>EXTRA_MAX_VOLUME_PER_SCOOP.</summary>
    private const int ExtraVolumePerScoop = 20;

    private static readonly VerbCategory FlavourCategory = new("imperial-icecream-verb-flavour", null);
    private static readonly VerbCategory ConesCategory = new("imperial-icecream-verb-cone", null);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialIcecreamVatComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ImperialIcecreamVatComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<ImperialIcecreamVatComponent, InteractUsingEvent>(OnInteractUsing);
    }

    /// <summary>attack_hand_secondary: переключение режима.</summary>
    private void OnAltVerbs(Entity<ImperialIcecreamVatComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.ConeMode ? "imperial-icecream-mode-scoops" : "imperial-icecream-mode-cones"),
            Act = () =>
            {
                ent.Comp.ConeMode = !ent.Comp.ConeMode;
                _popup.PopupEntity(Loc.GetString(ent.Comp.ConeMode ? "imperial-icecream-now-cones" : "imperial-icecream-now-scoops"), ent, user);
            },
        });
    }

    /// <summary>Радиальное меню TG: вкусы или рожки.</summary>
    private void OnVerbs(Entity<ImperialIcecreamVatComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (ent.Comp.ConeMode)
        {
            foreach (var cone in ent.Comp.Cones)
            {
                args.Verbs.Add(new Verb
                {
                    Text = _proto.Index(cone).Name,
                    Category = ConesCategory,
                    Act = () => MakeCone(ent, user, cone),
                });
            }

            return;
        }

        foreach (var flavour in _proto.EnumeratePrototypes<ImperialIcecreamFlavourPrototype>())
        {
            var id = flavour.ID;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString(flavour.Name),
                Category = FlavourCategory,
                Act = () =>
                {
                    ent.Comp.Flavour = id;
                    _popup.PopupEntity(Loc.GetString("imperial-icecream-selected", ("flavour", Loc.GetString(flavour.Name))), ent, user);
                },
            });
        }
    }

    private bool HasIngredients(EntityUid vat, string solution, IEnumerable<ProtoId<Shared.Chemistry.Reagent.ReagentPrototype>> ingredients)
    {
        if (!_solutions.TryGetSolution(vat, solution, out _, out var sol))
            return false;

        foreach (var reagent in ingredients)
        {
            if (sol.GetTotalPrototypeQuantity(reagent) < 1)
                return false;
        }

        return true;
    }

    private void UseIngredients(EntityUid vat, string solution, IEnumerable<ProtoId<Shared.Chemistry.Reagent.ReagentPrototype>> ingredients)
    {
        if (!_solutions.TryGetSolution(vat, solution, out var solEnt, out _))
            return;

        foreach (var reagent in ingredients)
            _solutions.RemoveReagent(solEnt.Value, reagent, 1);
    }

    /// <summary>make_cone.</summary>
    private void MakeCone(Entity<ImperialIcecreamVatComponent> ent, EntityUid user, EntProtoId cone)
    {
        if (!_proto.Index(cone).TryGetComponent<ImperialIcecreamConeComponent>(out var coneComp, _factory))
            return;

        if (!HasIngredients(ent, ent.Comp.Solution, coneComp.Ingredients))
        {
            _popup.PopupEntity(Loc.GetString("imperial-icecream-no-ingredients"), ent, user);
            return;
        }

        UseIngredients(ent, ent.Comp.Solution, coneComp.Ingredients);
        var made = Spawn(cone, Transform(ent).Coordinates);
        _hands.PickupOrDrop(user, made);
        _popup.PopupEntity(Loc.GetString("imperial-icecream-cone-made", ("cone", made)), ent);
    }

    /// <summary>add_flavor_to_cone: шарик выбранного вкуса в рожок.</summary>
    private void OnInteractUsing(Entity<ImperialIcecreamVatComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ImperialIcecreamConeComponent>(args.Used, out var cone))
            return;

        args.Handled = true;
        if (cone.Scoops >= cone.MaxScoops)
        {
            _popup.PopupEntity(Loc.GetString("imperial-icecream-full"), ent, args.User);
            return;
        }

        if (!_proto.TryIndex(ent.Comp.Flavour, out var flavour))
            return;

        if (!HasIngredients(ent, ent.Comp.Solution, flavour.Ingredients))
        {
            _popup.PopupEntity(Loc.GetString("imperial-icecream-no-ingredients"), ent, args.User);
            return;
        }

        UseIngredients(ent, ent.Comp.Solution, flavour.Ingredients);
        if (_solutions.TryGetSolution(args.Used, "food", out var food, out var foodSol))
        {
            _solutions.SetCapacity(food.Value, foodSol.MaxVolume + ExtraVolumePerScoop);
            if (flavour.Reagent is { } reagent)
                _solutions.TryAddReagent(food.Value, reagent, flavour.Amount, out _);
        }

        var name = Loc.GetString(flavour.Name);
        if (cone.Scoops == 0)
            _meta.SetEntityName(args.Used, Loc.GetString("imperial-icecream-filled-name", ("flavour", name)));

        cone.Scoops++;
        cone.Scoop = flavour.Color;
        Dirty(args.Used, cone);
        _quality.MarkHandmade(args.Used);
        _popup.PopupEntity(Loc.GetString("imperial-icecream-scoop", ("flavour", name)), ent);
    }
}
