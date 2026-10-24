using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Server.Construction.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Delivery;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Vending;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Vending;

/// <summary>Машины сервиса из торгоматов TG: дисковод для посуды (dish_drive) и сортировщик почты (mailsorter).</summary>
public sealed class ImperialServiceMachinesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedDisposalUnitSystem _disposal = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private static readonly TimeSpan SuckInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialDishDriveComponent, ComponentStartup>(OnDriveStartup);
        SubscribeLocalEvent<ImperialDishDriveComponent, MapInitEvent>(OnDriveMapInit);
        SubscribeLocalEvent<ImperialDishDriveComponent, InteractUsingEvent>(OnDriveInteractUsing);
        SubscribeLocalEvent<ImperialDishDriveComponent, InteractHandEvent>(OnDriveInteractHand);
        SubscribeLocalEvent<ImperialDishDriveComponent, ExaminedEvent>(OnDriveExamined);
        SubscribeLocalEvent<ImperialDishDriveComponent, GetVerbsEvent<AlternativeVerb>>(OnDriveVerbs);
        SubscribeLocalEvent<ImperialDishDriveBoardComponent, UseInHandEvent>(OnBoardUse);
        SubscribeLocalEvent<ImperialDishDriveBoardComponent, GetVerbsEvent<AlternativeVerb>>(OnBoardVerbs);
        SubscribeLocalEvent<ImperialDishDriveBoardComponent, ExaminedEvent>(OnBoardExamined);

        SubscribeLocalEvent<ImperialMailSorterComponent, ComponentStartup>(OnSorterStartup);
        SubscribeLocalEvent<ImperialMailSorterComponent, InteractUsingEvent>(OnSorterInteractUsing);
        SubscribeLocalEvent<ImperialMailSorterComponent, InteractHandEvent>(OnSorterInteractHand);
        SubscribeLocalEvent<ImperialMailSorterComponent, ExaminedEvent>(OnSorterExamined);
        SubscribeLocalEvent<ImperialMailSorterComponent, GetVerbsEvent<AlternativeVerb>>(OnSorterVerbs);
    }

    #region Дисковод для посуды

    private void OnDriveStartup(Entity<ImperialDishDriveComponent> ent, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    /// <summary>RefreshParts(): настройки берутся с платы.</summary>
    private void OnDriveMapInit(Entity<ImperialDishDriveComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTransmit = _timing.CurTime + ent.Comp.TransmitInterval;
        if (!TryComp<MachineComponent>(ent, out var machine))
            return;

        foreach (var board in machine.BoardContainer.ContainedEntities)
        {
            if (!TryComp<ImperialDishDriveBoardComponent>(board, out var settings))
                continue;

            ent.Comp.Suction = settings.Suction;
            ent.Comp.Transmit = settings.Transmit;
        }
    }

    private bool Matches(EntityUid uid, List<EntProtoId> list)
    {
        if (MetaData(uid).EntityPrototype is not { } proto)
            return false;

        if (list.Any(id => id == proto.ID))
            return true;

        foreach (var parent in _proto.EnumerateAllParents<EntityPrototype>(proto.ID))
        {
            if (list.Any(id => id == parent.id))
                return true;
        }

        return false;
    }

    /// <summary>Посуда без содержимого (reagents.total_volume == 0, contents пусты).</summary>
    private bool IsCollectable(Entity<ImperialDishDriveComponent> ent, EntityUid dish)
    {
        if (!Matches(dish, ent.Comp.Collectable) || _container.IsEntityInContainer(dish))
            return false;

        if (TryComp<SolutionContainerManagerComponent>(dish, out var manager))
        {
            foreach (var (_, soln) in _solutions.EnumerateSolutions((dish, manager)))
            {
                if (soln.Comp.Solution.Volume > 0)
                    return false;
            }
        }

        return true;
    }

    private void Store(Entity<ImperialDishDriveComponent> ent, EntityUid dish)
    {
        if (!_container.Insert(dish, _container.GetContainer(ent, ent.Comp.Container)))
            return;

        _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    private void OnDriveInteractUsing(Entity<ImperialDishDriveComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !Matches(args.Used, ent.Comp.Collectable))
            return;

        args.Handled = true;
        Store(ent, args.Used);
        _popup.PopupEntity(Loc.GetString("imperial-dish-drive-placed", ("item", args.Used)), ent, args.User);
    }

    /// <summary>attack_hand(): достать последнюю положенную посуду.</summary>
    private void OnDriveInteractHand(Entity<ImperialDishDriveComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var container = _container.GetContainer(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-dish-drive-empty"), ent, args.User);
            return;
        }

        var dish = container.ContainedEntities[^1];
        _container.Remove(dish, container);
        _hands.PickupOrDrop(args.User, dish);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("imperial-dish-drive-taken", ("item", dish)), ent, args.User);
    }

    private void OnDriveExamined(Entity<ImperialDishDriveComponent> ent, ref ExaminedEvent args)
    {
        var container = _container.GetContainer(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count == 0)
        {
            args.PushMarkup(Loc.GetString("imperial-dish-drive-examine-empty"));
            return;
        }

        var list = container.ContainedEntities
            .GroupBy(e => Name(e))
            .Select(g => $"{g.Count()} × {g.Key}");
        args.PushMarkup(Loc.GetString("imperial-dish-drive-examine", ("list", string.Join(", ", list))));
    }

    private void OnDriveVerbs(Entity<ImperialDishDriveComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-dish-drive-verb-transmit"),
            Act = () => DoTheDishes(ent, user),
        });
    }

    /// <summary>do_the_dishes(): осколки и подносы улетают в ближайший утилизатор.</summary>
    private void DoTheDishes(Entity<ImperialDishDriveComponent> ent, EntityUid? user)
    {
        var container = _container.GetContainer(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count == 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("imperial-dish-drive-empty"), ent, user.Value);
            return;
        }

        Entity<DisposalUnitComponent>? bin = null;
        foreach (var unit in _lookup.GetEntitiesInRange<DisposalUnitComponent>(Transform(ent).Coordinates, ent.Comp.DisposalRange))
        {
            bin = unit;
            break;
        }

        if (bin is not { } target)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("imperial-dish-drive-no-bin"), ent, user.Value);
            return;
        }

        var disposed = 0;
        foreach (var dish in container.ContainedEntities.ToList())
        {
            if (!Matches(dish, ent.Comp.Disposable))
                continue;

            _container.Remove(dish, container);
            _disposal.AfterInsert(target, target.Comp, dish, null, true);
            disposed++;
        }

        if (disposed > 0)
        {
            _audio.PlayPvs(ent.Comp.Sound, ent);
            _popup.PopupEntity(Loc.GetString("imperial-dish-drive-transmitted", ("count", disposed), ("bin", target.Owner)), ent);
        }
        else if (user != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-dish-drive-nothing"), ent, user.Value);
        }
    }

    private void OnBoardUse(Entity<ImperialDishDriveBoardComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Suction = !ent.Comp.Suction;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Suction ? "imperial-dish-board-suction-on" : "imperial-dish-board-suction-off"), args.User, args.User);
    }

    private void OnBoardVerbs(Entity<ImperialDishDriveBoardComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-dish-board-verb-transmit"),
            Act = () =>
            {
                ent.Comp.Transmit = !ent.Comp.Transmit;
                _popup.PopupEntity(Loc.GetString(ent.Comp.Transmit ? "imperial-dish-board-transmit-on" : "imperial-dish-board-transmit-off"), user, user);
            },
        });
    }

    private void OnBoardExamined(Entity<ImperialDishDriveBoardComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-dish-board-examine", ("suction", ent.Comp.Suction ? 1 : 0), ("transmit", ent.Comp.Transmit ? 1 : 0)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialDishDriveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drive, out var xform))
        {
            if (!xform.Anchored || !_power.IsPowered(uid))
                continue;

            var ent = (uid, drive);
            if (drive.Transmit && now >= drive.NextTransmit)
            {
                drive.NextTransmit = now + drive.TransmitInterval;
                DoTheDishes(ent, null);
            }

            if (!drive.Suction || now < drive.NextSuck)
                continue;

            drive.NextSuck = now + SuckInterval;
            var coords = xform.Coordinates;
            foreach (var dish in _lookup.GetEntitiesInRange<ItemComponent>(coords, drive.Range))
            {
                if (!IsCollectable(ent, dish))
                    continue;

                // Рядом — затягивается внутрь, дальше — подтягивается на шаг.
                if (_transform.InRange(Transform(dish).Coordinates, coords, 1.5f))
                    Store(ent, dish);
                else
                    _throwing.TryThrow(dish, coords, 3f, playSound: false);
            }
        }
    }

    #endregion

    #region Сортировщик почты

    private void OnSorterStartup(Entity<ImperialMailSorterComponent> ent, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    /// <summary>accept_check(): письма и бумага.</summary>
    private void OnSorterInteractUsing(Entity<ImperialMailSorterComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<DeliveryComponent>(args.Used) && !HasComp<PaperComponent>(args.Used))
            return;

        args.Handled = true;
        var container = _container.GetContainer(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count >= ent.Comp.Capacity)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mail-sorter-full"), ent, args.User);
            return;
        }

        if (_container.Insert(args.Used, container))
            _popup.PopupEntity(Loc.GetString("imperial-mail-sorter-inserted", ("item", args.Used)), ent, args.User);
    }

    /// <summary>Eject: выдаёт последнее письмо.</summary>
    private void OnSorterInteractHand(Entity<ImperialMailSorterComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Sorting)
            return;

        args.Handled = true;
        var container = _container.GetContainer(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mail-sorter-empty"), ent, args.User);
            return;
        }

        var mail = container.ContainedEntities[^1];
        _container.Remove(mail, container);
        _hands.PickupOrDrop(args.User, mail);
    }

    private void OnSorterExamined(Entity<ImperialMailSorterComponent> ent, ref ExaminedEvent args)
    {
        var count = _container.GetContainer(ent, ent.Comp.Container).ContainedEntities.Count;
        args.PushMarkup(Loc.GetString("imperial-mail-sorter-examine", ("count", count), ("free", ent.Comp.Capacity - count)));
    }

    private void OnSorterVerbs(Entity<ImperialMailSorterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Sorting)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-mail-sorter-verb-dump"),
            Priority = 2,
            Act = () => Dump(ent, user),
        });

        foreach (var department in _proto.EnumeratePrototypes<DepartmentPrototype>().Where(d => !d.EditorHidden).OrderBy(d => Loc.GetString(d.Name)))
        {
            var dept = department;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("imperial-mail-sorter-verb-sort", ("department", Loc.GetString(dept.Name))),
                Category = VerbCategory.SelectType,
                Act = () => StartSort(ent, user, dept),
            });
        }
    }

    /// <summary>dump_all_mail(): всё письма на пол перед сортировщиком.</summary>
    private void Dump(Entity<ImperialMailSorterComponent> ent, EntityUid user)
    {
        var container = _container.GetContainer(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mail-sorter-empty"), ent, user);
            return;
        }

        _audio.PlayPvs(ent.Comp.DenySound, ent);
        _popup.PopupEntity(Loc.GetString("imperial-mail-sorter-dumped", ("count", container.ContainedEntities.Count)), ent, user);
        _container.EmptyContainer(container, destination: Transform(ent).Coordinates);
    }

    /// <summary>sort_mail(): через 5 секунд выдаёт письма для выбранного отдела.</summary>
    private void StartSort(Entity<ImperialMailSorterComponent> ent, EntityUid user, DepartmentPrototype department)
    {
        var container = _container.GetContainer(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mail-sorter-empty"), ent, user);
            return;
        }

        ent.Comp.Sorting = true;
        _audio.PlayPvs(ent.Comp.SortSound, ent);
        var uid = ent.Owner;
        Robust.Shared.Timing.Timer.Spawn(ent.Comp.SortDelay, () =>
        {
            if (TryComp<ImperialMailSorterComponent>(uid, out var comp))
                ContinueSort((uid, comp), department);
        });
    }

    private void ContinueSort(Entity<ImperialMailSorterComponent> ent, DepartmentPrototype department)
    {
        ent.Comp.Sorting = false;
        var container = _container.GetContainer(ent, ent.Comp.Container);
        var jobs = department.Roles.Select(r => _proto.Index(r).LocalizedName).ToHashSet();
        var sorted = 0;
        var unable = 0;

        foreach (var mail in container.ContainedEntities.ToList())
        {
            if (!TryComp<DeliveryComponent>(mail, out var delivery) || string.IsNullOrEmpty(delivery.RecipientJobTitle))
            {
                unable++;
                continue;
            }

            if (!jobs.Contains(delivery.RecipientJobTitle))
                continue;

            _container.Remove(mail, container, destination: Transform(ent).Coordinates);
            sorted++;
        }

        var deptName = Loc.GetString(department.Name);
        if (sorted == 0)
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            _chat.TrySendInGameICMessage(ent, Loc.GetString("imperial-mail-sorter-none", ("department", deptName)), InGameICChatType.Speak, false);
        }
        else
        {
            _audio.PlayPvs(ent.Comp.SuccessSound, ent);
            _chat.TrySendInGameICMessage(ent, Loc.GetString("imperial-mail-sorter-sorted", ("count", sorted)), InGameICChatType.Speak, false);
        }

        if (unable > 0)
            _chat.TrySendInGameICMessage(ent, Loc.GetString("imperial-mail-sorter-unable", ("count", unable)), InGameICChatType.Speak, false);
    }

    #endregion
}
