using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Генная пушка и инъекторы черт рыб: извлечение черты в пустой шприц (выбор через меню действий на рыбе)
/// и ввод черты. Повторная обработка одной рыбы её убивает (TRAIT_FISH_GENEGUNNED).
/// </summary>
public sealed class ImperialFishGenegunSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;
    [Dependency] private readonly ImperialFishTraitSystem _traits = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    private const string GeneProto = "ImperialFishGene";
    private const string HypospraySound = "/Audio/Items/hypospray.ogg";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishGenegunComponent, AfterInteractEvent>(OnGenegunAfterInteract);
        SubscribeLocalEvent<ImperialFishGenegunComponent, UseInHandEvent>(OnGenegunUseInHand);
        SubscribeLocalEvent<ImperialFishGenegunComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<ImperialFishGenegunComponent, EntRemovedFromContainerMessage>(OnSlotRemoved);
        SubscribeLocalEvent<ImperialFishGeneComponent, AfterInteractEvent>(OnGeneAfterInteract);
        SubscribeLocalEvent<ImperialFishGeneComponent, MapInitEvent>(OnGeneMapInit);
        Subs.BuiEvents<ImperialFishGenegunComponent>(ImperialFishGenegunUiKey.Key, subs =>
        {
            subs.Event<ImperialFishGenegunExtractMessage>(OnExtractMessage);
        });
    }

    private EntityUid? GetLoaded(EntityUid gun) => _slots.GetItemOrNull(gun, ImperialFishGenegunComponent.SlotId);

    private bool IsHolo(EntityUid fish) => Prototype(fish)?.ID.StartsWith("ImperialFishHolo") == true;

    #region Пушка

    private void OnSlotChanged(Entity<ImperialFishGenegunComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateVisuals(ent);
        _audio.PlayPvs("/Audio/Weapons/Guns/MagIn/revolver_magin.ogg", ent);
    }

    private void OnSlotRemoved(Entity<ImperialFishGenegunComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(EntityUid gun)
    {
        var state = GetLoaded(gun) switch
        {
            null => ImperialFishGenegunState.Empty,
            { } loaded when HasComp<ImperialFishGeneComponent>(loaded) => ImperialFishGenegunState.Inject,
            _ => ImperialFishGenegunState.Extract,
        };
        _appearance.SetData(gun, ImperialFishGenegunVisuals.State, state);
    }

    /// <summary>attack_self(): разрядить.</summary>
    private void OnGenegunUseInHand(Entity<ImperialFishGenegunComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (GetLoaded(ent) == null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-empty"), ent, args.User);
            return;
        }

        if (_slots.TryGetSlot(ent, ImperialFishGenegunComponent.SlotId, out var slot))
            _slots.TryEjectToHands(ent, slot, args.User, true);
        _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-unloaded"), ent, args.User);
    }

    private void OnGenegunAfterInteract(Entity<ImperialFishGenegunComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<ImperialFishComponent>(target, out var fish))
            return;

        args.Handled = true;
        if (GetLoaded(ent) is not { } loaded)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-empty"), ent, args.User);
            return;
        }

        if (!CanUseOn(ent, (target, fish), args.User))
            return;

        if (TryComp<ImperialFishGeneComponent>(loaded, out var gene))
        {
            Inject((loaded, gene), (target, fish), args.User, ent);
            return;
        }

        if (fish.Traits.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-no-traits", ("fish", target)), target, args.User);
            return;
        }

        // tgui_input_list «Choose a trait to extract».
        var traits = new List<ImperialFishGenegunTrait>();
        foreach (var traitId in fish.Traits)
        {
            if (_proto.TryIndex(traitId, out var trait))
                traits.Add(new ImperialFishGenegunTrait { Id = traitId.Id, Name = Loc.GetString(trait.Name) });
        }

        _ui.OpenUi(ent.Owner, ImperialFishGenegunUiKey.Key, args.User);
        _ui.SetUiState(ent.Owner, ImperialFishGenegunUiKey.Key, new ImperialFishGenegunBoundUserInterfaceState(GetNetEntity(target), traits));
    }

    private bool CanUseOn(EntityUid tool, Entity<ImperialFishComponent> fish, EntityUid user)
    {
        if (IsHolo(fish))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-incompatible", ("fish", fish.Owner)), fish, user);
            return false;
        }

        if (fish.Comp.Dead)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-dead", ("fish", fish.Owner)), fish, user);
            return false;
        }

        return true;
    }

    private void OnExtractMessage(Entity<ImperialFishGenegunComponent> ent, ref ImperialFishGenegunExtractMessage args)
    {
        _ui.CloseUi(ent.Owner, ImperialFishGenegunUiKey.Key, args.Actor);
        var fishUid = GetEntity(args.Fish);
        if (!TryComp<ImperialFishComponent>(fishUid, out var fish) || !_hands.IsHolding(args.Actor, ent.Owner)
            || !_interaction.InRangeUnobstructed(args.Actor, fishUid))
            return;

        Extract(ent, (fishUid, fish), args.Trait, args.Actor);
    }

    private void Extract(EntityUid gun, Entity<ImperialFishComponent> fish, string trait, EntityUid user)
    {
        if (TerminatingOrDeleted(fish) || !CanUseOn(gun, fish, user))
            return;

        if (GetLoaded(gun) is not { } syringe || HasComp<ImperialFishGeneComponent>(syringe))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-no-syringe"), gun, user);
            return;
        }

        if (!_traits.Has(fish.Comp, trait))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-trait-gone", ("fish", fish.Owner)), fish, user);
            return;
        }

        QueueDel(syringe);
        _traits.Remove(fish, trait);

        var gene = Spawn(GeneProto, Transform(gun).Coordinates);
        SetTrait(gene, trait);
        if (_slots.TryGetSlot(gun, ImperialFishGenegunComponent.SlotId, out var slot))
        {
            _slots.SetLock(gun, slot, false);
            _slots.TryInsert(gun, slot, gene, null);
        }

        _popup.PopupEntity(Loc.GetString("imperial-fish-genegun-extracted", ("trait", TraitName(trait)), ("fish", fish.Owner)), fish, user);
        Genegunned(fish);
        UpdateVisuals(gun);
    }

    #endregion

    #region Инъектор

    private void OnGeneMapInit(Entity<ImperialFishGeneComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Trait != null)
            SetTrait(ent, ent.Comp.Trait);
    }

    private void SetTrait(EntityUid gene, string trait)
    {
        var comp = EnsureComp<ImperialFishGeneComponent>(gene);
        comp.Trait = trait;
        Dirty(gene, comp);
        _meta.SetEntityName(gene, Loc.GetString("imperial-fish-gene-name", ("trait", TraitName(trait))));
    }

    private string TraitName(string trait)
    {
        return _proto.TryIndex<ImperialFishTraitPrototype>(trait, out var proto) ? Loc.GetString(proto.Name) : trait;
    }

    private void OnGeneAfterInteract(Entity<ImperialFishGeneComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<ImperialFishComponent>(target, out var fish))
            return;

        args.Handled = true;
        if (!CanUseOn(ent, (target, fish), args.User))
            return;

        Inject(ent, (target, fish), args.User, ent);
    }

    /// <summary>inject_into_fish().</summary>
    private void Inject(Entity<ImperialFishGeneComponent> gene, Entity<ImperialFishComponent> fish, EntityUid user, EntityUid tool)
    {
        if (gene.Comp.Trait is not { } trait || !_traits.Apply(fish, trait, false))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-gene-cannot", ("trait", TraitName(gene.Comp.Trait ?? "?")), ("fish", fish.Owner)), fish, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-fish-gene-injected", ("trait", TraitName(trait)), ("fish", fish.Owner)), fish, user);
        QueueDel(gene);
        Genegunned(fish);
        _audio.PlayPvs(HypospraySound, fish);
        if (tool != gene.Owner)
            UpdateVisuals(tool);
    }

    private void Genegunned(Entity<ImperialFishComponent> fish)
    {
        if (fish.Comp.Genegunned)
            _fish.SetDead(fish, true);

        fish.Comp.Genegunned = true;
        Dirty(fish);
    }

    #endregion
}
