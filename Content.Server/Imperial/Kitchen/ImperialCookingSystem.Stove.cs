using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Kitchen;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Placeable;
using Content.Shared.Tools;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Kitchen;

/// <summary>Плита с кастрюлей и супами, процессор и обработка еды инструментами.</summary>
public sealed partial class ImperialCookingSystem
{
    private static readonly ProtoId<ReagentPrototype> Nutriment = "Nutriment";
    private static readonly ProtoId<ToolQualityPrototype> SawingQuality = "Sawing";
    private static readonly ProtoId<ToolQualityPrototype> RollingQuality = "Rolling";

    #region Плита

    private void OnStoveMapInit(Entity<ImperialStoveComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.StartingPot is not { } pot ||
            !_slots.TryGetSlot(ent, ImperialStoveComponent.PotSlot, out var slot) || slot.HasItem)
            return;

        _slots.TryInsert(ent, slot, Spawn(pot, Transform(ent).Coordinates), null);
    }

    /// <summary>ПКМ по плите TG: включить или выключить конфорку.</summary>
    private void OnStoveVerbs(Entity<ImperialStoveComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.On ? "imperial-stove-off" : "imperial-stove-on"),
            Priority = 2,
            Act = () => ToggleStove(ent, user),
        });
    }

    private void ToggleStove(Entity<ImperialStoveComponent> ent, EntityUid user)
    {
        if (!ent.Comp.On && !_power.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("imperial-cooking-no-power"), ent, user);
            return;
        }

        ent.Comp.On = !ent.Comp.On;
        Dirty(ent);
        _appearance.SetData(ent, ImperialKitchenVisuals.StoveOn, ent.Comp.On);
        _audio.PlayPvs("/Audio/Machines/click.ogg", ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.On ? "imperial-stove-turned-on" : "imperial-stove-turned-off"), ent);
    }

    /// <summary>stove/process: кастрюля греется к SOUP_BURN_TEMP + 80, в ней варится суп.</summary>
    private void UpdateStoves()
    {
        var query = EntityQueryEnumerator<ImperialStoveComponent>();
        while (query.MoveNext(out var uid, out var stove))
        {
            if (!stove.On)
                continue;

            if (!_power.IsPowered(uid))
            {
                stove.On = false;
                Dirty(uid, stove);
                _appearance.SetData(uid, ImperialKitchenVisuals.StoveOn, false);
                continue;
            }

            if (!_slots.TryGetSlot(uid, ImperialStoveComponent.PotSlot, out var slot) || slot.Item is not { } pot ||
                !TryComp<ImperialSoupPotComponent>(pot, out var potComp) ||
                !_solutions.TryGetSolution(pot, potComp.Solution, out var solutionEnt, out var solution))
                continue;

            // expose_temperature: температура тянется к цели долей heat_coefficient за тик.
            var temperature = solution.Temperature + (stove.TargetTemperature - solution.Temperature) * stove.HeatCoefficient;
            _solutions.SetTemperature(solutionEnt.Value, temperature);
            TryCookSoup((pot, potComp));
        }
    }

    #endregion

    #region Кастрюля

    private void OnPotInit(Entity<ImperialSoupPotComponent> ent, ref ComponentInit args)
    {
        _containers.EnsureContainer<Container>(ent, ImperialSoupPotComponent.IngredientsContainer);
    }

    /// <summary>soup_pot/attackby: еда бросается в кастрюлю.</summary>
    private void OnPotInteractUsing(Entity<ImperialSoupPotComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !TryComp<EdibleComponent>(args.Used, out var edible) ||
            edible.Edible != Content.Shared.Nutrition.EntitySystems.IngestionSystem.Food)
            return;

        var container = _containers.EnsureContainer<Container>(ent, ImperialSoupPotComponent.IngredientsContainer);
        args.Handled = true;
        if (container.ContainedEntities.Count >= ent.Comp.MaxIngredients)
        {
            _popup.PopupEntity(Loc.GetString("imperial-soup-pot-full", ("pot", ent.Owner)), ent, args.User);
            return;
        }

        if (_containers.Insert(args.Used, container))
            _popup.PopupEntity(Loc.GetString("imperial-soup-pot-add", ("item", args.Used), ("pot", ent.Owner)), ent, args.User);
    }

    private void OnPotVerbs(Entity<ImperialSoupPotComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract ||
            !_containers.TryGetContainer(ent, ImperialSoupPotComponent.IngredientsContainer, out var container) ||
            container.ContainedEntities.Count == 0)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-soup-pot-empty"),
            Act = () => _containers.EmptyContainer(container),
        });
    }

    /// <summary>food/soup: при нужной температуре реагенты и ингредиенты становятся супом.</summary>
    private void TryCookSoup(Entity<ImperialSoupPotComponent> pot)
    {
        if (!_solutions.TryGetSolution(pot.Owner, pot.Comp.Solution, out var solutionEnt, out var solution) ||
            !_containers.TryGetContainer(pot, ImperialSoupPotComponent.IngredientsContainer, out var container))
            return;

        foreach (var recipe in _proto.EnumeratePrototypes<ImperialSoupRecipePrototype>())
        {
            if (solution.Temperature < recipe.RequiredTemperature)
                continue;

            if (recipe.Reagents.Any(r => solution.GetTotalPrototypeQuantity(r.Key) < r.Value))
                continue;

            var used = new List<EntityUid>();
            var ok = true;
            foreach (var (proto, count) in recipe.Ingredients)
            {
                var found = container.ContainedEntities.Where(e => !used.Contains(e) && IsOrInherits(e, proto)).Take(count).ToList();
                if (found.Count < count)
                {
                    ok = false;
                    break;
                }

                used.AddRange(found);
            }

            if (!ok || used.Count == 0 && recipe.Ingredients.Count > 0)
                continue;

            foreach (var (reagent, amount) in recipe.Reagents)
                _solutions.RemoveReagent(solutionEnt.Value, reagent, amount);

            foreach (var ingredient in used)
            {
                // Реагенты ингредиентов переходят в суп, часть питательности уходит в сам суп.
                if (_solutions.TryGetSolution(ingredient, "food", out _, out var food))
                {
                    var moved = food.Clone();
                    moved.ScaleSolution(recipe.IngredientMultiplier);
                    var nutriment = moved.GetTotalPrototypeQuantity(Nutriment);
                    if (nutriment > 0)
                        moved.RemoveReagent(Nutriment, nutriment * recipe.NutrimentConverted);
                    _solutions.TryAddSolution(solutionEnt.Value, moved);
                }

                Del(ingredient);
            }

            if (recipe.ResultFood is { } result)
            {
                var made = Spawn(result, Transform(pot).Coordinates);
                _quality.MarkHandmade(made);
            }

            foreach (var (reagent, amount) in recipe.Results)
                _solutions.TryAddReagent(solutionEnt.Value, reagent, amount, out _, solution.Temperature);

            _popup.PopupEntity(Loc.GetString("imperial-soup-pot-cooked"), pot, Content.Shared.Popups.PopupType.Small);
            return;
        }
    }

    private bool IsOrInherits(EntityUid uid, EntProtoId proto)
    {
        if (MetaData(uid).EntityPrototype is not { } entity)
            return false;

        foreach (var (id, _) in _proto.EnumerateAllParents<EntityPrototype>(entity.ID, true))
        {
            if (id == proto)
                return true;
        }

        return false;
    }

    #endregion

    #region Процессор

    private void OnProcessorInit(Entity<ImperialFoodProcessorComponent> ent, ref ComponentInit args)
    {
        _containers.EnsureContainer<Container>(ent, ImperialFoodProcessorComponent.Container);
    }

    private ImperialProcessorRecipePrototype? ProcessorRecipe(EntityUid item)
    {
        return _proto.EnumeratePrototypes<ImperialProcessorRecipePrototype>().FirstOrDefault(r => IsOrInherits(item, r.Input));
    }

    private void OnProcessorInteractUsing(Entity<ImperialFoodProcessorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Processing)
        {
            _popup.PopupEntity(Loc.GetString("imperial-processor-busy"), ent, args.User);
            return;
        }

        if (ProcessorRecipe(args.Used) == null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-processor-cant", ("item", args.Used)), ent, args.User);
            return;
        }

        if (_containers.Insert(args.Used, _containers.EnsureContainer<Container>(ent, ImperialFoodProcessorComponent.Container)))
            _popup.PopupEntity(Loc.GetString("imperial-processor-add", ("item", args.Used)), ent, args.User);
    }

    /// <summary>attack_hand: включить переработку всего, что внутри.</summary>
    private void OnProcessorActivate(Entity<ImperialFoodProcessorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || ent.Comp.Processing)
            return;

        args.Handled = true;
        var container = _containers.EnsureContainer<Container>(ent, ImperialFoodProcessorComponent.Container);
        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-processor-empty"), ent, args.User);
            return;
        }

        if (!_power.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("imperial-cooking-no-power"), ent, args.User);
            return;
        }

        var total = container.ContainedEntities.Sum(e => ProcessorRecipe(e)?.Time ?? 0f);
        ent.Comp.Processing = true;
        ent.Comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(total / Math.Max(ent.Comp.Speed, 0.1f));
        Dirty(ent);
        _appearance.SetData(ent, ImperialKitchenVisuals.Active, true);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("imperial-processor-start", ("processor", ent.Owner)), ent);
    }

    private void UpdateProcessors()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialFoodProcessorComponent>();
        while (query.MoveNext(out var uid, out var processor))
        {
            if (!processor.Processing || now < processor.EndTime)
                continue;

            processor.Processing = false;
            Dirty(uid, processor);
            _appearance.SetData(uid, ImperialKitchenVisuals.Active, false);

            var container = _containers.EnsureContainer<Container>(uid, ImperialFoodProcessorComponent.Container);
            foreach (var item in container.ContainedEntities.ToArray())
            {
                var recipe = ProcessorRecipe(item);
                if (recipe == null)
                {
                    _containers.Remove(item, container);
                    continue;
                }

                for (var i = 0; i < recipe.Multiplier * processor.Amount; i++)
                    Spawn(recipe.Output, Transform(uid).Coordinates);

                Del(item);
            }

            _popup.PopupEntity(Loc.GetString("imperial-processor-done", ("processor", uid)), uid);
        }
    }

    #endregion

    #region Инструменты

    private bool HasTool(EntityUid used, ImperialKitchenTool tool)
    {
        if (TryComp<ImperialKitchenToolComponent>(used, out var kitchen) && kitchen.Tools.Contains(tool))
            return true;

        return tool switch
        {
            ImperialKitchenTool.Knife or ImperialKitchenTool.Scalpel => HasComp<SharpComponent>(used),
            ImperialKitchenTool.Saw => _tools.HasQuality(used, SawingQuality),
            ImperialKitchenTool.RollingPin => _tools.HasQuality(used, RollingQuality),
            _ => false,
        };
    }

    /// <summary>Стол или противень под предметом (table_required).</summary>
    private bool OnTable(EntityUid item)
    {
        if (_containers.TryGetContainingContainer(item, out var container))
            return HasComp<ImperialOvenTrayComponent>(container.Owner);

        foreach (var near in _lookup.GetEntitiesInRange<PlaceableSurfaceComponent>(Transform(item).Coordinates, 0.4f))
        {
            if (near.Owner != item)
                return true;
        }

        return false;
    }

    /// <summary>processable/try_process.</summary>
    private void OnProcessableInteractUsing(Entity<ImperialProcessableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        for (var i = 0; i < ent.Comp.Entries.Count; i++)
        {
            var entry = ent.Comp.Entries[i];
            if (!HasTool(args.Used, entry.Tool))
                continue;

            args.Handled = true;
            if (entry.TableRequired && !OnTable(ent))
            {
                _popup.PopupEntity(Loc.GetString("imperial-process-need-table", ("result", _proto.Index(entry.Result).Name)), ent, args.User);
                return;
            }

            var speed = CompOrNull<ImperialKitchenToolComponent>(args.Used)?.Speed ?? 1f;
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, entry.Time * speed, new ImperialProcessDoAfterEvent(i), ent, ent, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }
    }

    private void OnProcessDoAfter(Entity<ImperialProcessableComponent> ent, ref ImperialProcessDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Entry >= ent.Comp.Entries.Count)
            return;

        args.Handled = true;
        var entry = ent.Comp.Entries[args.Entry];
        _solutions.TryGetSolution(ent.Owner, "food", out _, out var source);

        for (var i = 0; i < entry.Amount; i++)
        {
            var made = Spawn(entry.Result, Transform(ent).Coordinates);
            if (source != null && _solutions.TryGetSolution(made, "food", out var target, out _))
            {
                var part = source.Clone();
                part.ScaleSolution(1f / entry.Amount);
                _solutions.RemoveAllSolution(target.Value);
                _solutions.TryAddSolution(target.Value, part);
            }
        }

        Del(ent);
    }

    #endregion
}
