using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Kitchen;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Kitchen;

[RegisterComponent]
public sealed partial class ImperialCraftUserComponent : Component
{
    public EntityUid? Action;
}

/// <summary>
/// /datum/component/personal_crafting для еды TG: ингредиенты берутся из рук и с соседних клеток,
/// реагенты — из открытых ёмкостей рядом; реагенты ингредиентов переходят в блюдо.
/// </summary>
public sealed class ImperialCraftingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ImperialFoodQualitySystem _quality = default!;

    private static readonly EntProtoId MenuAction = "ImperialActionCraftMenu";

    /// <summary>Соседние клетки, как range(1) TG.</summary>
    private const float Range = 1.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<ImperialCraftStateRequest>(OnStateRequest);
        SubscribeNetworkEvent<ImperialCraftMessage>(OnCraft);
        SubscribeLocalEvent<ImperialCraftUserComponent, ImperialCraftDoAfterEvent>(OnCraftDoAfter);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Entity))
            return;

        var user = EnsureComp<ImperialCraftUserComponent>(args.Entity);
        if (user.Action == null)
            _actions.AddAction(args.Entity, ref user.Action, MenuAction);
    }

    #region Окружение

    /// <summary>get_environment: предметы в руках и на соседних клетках.</summary>
    private List<EntityUid> Environment(EntityUid user)
    {
        var result = _hands.EnumerateHeld(user).ToList();
        foreach (var near in _lookup.GetEntitiesInRange(Transform(user).Coordinates, Range, LookupFlags.Uncontained | LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (near == user || result.Contains(near) || _containers.IsEntityInContainer(near))
                continue;

            if (_interaction.InRangeUnobstructed(user, near, Range))
                result.Add(near);
        }

        return result;
    }

    private bool Matches(EntityUid uid, EntProtoId proto)
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

    private int Count(EntityUid uid) => TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;

    /// <summary>check_contents: хватает ли ингредиентов и реагентов. used — какие предметы и сколько пойдёт.</summary>
    private bool Check(EntityUid user, ImperialCraftRecipePrototype recipe, List<EntityUid> env, out Dictionary<EntityUid, int> used)
    {
        used = new Dictionary<EntityUid, int>();
        foreach (var (proto, amount) in recipe.Ingredients)
        {
            var need = amount;
            foreach (var item in env)
            {
                if (need <= 0)
                    break;
                if (!Matches(item, proto))
                    continue;

                var free = Count(item) - used.GetValueOrDefault(item);
                if (free <= 0)
                    continue;

                var take = Math.Min(free, need);
                used[item] = used.GetValueOrDefault(item) + take;
                need -= take;
            }

            if (need > 0)
                return false;
        }

        foreach (var (reagent, amount) in recipe.Reagents)
        {
            var total = FixedPoint2.Zero;
            foreach (var item in env)
            {
                if (used.ContainsKey(item) || !_solutions.TryGetDrainableSolution(item, out _, out var solution))
                    continue;
                total += solution.GetTotalPrototypeQuantity(reagent);
            }

            if (total < amount)
                return false;
        }

        return true;
    }

    #endregion

    private void OnStateRequest(ImperialCraftStateRequest msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var env = Environment(user);
        var craftable = new HashSet<string>();
        foreach (var recipe in _proto.EnumeratePrototypes<ImperialCraftRecipePrototype>())
        {
            if (Check(user, recipe, env, out _))
                craftable.Add(recipe.ID);
        }

        RaiseNetworkEvent(new ImperialCraftStateMessage(craftable), args.SenderSession);
    }

    private void OnCraft(ImperialCraftMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !HasComp<ImperialCraftUserComponent>(user) ||
            !_proto.TryIndex<ImperialCraftRecipePrototype>(msg.Recipe, out var recipe))
            return;

        if (!Check(user, recipe, Environment(user), out _))
        {
            _popup.PopupEntity(Loc.GetString("imperial-craft-missing"), user, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, recipe.Time, new ImperialCraftDoAfterEvent(recipe.ID), user)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    /// <summary>construct_item: ингредиенты съедаются, их реагенты переходят в блюдо, блюдо — рукотворное.</summary>
    private void OnCraftDoAfter(Entity<ImperialCraftUserComponent> ent, ref ImperialCraftDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_proto.TryIndex<ImperialCraftRecipePrototype>(args.Recipe, out var recipe))
            return;

        args.Handled = true;
        var user = ent.Owner;
        var env = Environment(user);
        if (!Check(user, recipe, env, out var used))
        {
            _popup.PopupEntity(Loc.GetString("imperial-craft-missing"), user, user);
            return;
        }

        var collected = new Solution();
        foreach (var (item, amount) in used)
        {
            if (TryComp<StackComponent>(item, out var stack) && stack.Count > amount)
            {
                _stacks.TryUse((item, stack), amount);
                continue;
            }

            if (_solutions.TryGetSolution(item, "food", out _, out var food))
                collected.AddSolution(food, _proto);
            QueueDel(item);
        }

        foreach (var (reagent, amount) in recipe.Reagents)
        {
            var need = amount;
            foreach (var item in env)
            {
                if (need <= 0)
                    break;
                if (used.ContainsKey(item) || !_solutions.TryGetDrainableSolution(item, out var solutionEnt, out _))
                    continue;

                var removed = _solutions.RemoveReagent(solutionEnt.Value, reagent, need);
                collected.AddReagent(reagent, removed);
                need -= removed;
            }
        }

        for (var i = 0; i < recipe.Amount; i++)
        {
            var made = Spawn(recipe.Result, Transform(user).Coordinates);
            if (_solutions.TryGetSolution(made, "food", out var target, out _) && collected.Volume > 0)
            {
                // CRAFT_CLEARS_REAGENTS | CRAFT_TRANSFERS_REAGENT_COMPONENTS.
                var part = collected.Clone();
                part.ScaleSolution(1f / recipe.Amount);
                _solutions.RemoveAllSolution(target.Value);
                _solutions.TryAddSolution(target.Value, part);
            }

            _quality.MarkHandmade(made);
            _hands.TryPickupAnyHand(user, made);
        }

        _popup.PopupEntity(Loc.GetString("imperial-craft-done", ("item", _proto.Index(recipe.Result).Name)), user, user);
    }
}
