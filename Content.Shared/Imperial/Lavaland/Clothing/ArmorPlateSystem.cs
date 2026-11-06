using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;

namespace Content.Shared.Imperial.Lavaland.Clothing;

public sealed class ArmorPlateSystem : EntitySystem
{
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    private static readonly string[] MeleeTypes = { "Blunt", "Slash" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArmorPlateComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ArmorPlateComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ArmorPlateComponent, RefreshNameModifiersEvent>(OnRefreshName);
        SubscribeLocalEvent<ArmorPlateComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
    }

    /// <summary>applyplate().</summary>
    private void OnInteractUsing(Entity<ArmorPlateComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != ent.Comp.UpgradeStack)
            return;

        args.Handled = true;
        if (ent.Comp.Amount >= ent.Comp.MaxAmount)
        {
            _popup.PopupClient(Loc.GetString("armor-plate-max", ("item", ent.Owner)), ent, args.User);
            return;
        }

        if (!_stack.TryUse((args.Used, stack), 1))
            return;

        ent.Comp.Amount++;
        Dirty(ent);
        _nameModifier.RefreshNameModifiers(ent.Owner);
        _popup.PopupClient(Loc.GetString("armor-plate-applied", ("item", ent.Owner)), ent, args.User);
    }

    private void OnExamined(Entity<ArmorPlateComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Amount > 0
            ? Loc.GetString("armor-plate-examine-amount", ("amount", ent.Comp.Amount), ("max", ent.Comp.MaxAmount))
            : Loc.GetString("armor-plate-examine-none", ("max", ent.Comp.MaxAmount)));
    }

    /// <summary>upgrade_prefix = "reinforced".</summary>
    private void OnRefreshName(Entity<ArmorPlateComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Amount > 0)
            args.AddModifier("armor-plate-name-prefix");
    }

    /// <summary>
    /// Пластины прибавляют melee к броне: урон ×(1 - (base + n·plate)) / (1 - base) поверх основной брони.
    /// </summary>
    private void OnDamageModify(EntityUid uid, ArmorPlateComponent comp, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (comp.Amount <= 0)
            return;

        var baseFraction = comp.BaseMelee / 100f;
        var plated = Math.Min(0.9f, baseFraction + comp.Amount * comp.MeleePerPlate / 100f);
        var factor = (1f - plated) / (1f - baseFraction);

        foreach (var type in MeleeTypes)
        {
            if (args.Args.Damage.DamageDict.TryGetValue(type, out var value) && value > FixedPoint2.Zero)
                args.Args.Damage.DamageDict[type] = value * factor;
        }
    }
}
