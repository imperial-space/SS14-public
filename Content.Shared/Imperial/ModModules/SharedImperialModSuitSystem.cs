using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// Блок управления МОДа (/obj/item/mod/control): крышка, установка и снятие модулей, осмотр.
/// Работа модулей (питание, эффекты, кнопки) — на сервере.
/// </summary>
public abstract class SharedImperialModSuitSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly InventorySystem Inventory = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly ActionContainerSystem ActionContainer = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly Content.Shared.Containers.ItemSlots.ItemSlotsSystem _itemSlots = default!;

    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    /// <summary>DEFAULT_CHARGE_DRAIN: 0.5% стандартной (малой) батареи в секунду.</summary>
    public const float ChargeUnit = 0.005f * 360f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialModSuitComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ImperialModSuitComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<ImperialModSuitComponent, ImperialModPanelDoAfterEvent>(OnPanelDoAfter);
        SubscribeLocalEvent<ImperialModSuitComponent, ImperialModRemoveDoAfterEvent>(OnRemoveDoAfter);
        SubscribeLocalEvent<ImperialModSuitComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ImperialModSuitComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialModSuitComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ImperialModModuleComponent, ExaminedEvent>(OnModuleExamined);
    }

    private void OnInit(Entity<ImperialModSuitComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Modules = Container.EnsureContainer<Container>(ent, ImperialModSuitComponent.ContainerId);
    }

    #region Части костюма

    /// <summary>Носитель: блок управления надет в слот спины.</summary>
    public EntityUid? GetWearer(EntityUid suit)
    {
        if (!Inventory.TryGetContainingSlot(suit, out var slot) || slot.Name != "back")
            return null;

        return Transform(suit).ParentUid;
    }

    /// <summary>Развёрнутая часть костюма на носителе, или null.</summary>
    public EntityUid? GetPart(EntityUid suit, ImperialModPart part)
    {
        if (GetWearer(suit) is not { } wearer)
            return null;

        switch (part)
        {
            case ImperialModPart.Back:
                return suit;
            case ImperialModPart.Outer:
                return GetDeployed(wearer, suit, "outerClothing");
            case ImperialModPart.Head:
                return GetDeployed(wearer, suit, "outerClothing") is { } outer ? GetDeployed(wearer, outer, "head") : null;
            default:
                return null;
        }
    }

    /// <summary>Часть, которую <paramref name="owner"/> развернул через ToggleableClothing и которая надета.</summary>
    private EntityUid? GetDeployed(EntityUid wearer, EntityUid owner, string slot)
    {
        if (!TryComp<ToggleableClothingComponent>(owner, out var toggleable))
            return null;

        var clothing = toggleable.ClothingUid;
        if (clothing == null || !Inventory.TryGetSlotEntity(wearer, slot, out var worn) || worn != clothing)
            return null;

        return clothing;
    }

    /// <summary>Все части из флагов развёрнуты.</summary>
    public bool HasParts(EntityUid suit, ImperialModPart parts)
    {
        foreach (var part in new[] { ImperialModPart.Back, ImperialModPart.Outer, ImperialModPart.Head })
        {
            if ((parts & part) != 0 && GetPart(suit, part) == null)
                return false;
        }

        return true;
    }

    /// <summary>active: костюм надет, нагрудник развёрнут, крышка закрыта.</summary>
    public bool IsActive(Entity<ImperialModSuitComponent> suit)
    {
        if (suit.Comp.Open || GetPart(suit, ImperialModPart.Outer) == null)
            return false;

        return suit.Comp.LockedDna == null || GetWearer(suit) is { } wearer
            && TryComp<DnaComponent>(wearer, out var dna) && dna.DNA == suit.Comp.LockedDna;
    }

    public int GetComplexity(Entity<ImperialModSuitComponent> suit)
    {
        var total = 0;
        foreach (var module in suit.Comp.Modules.ContainedEntities)
        {
            if (TryComp<ImperialModModuleComponent>(module, out var comp))
                total += comp.Complexity;
        }

        return total;
    }

    #endregion

    #region Установка и снятие

    private void OnInteractUsing(Entity<ImperialModSuitComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ImperialModModuleComponent>(args.Used, out var module))
        {
            args.Handled = true;
            TryInstall(ent, (args.Used, module), args.User);
            return;
        }

        // Принадлежности модулей (удочка рыболовной перчатки и т. п.) вставляются кликом по блоку управления.
        foreach (var installed in ent.Comp.Modules.ContainedEntities)
        {
            if (!TryComp<Content.Shared.Containers.ItemSlots.ItemSlotsComponent>(installed, out var slots))
                continue;

            foreach (var slot in slots.Slots.Values)
            {
                if (!_itemSlots.CanInsert(installed, args.Used, args.User, slot) || !_itemSlots.TryInsertFromHand(installed, slot, args.User))
                    continue;

                args.Handled = true;
                return;
            }
        }

        if (_tool.HasQuality(args.Used, ScrewingQuality))
        {
            args.Handled = true;
            if (IsActive(ent))
            {
                Popup.PopupClient(Loc.GetString("imperial-mod-deactivate-first"), ent, args.User);
                return;
            }

            _tool.UseTool(args.Used, args.User, ent, 1f, ScrewingQuality, new ImperialModPanelDoAfterEvent());
        }
    }

    private void OnPanelDoAfter(Entity<ImperialModSuitComponent> ent, ref ImperialModPanelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Open = !ent.Comp.Open;
        Dirty(ent);
        Popup.PopupClient(Loc.GetString(ent.Comp.Open ? "imperial-mod-cover-opened" : "imperial-mod-cover-closed"), ent, args.User);
        OnPanelToggled(ent);
    }

    /// <summary>Крышка открыта или закрыта: сервер пересчитывает работу модулей.</summary>
    protected virtual void OnPanelToggled(Entity<ImperialModSuitComponent> ent)
    {
    }

    /// <summary>install(): проверки несовместимости и сложности, затем модуль в контейнер.</summary>
    public bool TryInstall(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module, EntityUid? user)
    {
        if (!suit.Comp.Open)
        {
            if (user != null)
                Popup.PopupClient(Loc.GetString("imperial-mod-cover-closed-install"), suit, user.Value);
            return false;
        }

        foreach (var installed in suit.Comp.Modules.ContainedEntities)
        {
            if (!TryComp<ImperialModModuleComponent>(installed, out var other))
                continue;

            if (other.Groups.Intersect(module.Comp.Groups).Any())
            {
                if (user != null)
                    Popup.PopupClient(Loc.GetString("imperial-mod-incompatible", ("module", installed)), suit, user.Value);
                return false;
            }
        }

        if (GetComplexity(suit) + module.Comp.Complexity > suit.Comp.MaxComplexity)
        {
            if (user != null)
                Popup.PopupClient(Loc.GetString("imperial-mod-complexity-max"), suit, user.Value);
            return false;
        }

        if (!Container.Insert(module.Owner, suit.Comp.Modules))
            return false;

        if (user != null)
            Popup.PopupClient(Loc.GetString("imperial-mod-installed", ("module", module.Owner)), suit, user.Value);

        return true;
    }

    private void OnGetVerbs(Entity<ImperialModSuitComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Open)
            return;

        var user = args.User;
        var tool = _hands.GetActiveItem(user);
        foreach (var module in ent.Comp.Modules.ContainedEntities)
        {
            var target = module;
            var verb = new AlternativeVerb
            {
                Text = Loc.GetString("imperial-mod-verb-remove", ("module", target)),
                Category = VerbCategory.Eject,
                Priority = -1,
                Act = () =>
                {
                    if (tool == null || !_tool.UseTool(tool.Value, user, ent, 1f, PryingQuality, new ImperialModRemoveDoAfterEvent(GetNetEntity(target))))
                        Popup.PopupClient(Loc.GetString("imperial-mod-need-crowbar"), ent, user);
                },
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnRemoveDoAfter(Entity<ImperialModSuitComponent> ent, ref ImperialModRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Open)
            return;

        args.Handled = true;
        var module = GetEntity(args.Module);
        if (!ent.Comp.Modules.Contains(module))
            return;

        Container.Remove(module, ent.Comp.Modules);
        _hands.PickupOrDrop(args.User, module);
        Popup.PopupClient(Loc.GetString("imperial-mod-removed", ("module", module)), ent, args.User);
    }

    #endregion

    private void OnGetActions(Entity<ImperialModSuitComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags != SlotFlags.BACK)
            return;

        foreach (var module in ent.Comp.Modules.ContainedEntities)
        {
            if (TryComp<ImperialModModuleComponent>(module, out var comp) && comp.ActionEntity != null)
                args.AddAction(comp.ActionEntity);
        }
    }

    private void OnExamined(Entity<ImperialModSuitComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(ImperialModSuitComponent)))
        {
            args.PushMarkup(Loc.GetString("imperial-mod-examine-complexity", ("current", GetComplexity(ent)), ("max", ent.Comp.MaxComplexity)));
            args.PushMarkup(Loc.GetString(ent.Comp.Open ? "imperial-mod-examine-open" : "imperial-mod-examine-closed"));

            if (ent.Comp.Modules.ContainedEntities.Count == 0)
            {
                args.PushMarkup(Loc.GetString("imperial-mod-examine-no-modules"));
                return;
            }

            args.PushMarkup(Loc.GetString("imperial-mod-examine-modules"));
            foreach (var module in ent.Comp.Modules.ContainedEntities)
            {
                if (!TryComp<ImperialModModuleComponent>(module, out var comp))
                    continue;

                args.PushMarkup(Loc.GetString(comp.Active ? "imperial-mod-examine-module-active" : "imperial-mod-examine-module",
                    ("module", module), ("complexity", comp.Complexity)));
            }
        }
    }

    private void OnModuleExamined(Entity<ImperialModModuleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("imperial-mod-module-examine-complexity", ("complexity", ent.Comp.Complexity)));

        var parts = new List<string>();
        if ((ent.Comp.RequiredParts & ImperialModPart.Head) != 0)
            parts.Add(Loc.GetString("imperial-mod-part-head"));
        if ((ent.Comp.RequiredParts & ImperialModPart.Outer) != 0)
            parts.Add(Loc.GetString("imperial-mod-part-outer"));
        if (parts.Count > 0)
            args.PushMarkup(Loc.GetString("imperial-mod-module-examine-parts", ("parts", string.Join(", ", parts))));
    }
}
