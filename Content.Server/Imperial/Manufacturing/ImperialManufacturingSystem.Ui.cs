using Robust.Server.GameObjects;
using System.Linq;
using Content.Shared.Imperial.Manufacturing;
using Content.Shared.Kitchen;
using Content.Shared.Research.Prototypes;

namespace Content.Server.Imperial.Manufacturing;

public sealed partial class ImperialManufacturingSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string SelectDesign = "design";
    private const string SelectRecipe = "recipe";
    private const string SelectFilter = "filter";

    /// <summary>Длина текста фильтра name_contains (tgui_input_text max_length).</summary>
    private const int NameFilterLength = 12;

    private static readonly ImperialSorterFilterType[] FilterTypes =
    {
        ImperialSorterFilterType.IsStack,
        ImperialSorterFilterType.IsOre,
        ImperialSorterFilterType.IsMail,
        ImperialSorterFilterType.NameContains,
        ImperialSorterFilterType.SpecificItem,
        ImperialSorterFilterType.SpecificKind,
    };

    private void InitializeUi()
    {
        SubscribeLocalEvent<ImperialManufacturingComponent, ImperialManufacturingSelectMessage>(OnSelect);
        SubscribeLocalEvent<ImperialManufacturingComponent, ImperialSorterActionMessage>(OnSorterAction);
        SubscribeLocalEvent<ImperialManufacturingComponent, ImperialSorterTextMessage>(OnSorterText);
    }

    /// <summary>tgui_input_list().</summary>
    private void OpenSelect(EntityUid uid, EntityUid user, string purpose, string title, IEnumerable<(string Id, string Name)> options)
    {
        var list = options
            .Select(o => new ImperialManufacturingSelectOption { Id = o.Id, Name = o.Name })
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _ui.OpenUi(uid, ImperialManufacturingUiKey.Select, user);
        _ui.SetUiState(uid, ImperialManufacturingUiKey.Select, new ImperialManufacturingSelectState(title, purpose, list));
    }

    private void OnSelect(Entity<ImperialManufacturingComponent> ent, ref ImperialManufacturingSelectMessage args)
    {
        var id = args.Id;
        var user = args.Actor;
        _ui.CloseUi(ent.Owner, ImperialManufacturingUiKey.Select, user);

        switch (args.Purpose)
        {
            case SelectDesign when ent.Comp.Kind == ImperialManufacturingKind.Lathe:
                if (!LatheDesigns().Any(d => d.ID == id))
                    return;
                ent.Comp.DesignId = args.Id;
                ent.Comp.BusyUntil = null;
                break;
            case SelectRecipe when ent.Comp.Kind == ImperialManufacturingKind.Crafter:
            {
                var valid = ent.Comp.Cooking
                    ? _proto.HasIndex<FoodRecipePrototype>(args.Id)
                    : CraftRecipes().Any(r => r.Id == id);
                if (!valid || ent.Comp.Recipe == args.Id)
                    return;
                ent.Comp.Recipe = args.Id;
                ent.Comp.CraftUntil = null;
                break;
            }
            case SelectFilter when ent.Comp.Kind == ImperialManufacturingKind.Sorter:
            {
                if (ent.Comp.Filters.Count >= ent.Comp.MaxFilters || !Enum.TryParse<ImperialSorterFilterType>(args.Id, out var type))
                    return;
                ent.Comp.Filters.Add(new ImperialSorterFilter { Type = type, Id = ent.Comp.NextFilterId++ });
                UpdateSorterUi(ent);
                OpenSorter(ent, user);
                return;
            }
            default:
                return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-manufacturing-set"), ent, user);
    }

    #region Сортировщик

    private void OpenSorter(Entity<ImperialManufacturingComponent> ent, EntityUid user)
    {
        EnsureFilterIds(ent);
        _ui.OpenUi(ent.Owner, ImperialManufacturingUiKey.Sorter, user);
        UpdateSorterUi(ent);
    }

    /// <summary>Фильтры, заданные маппером, получают номера.</summary>
    private void EnsureFilterIds(Entity<ImperialManufacturingComponent> ent)
    {
        foreach (var filter in ent.Comp.Filters)
        {
            if (filter.Id == 0 && ent.Comp.Filters.Count(f => f.Id == 0) > 1 || filter.Id < ent.Comp.NextFilterId && ent.Comp.Filters.Count(f => f.Id == filter.Id) > 1)
                filter.Id = ent.Comp.NextFilterId++;
            ent.Comp.NextFilterId = Math.Max(ent.Comp.NextFilterId, filter.Id + 1);
        }
    }

    public void UpdateSorterUi(Entity<ImperialManufacturingComponent> ent)
    {
        var filters = ent.Comp.Filters.Select(f => new ImperialSorterFilterData
        {
            Id = f.Id,
            Name = FilterName(f),
            Inverted = f.Inverted,
            Dir = f.Dir,
            TextValue = f.Type == ImperialSorterFilterType.NameContains ? f.Value : null,
        }).ToList();

        _ui.SetUiState(ent.Owner, ImperialManufacturingUiKey.Sorter, new ImperialSorterState(filters, ent.Comp.DirIfNotMet));
    }

    /// <summary>return_name().</summary>
    private string FilterName(ImperialSorterFilter filter)
    {
        if (filter.Listening)
            return Loc.GetString("imperial-sorter-filter-awaiting");

        return filter.Type switch
        {
            ImperialSorterFilterType.NameContains => Loc.GetString("imperial-sorter-filter-name-contains-value", ("value", filter.Value)),
            ImperialSorterFilterType.SpecificItem or ImperialSorterFilterType.SpecificKind when filter.ValueName != string.Empty => filter.ValueName,
            _ => FilterTypeName(filter.Type),
        };
    }

    private string FilterTypeName(ImperialSorterFilterType type)
    {
        return Loc.GetString(type switch
        {
            ImperialSorterFilterType.IsStack => "imperial-sorter-filter-is-stack",
            ImperialSorterFilterType.IsOre => "imperial-sorter-filter-is-ore",
            ImperialSorterFilterType.IsMail => "imperial-sorter-filter-is-mail",
            ImperialSorterFilterType.NameContains => "imperial-sorter-filter-name-contains",
            ImperialSorterFilterType.SpecificItem => "imperial-sorter-filter-specific-item",
            _ => "imperial-sorter-filter-specific-kind",
        });
    }

    /// <summary>GLOB.cardinals: N, S, E, W.</summary>
    private static Direction NextCardinal(Direction dir) => dir switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.East,
        Direction.East => Direction.West,
        _ => Direction.North,
    };

    private void OnSorterAction(Entity<ImperialManufacturingComponent> ent, ref ImperialSorterActionMessage args)
    {
        if (ent.Comp.Kind != ImperialManufacturingKind.Sorter)
            return;

        var id = args.Id;
        var filter = ent.Comp.Filters.FirstOrDefault(f => f.Id == id);
        switch (args.Action)
        {
            case ImperialSorterAction.NewFilter:
                if (ent.Comp.Filters.Count >= ent.Comp.MaxFilters)
                    return;
                OpenSelect(ent, args.Actor, SelectFilter, Loc.GetString("imperial-sorter-new-filter-title"),
                    FilterTypes.Select(t => (t.ToString(), FilterTypeName(t))));
                return;
            case ImperialSorterAction.DelFilter:
                if (filter == null)
                    return;
                ent.Comp.Filters.Remove(filter);
                break;
            case ImperialSorterAction.Rotate:
                if (filter == null)
                    return;
                filter.Dir = NextCardinal(filter.Dir);
                break;
            case ImperialSorterAction.RotateUnmet:
                ent.Comp.DirIfNotMet = NextCardinal(ent.Comp.DirIfNotMet);
                break;
            case ImperialSorterAction.Edit:
                if (filter == null)
                    return;
                EditFilter(ent, filter, args.Actor);
                break;
            case ImperialSorterAction.ShiftUp:
            case ImperialSorterAction.ShiftDown:
            {
                if (filter == null)
                    return;
                var count = ent.Comp.Filters.Count;
                var index = ent.Comp.Filters.IndexOf(filter);
                var next = ((index + (args.Action == ImperialSorterAction.ShiftUp ? -1 : 1)) % count + count) % count;
                ent.Comp.Filters.RemoveAt(index);
                ent.Comp.Filters.Insert(next, filter);
                break;
            }
        }

        UpdateSorterUi(ent);
    }

    /// <summary>edit() фильтров.</summary>
    private void EditFilter(Entity<ImperialManufacturingComponent> ent, ImperialSorterFilter filter, EntityUid user)
    {
        switch (filter.Type)
        {
            case ImperialSorterFilterType.SpecificItem:
            case ImperialSorterFilterType.SpecificKind:
                if (!filter.Listening)
                {
                    foreach (var other in ent.Comp.Filters)
                    {
                        other.Listening = false;
                    }

                    filter.Listening = true;
                    filter.ValueName = string.Empty;
                    _popup.PopupEntity(Loc.GetString("imperial-manufacturing-sorter-awaiting"), ent, user);
                }
                else
                {
                    filter.Listening = false;
                }

                break;
            case ImperialSorterFilterType.NameContains:
                // Текст приходит отдельным сообщением из окна.
                break;
            default:
                _popup.PopupEntity(Loc.GetString("imperial-manufacturing-sorter-not-editable"), ent, user);
                break;
        }
    }

    private void OnSorterText(Entity<ImperialManufacturingComponent> ent, ref ImperialSorterTextMessage args)
    {
        var id = args.Id;
        if (ent.Comp.Filters.FirstOrDefault(f => f.Id == id) is not { Type: ImperialSorterFilterType.NameContains } filter)
            return;

        var text = args.Text.Trim();
        filter.Value = text.Length > NameFilterLength ? text[..NameFilterLength] : text;
        UpdateSorterUi(ent);
    }

    #endregion
}
