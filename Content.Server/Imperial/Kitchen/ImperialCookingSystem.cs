using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Kitchen;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Kitchen;

/// <summary>
/// Приборы кухни TG: духовка (bakeable), сковорода-гриль (grillable), плита с кастрюлей (супы),
/// процессор и обработка инструментами (processable).
/// </summary>
public sealed partial class ImperialCookingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ImperialFoodQualitySystem _quality = default!;

    /// <summary>/obj/item/food/badrecipe — «сгоревшее месиво».</summary>
    public static readonly EntProtoId BadRecipe = "FoodBadRecipe";

    private TimeSpan _nextTick;

    /// <summary>SSmachines: раз в 2 секунды.</summary>
    private const float TickSeconds = 2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialOvenComponent, MapInitEvent>(OnOvenMapInit);
        SubscribeLocalEvent<ImperialOvenComponent, ActivateInWorldEvent>(OnOvenActivate);

        SubscribeLocalEvent<ImperialGriddleComponent, ActivateInWorldEvent>(OnGriddleActivate);

        SubscribeLocalEvent<ImperialStoveComponent, MapInitEvent>(OnStoveMapInit);
        SubscribeLocalEvent<ImperialStoveComponent, GetVerbsEvent<AlternativeVerb>>(OnStoveVerbs);

        SubscribeLocalEvent<ImperialSoupPotComponent, ComponentInit>(OnPotInit);
        SubscribeLocalEvent<ImperialSoupPotComponent, AfterInteractUsingEvent>(OnPotInteractUsing);
        SubscribeLocalEvent<ImperialSoupPotComponent, GetVerbsEvent<AlternativeVerb>>(OnPotVerbs);

        SubscribeLocalEvent<ImperialFoodProcessorComponent, ComponentInit>(OnProcessorInit);
        SubscribeLocalEvent<ImperialFoodProcessorComponent, InteractUsingEvent>(OnProcessorInteractUsing);
        SubscribeLocalEvent<ImperialFoodProcessorComponent, ActivateInWorldEvent>(OnProcessorActivate);

        SubscribeLocalEvent<ImperialProcessableComponent, InteractUsingEvent>(OnProcessableInteractUsing);
        SubscribeLocalEvent<ImperialProcessableComponent, ImperialProcessDoAfterEvent>(OnProcessDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateProcessors();

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TimeSpan.FromSeconds(TickSeconds);
        UpdateOvens();
        UpdateGriddles();
        UpdateStoves();
    }

    #region Общее

    /// <summary>Превращение готовящегося предмета: реагенты переносятся, хорошее блюдо — рукотворное.</summary>
    public EntityUid Cook(EntityUid item, EntProtoId result, bool positive, BaseContainer? container)
    {
        var made = Spawn(result, Transform(item).Coordinates);
        if (_solutions.TryGetSolution(item, "food", out _, out var from) &&
            _solutions.TryGetSolution(made, "food", out var toEnt, out _))
        {
            _solutions.RemoveAllSolution(toEnt.Value);
            _solutions.TryAddSolution(toEnt.Value, from.Clone());
        }

        if (positive)
            _quality.MarkHandmade(made);

        Del(item);
        if (container != null)
            _containers.Insert(made, container);

        return made;
    }

    private void Smell(EntityUid source, bool positive)
    {
        _popup.PopupEntity(Loc.GetString(positive ? "imperial-cooking-smell-good" : "imperial-cooking-smell-bad", ("machine", source)),
            source, positive ? PopupType.Small : PopupType.SmallCaution);
    }

    #endregion

    #region Духовка

    private void OnOvenMapInit(Entity<ImperialOvenComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.StartingTray is not { } tray ||
            !_slots.TryGetSlot(ent, ImperialOvenComponent.TraySlot, out var slot) || slot.HasItem)
            return;

        // Слот закрыт, пока закрыта дверца: противень кладётся при открытом замке.
        _slots.SetLock(ent, slot, false);
        _slots.TryInsert(ent, slot, Spawn(tray, Transform(ent).Coordinates), null);
        _slots.SetLock(ent, slot, !ent.Comp.Open);
        UpdateOvenVisuals(ent);
    }

    /// <summary>attack_hand: открыть или закрыть дверцу.</summary>
    private void OnOvenActivate(Entity<ImperialOvenComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        ent.Comp.Open = !ent.Comp.Open;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.Open ? ent.Comp.OpenSound : ent.Comp.CloseSound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Open ? "imperial-oven-open" : "imperial-oven-close", ("oven", ent.Owner)), ent, args.User);

        if (_slots.TryGetSlot(ent, ImperialOvenComponent.TraySlot, out var slot))
            _slots.SetLock(ent, slot, !ent.Comp.Open);

        UpdateOvenVisuals(ent);
    }

    private EntityUid? OvenTray(EntityUid oven)
    {
        return _slots.TryGetSlot(oven, ImperialOvenComponent.TraySlot, out var slot) ? slot.Item : null;
    }

    private bool OvenActive(Entity<ImperialOvenComponent> ent)
    {
        return !ent.Comp.Open && _power.IsPowered(ent.Owner) && OvenTray(ent) is { } tray &&
               TryComp<StorageComponent>(tray, out var storage) && storage.Container.ContainedEntities.Count > 0;
    }

    private void UpdateOvenVisuals(Entity<ImperialOvenComponent> ent)
    {
        _appearance.SetData(ent, ImperialKitchenVisuals.Active, OvenActive(ent));
        _appearance.SetData(ent, ImperialKitchenVisuals.Open, ent.Comp.Open);
    }

    /// <summary>oven/process: всё на противне запекается или подгорает.</summary>
    private void UpdateOvens()
    {
        var query = EntityQueryEnumerator<ImperialOvenComponent>();
        while (query.MoveNext(out var uid, out var oven))
        {
            var ent = (uid, oven);
            UpdateOvenVisuals(ent);
            if (!OvenActive(ent) || OvenTray(uid) is not { } tray || !TryComp<StorageComponent>(tray, out var storage))
                continue;

            foreach (var item in storage.Container.ContainedEntities.ToArray())
            {
                if (!TryComp<ImperialBakeableComponent>(item, out var bake))
                {
                    // make_bakeable по умолчанию: любая еда сгорает в месиво.
                    if (!HasComp<EdibleComponent>(item))
                        continue;

                    bake = EnsureComp<ImperialBakeableComponent>(item);
                    if (_quality.GetData(item) is { Bake: { } result } data)
                    {
                        bake.Result = result;
                        bake.Time = data.BakeTime;
                    }
                    else
                    {
                        bake.Result = BadRecipe;
                        bake.Time = _random.NextFloat(25f, 40f);
                        bake.Positive = false;
                    }
                }

                bake.Progress += TickSeconds;
                if (bake.Progress < bake.Time)
                    continue;

                Cook(item, bake.Result, bake.Positive, storage.Container);
                Smell(uid, bake.Positive);
            }
        }
    }

    #endregion

    #region Сковорода-гриль

    private void OnGriddleActivate(Entity<ImperialGriddleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (!ent.Comp.On && ent.Comp.NeedsPower && !_power.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("imperial-cooking-no-power"), ent, args.User);
            return;
        }

        ent.Comp.On = !ent.Comp.On;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.ToggleSound, ent);
        _appearance.SetData(ent, ImperialKitchenVisuals.Active, ent.Comp.On);
    }

    /// <summary>griddle/process: всё сверху жарится (grillable), прочая еда подгорает.</summary>
    private void UpdateGriddles()
    {
        var query = EntityQueryEnumerator<ImperialGriddleComponent, ItemPlacerComponent>();
        while (query.MoveNext(out var uid, out var griddle, out var placer))
        {
            if (!griddle.On)
                continue;

            if (griddle.NeedsPower && !_power.IsPowered(uid))
            {
                griddle.On = false;
                Dirty(uid, griddle);
                _appearance.SetData(uid, ImperialKitchenVisuals.Active, false);
                continue;
            }

            foreach (var item in placer.PlacedEntities.ToArray())
            {
                if (!Exists(item))
                    continue;

                if (!TryComp<ImperialGrillableComponent>(item, out var grill))
                {
                    if (!HasComp<EdibleComponent>(item))
                        continue;

                    grill = EnsureComp<ImperialGrillableComponent>(item);
                    if (_quality.GetData(item) is { Grill: { } result } data)
                    {
                        grill.Result = result;
                        grill.Time = data.GrillTime;
                    }
                    else
                    {
                        grill.Result = BadRecipe;
                        grill.Time = _random.NextFloat(20f, 30f);
                        grill.Positive = false;
                    }
                }

                grill.Progress += TickSeconds;
                if (grill.Progress < grill.Time)
                    continue;

                var made = Cook(item, grill.Result, grill.Positive, null);
                _transform.SetCoordinates(made, Transform(uid).Coordinates);
                Smell(uid, grill.Positive);
            }
        }
    }

    #endregion
}
