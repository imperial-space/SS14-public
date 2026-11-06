using System.Linq;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Research.Items;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Research.Items;

/// <summary>
/// Крафт TG из предмета-основы: рецепты видны как действия на предмете, нужные вещи ищутся в руках и рядом.
/// </summary>
public sealed class ImperialTgCraftSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialTgCraftComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<ImperialTgCraftComponent, ImperialTgCraftDoAfterEvent>(OnDoAfter);
    }

    private void OnVerbs(Entity<ImperialTgCraftComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        for (var i = 0; i < ent.Comp.Recipes.Count; i++)
        {
            var index = i;
            var recipe = ent.Comp.Recipes[i];
            var resultName = _proto.TryIndex<EntityPrototype>(recipe.Result, out var resultProto) ? resultProto.Name : recipe.Result.Id;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("imperial-tg-craft-verb", ("result", resultName)),
                Message = Requirements(recipe),
                Act = () => TryStart(ent, user, index),
            });
        }
    }

    private string Requirements(ImperialTgCraftRecipe recipe)
    {
        var parts = recipe.Items.Select(i => $"{Loc.GetString(i.Name)} ×{i.Count}").ToList();
        foreach (var (reagent, amount) in recipe.Reagents)
        {
            var name = _proto.TryIndex(reagent, out var proto) ? proto.LocalizedName : reagent.Id;
            parts.Add($"{name} {amount}u");
        }

        return Loc.GetString("imperial-tg-craft-needs", ("list", string.Join(", ", parts)));
    }

    private void TryStart(Entity<ImperialTgCraftComponent> ent, EntityUid user, int index)
    {
        var recipe = ent.Comp.Recipes[index];
        if (!Collect(ent, user, recipe, out _, out _))
        {
            _popup.PopupEntity(Requirements(recipe), user, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, recipe.Time,
            new ImperialTgCraftDoAfterEvent(index), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<ImperialTgCraftComponent> ent, ref ImperialTgCraftDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Recipe < 0 || args.Recipe >= ent.Comp.Recipes.Count)
            return;

        args.Handled = true;
        var user = args.User;
        var recipe = ent.Comp.Recipes[args.Recipe];

        if (!Collect(ent, user, recipe, out var items, out var reagentSources))
        {
            _popup.PopupEntity(Requirements(recipe), user, user);
            return;
        }

        foreach (var (item, count) in items)
        {
            if (TryComp<StackComponent>(item, out var stack) && stack.Count > count)
                _stack.SetCount((item, stack), stack.Count - count);
            else
                QueueDel(item);
        }

        foreach (var (reagent, amount) in recipe.Reagents)
        {
            var left = amount;
            foreach (var source in reagentSources)
            {
                if (left <= 0)
                    break;

                foreach (var (_, soln) in _solution.EnumerateSolutions(source))
                {
                    var removed = _solution.RemoveReagent(soln, reagent, left);
                    left -= removed;
                    if (left <= 0)
                        break;
                }
            }
        }

        var coords = _transform.GetMapCoordinates(user);
        QueueDel(ent);
        var result = Spawn(recipe.Result, coords);
        _hands.TryPickupAnyHand(user, result);
        _popup.PopupEntity(Loc.GetString("imperial-tg-craft-done", ("result", result)), user, user);
    }

    /// <summary>
    /// Ищет предметы рецепта в руках, в инвентаре рук и вокруг игрока.
    /// </summary>
    private bool Collect(Entity<ImperialTgCraftComponent> ent, EntityUid user, ImperialTgCraftRecipe recipe,
        out List<(EntityUid, int)> used, out List<EntityUid> reagentSources)
    {
        used = new List<(EntityUid, int)>();
        reagentSources = new List<EntityUid>();

        var candidates = new HashSet<EntityUid>(_hands.EnumerateHeld(user));
        foreach (var near in _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(user), ent.Comp.Range, LookupFlags.Uncontained))
            candidates.Add(near);
        candidates.Remove(ent);

        var taken = new HashSet<EntityUid>();
        foreach (var req in recipe.Items)
        {
            var need = req.Count;
            foreach (var cand in candidates)
            {
                if (need <= 0)
                    break;
                if (taken.Contains(cand) || !Matches(cand, req))
                    continue;

                var have = TryComp<StackComponent>(cand, out var stack) ? stack.Count : 1;
                var take = Math.Min(have, need);
                used.Add((cand, take));
                taken.Add(cand);
                need -= take;
            }

            if (need > 0)
                return false;
        }

        foreach (var (reagent, amount) in recipe.Reagents)
        {
            var total = FixedPoint2.Zero;
            foreach (var cand in candidates)
            {
                if (taken.Contains(cand) || !HasComp<SolutionContainerManagerComponent>(cand))
                    continue;

                var found = FixedPoint2.Zero;
                foreach (var (name, _) in _solution.EnumerateSolutions(cand))
                {
                    if (_solution.TryGetSolution(cand, name, out _, out var sol))
                        found += sol.GetTotalPrototypeQuantity(reagent);
                }

                if (found <= 0)
                    continue;

                total += found;
                if (!reagentSources.Contains(cand))
                    reagentSources.Add(cand);
            }

            if (total < amount)
                return false;
        }

        return true;
    }

    private bool Matches(EntityUid uid, ImperialTgCraftItem req)
    {
        if (req.Tag is { } tag && _tag.HasTag(uid, tag))
            return true;

        var proto = MetaData(uid).EntityPrototype;
        if (proto == null)
            return false;

        foreach (var want in req.Protos)
        {
            if (proto.ID == want.Id || _proto.EnumerateParents<EntityPrototype>(proto.ID).Any(p => p.ID == want.Id))
                return true;
        }

        return false;
    }
}
