using Content.Shared.Store;
using JetBrains.Annotations;
using System.Linq;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Store.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Trading;

[UsedImplicitly]
public sealed class TradingBoundUserInterface : BoundUserInterface
{
    private IPrototypeManager _prototypeManager = default!;

    [ViewVariables]
    private TradingMenu? _menu;

    [ViewVariables]
    private string _search = string.Empty;

    [ViewVariables]
    private HashSet<Guild> _guilds = new();

    public TradingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<TradingMenu>();
        BindMenuEvents();
    }

    private void BindMenuEvents()
    {
        if (_menu == null)
            return;

        _menu.OnGuildSelect += (guild) =>
        {
            _menu?.SelectTradingTab();
            _menu?.SelectGuild(guild);
        };

        _menu.OnItemButtonPressed += (_, item) =>
        {
            SendMessage(new TradingBuyMessage(item));
        };

        _menu.SearchTextUpdated += (_, search) =>
        {
            _search = search.Trim().ToLowerInvariant();
            UpdateCurrentGuildWithSearchFilter();
        };

        _menu.OnWithdrawAttempt += (_, type, amount) =>
        {
            SendMessage(new TradingRequestWithdrawMessage(amount));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case TradingUpdateState msg:
                _guilds = msg.Guilds;
                if (_menu == null)
                    return;

                _menu.User = msg.User;
                _menu.CurrencyPrototype = msg.Currency;
                UpdateCurrentGuild();

                _menu?.PopulateGuilds(_guilds);
                _menu?.UpdateBalance(msg.Balance);

                _menu?.SelectGuild();
                break;
        }
    }

    private void UpdateCurrentGuildWithSearchFilter()
    {
        if (_menu?.CurrentGuild == null)
            return;

        var guild = _menu.CurrentGuild!;
        _menu.UpdateItems(guild, _search);
    }

    private void UpdateCurrentGuild()
    {
        if (_menu?.CurrentGuild == null)
            return;

        _menu.CurrentGuild = _guilds.FirstOrDefault(g => g.Id == _menu.CurrentGuild.Id);
    }
}
