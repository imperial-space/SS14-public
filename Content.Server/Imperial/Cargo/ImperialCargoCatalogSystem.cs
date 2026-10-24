using Content.Server.Cargo.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Emag.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Каталог карго TG (code/modules/cargo/packs): ванильный рынок «market» у станции и консолей заменяется
/// на рынок наборов TG и товаров Imperial. Контрабанда (ORDER_CONTRABAND) и наборы только для взлома
/// (ORDER_EMAG_ONLY) открываются на взломанной консоли, как contraband/EMAGGED у /obj/machinery/computer/cargo.
/// </summary>
public sealed class ImperialCargoCatalogSystem : EntitySystem
{
    [Dependency] private readonly ImperialPrivateOrderSystem _privateOrders = default!;

    public static readonly ProtoId<CargoMarketPrototype> VanillaMarket = "market";
    public static readonly ProtoId<CargoMarketPrototype> Market = "ImperialCargo";
    public static readonly ProtoId<CargoMarketPrototype> Contraband = "ImperialCargoContraband";
    public static readonly ProtoId<CargoMarketPrototype> Emag = "ImperialCargoEmag";
    public static readonly ProtoId<CargoMarketPrototype> Pod = "ImperialCargoPod";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCargoOrderDatabaseComponent, ComponentStartup>(OnDatabaseStartup);
        SubscribeLocalEvent<CargoOrderConsoleComponent, ComponentStartup>(OnConsoleStartup);
        SubscribeLocalEvent<EmaggedComponent, ComponentStartup>(OnEmagged);
    }

    private void OnDatabaseStartup(Entity<StationCargoOrderDatabaseComponent> ent, ref ComponentStartup args)
    {
        var markets = ent.Comp.Markets;
        if (!markets.Remove(VanillaMarket))
            return;

        foreach (var market in new[] { Market, Contraband, Emag, Pod })
        {
            if (!markets.Contains(market))
                markets.Add(market);
        }
    }

    private void OnConsoleStartup(Entity<CargoOrderConsoleComponent> ent, ref ComponentStartup args)
    {
        var groups = Groups(ent.Comp);
        if (!groups.Remove(VanillaMarket))
            return;

        _privateOrders.Setup(ent);

        groups.Add(Market);
        if (HasComp<EmaggedComponent>(ent))
            AddHacked(groups);

        Dirty(ent);
    }

    private void OnEmagged(Entity<EmaggedComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<CargoOrderConsoleComponent>(ent, out var console) || !Groups(console).Contains(Market))
            return;

        AddHacked(Groups(console));
        Dirty(ent.Owner, console);
    }

    private static readonly System.Reflection.FieldInfo AllowedGroupsField =
        typeof(CargoOrderConsoleComponent).GetField(nameof(CargoOrderConsoleComponent.AllowedGroups))!;

    /// <summary>Поле закрыто [Access] ванильной системы карго.</summary>
    private static List<ProtoId<CargoMarketPrototype>> Groups(CargoOrderConsoleComponent console)
    {
        return (List<ProtoId<CargoMarketPrototype>>) AllowedGroupsField.GetValue(console)!;
    }

    private static void AddHacked(List<ProtoId<CargoMarketPrototype>> groups)
    {
        if (!groups.Contains(Contraband))
            groups.Add(Contraband);
        if (!groups.Contains(Emag))
            groups.Add(Emag);
    }
}
