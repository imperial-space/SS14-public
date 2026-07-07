using Content.Shared.Cargo;
using Content.Client.Cargo.UI;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.IdentityManagement;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Cargo.BUI
{
    public sealed class CargoOrderConsoleBoundUserInterface : BoundUserInterface
    {
        private readonly SharedCargoSystem _cargoSystem;

        [ViewVariables]
        private CargoConsoleMenu? _menu;

        /// <summary>
        /// This is the separate popup window for individual orders.
        /// </summary>
        [ViewVariables]
        private CargoConsoleOrderMenu? _orderMenu;

        [ViewVariables]
        public string? AccountName { get; private set; }

        [ViewVariables]
        public int BankBalance { get; private set; }

        [ViewVariables]
        public int OrderCapacity { get; private set; }

        [ViewVariables]
        public int OrderCount { get; private set; }

        /// <summary>
        /// Currently selected product
        /// </summary>
        [ViewVariables]
        // Imperial Weekly Mode
        private string _productId = string.Empty;

        public CargoOrderConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _cargoSystem = EntMan.System<SharedCargoSystem>();
        }

        protected override void Open()
        {
            base.Open();

            var spriteSystem = EntMan.System<SpriteSystem>();
            var dependencies = IoCManager.Instance!;
            _menu = new CargoConsoleMenu(Owner, EntMan, dependencies.Resolve<IPrototypeManager>(), spriteSystem);
            var localPlayer = dependencies.Resolve<IPlayerManager>().LocalEntity;
            var description = new FormattedMessage();

            string orderRequester;

            if (EntMan.EntityExists(localPlayer))
                orderRequester = Identity.Name(localPlayer.Value, EntMan);
            else
                orderRequester = string.Empty;

            _orderMenu = new CargoConsoleOrderMenu();

            _menu.OnClose += Close;

            _menu.OnItemSelected += (row) =>
            {
                if (row == null)
                    return;

                description.Clear();
                description.PushColor(Color.White); // Rich text default color is grey
                if (row.MainButton.ToolTip != null)
                    description.AddText(row.MainButton.ToolTip);

                _orderMenu.Description.SetMessage(description);
                // Imperial Weekly Mode: Original code removed:
                // _product = row.Product;
                // Imperial Weekly Mode
                _productId = row.ProductId;
                _orderMenu.ProductName.Text = row.ProductName.Text;
                _orderMenu.PointCost.Text = row.PointCost.Text;
                _orderMenu.Requester.Text = orderRequester;
                _orderMenu.Reason.Text = "";
                _orderMenu.Amount.Value = 1;

                _orderMenu.OpenCentered();
            };
            _menu.OnOrderApproved += ApproveOrder;
            _menu.OnOrderCanceled += RemoveOrder;
            _orderMenu.SubmitButton.OnPressed += (_) =>
            {
                if (AddOrder())
                {
                    _orderMenu.Close();
                }
            };

            _menu.OnAccountAction += (account, amount) =>
            {
                SendMessage(new CargoConsoleWithdrawFundsMessage(account, amount));
            };

            _menu.OnToggleUnboundedLimit += _ =>
            {
                SendMessage(new CargoConsoleToggleLimitMessage());
            };

            _menu.OpenCentered();
        }

        private void Populate(List<CargoOrderData> orders)
        {
            if (_menu == null)
                return;

            // Imperial Weekly Mode: Original code moved below PopulateCategories:
            // _menu.PopulateProducts();
            _menu.PopulateCategories();
            // Imperial Weekly Mode
            _menu.PopulateProducts();
            _menu.PopulateOrders(orders);
            _menu.PopulateAccountActions();
            // Imperial Weekly Mode
            RefreshSelectedProduct();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not CargoConsoleInterfaceState cState || !EntMan.TryGetComponent<CargoOrderConsoleComponent>(Owner, out var orderConsole))
                return;
            var station = EntMan.GetEntity(cState.Station);

            OrderCapacity = cState.Capacity;
            OrderCount = cState.Count;
            BankBalance = _cargoSystem.GetBalanceFromAccount(station, orderConsole.Account);

            AccountName = cState.Name;

            if (_menu == null)
                return;

            _menu.ProductCatalogue = cState.Products;
            // Imperial Weekly Mode Start
            _menu.WeeklyProductCatalogue = cState.WeeklyProducts;
            _menu.InvalidateProductCache();
            // Imperial Weekly Mode End

            _menu?.UpdateStation(station);
            Populate(cState.Orders);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
                return;

            _menu?.Dispose();
            _orderMenu?.Dispose();
        }

        private bool AddOrder()
        {
            var orderAmt = _orderMenu?.Amount.Value ?? 0;
            if (orderAmt < 1 || orderAmt > OrderCapacity)
            {
                return false;
            }

            SendMessage(new CargoConsoleAddOrderMessage(
                _orderMenu?.Requester.Text ?? "",
                _orderMenu?.Reason.Text ?? "",
                // Imperial Weekly Mode: Original code removed:
                // _product?.ID ?? "",
                _productId,
                orderAmt));

            return true;
        }

        // Imperial Weekly Mode
        private void RefreshSelectedProduct()
        {
            if (_menu == null ||
                _orderMenu == null ||
                string.IsNullOrEmpty(_productId))
            {
                return;
            }

            if (!_menu.TryGetProductDisplayData(_productId, out var name, out var descriptionText, out var cost))
            {
                _productId = string.Empty;
                _orderMenu.Close();
                return;
            }

            var description = new FormattedMessage();
            description.PushColor(Color.White); // Rich text default color is grey
            description.AddText(descriptionText);

            _orderMenu.Description.SetMessage(description);
            _orderMenu.ProductName.Text = name;
            _orderMenu.PointCost.Text = Loc.GetString("cargo-console-menu-points-amount", ("amount", cost.ToString()));
        }

        private void RemoveOrder(CargoOrderData? order)
        {
            if (order == null)
                return;

            SendMessage(new CargoConsoleRemoveOrderMessage(order.OrderId));
        }

        private void ApproveOrder(CargoOrderData? order)
        {
            if (order == null)
                return;

            if (OrderCount >= OrderCapacity)
                return;

            SendMessage(new CargoConsoleApproveOrderMessage(order.OrderId));
        }
    }
}
