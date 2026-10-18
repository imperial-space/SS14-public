using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Contractor;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client.Imperial.Contractor;

public sealed partial class ContractorUplinkWindow : FancyWindow
{
    public event Action<string, ContractorDifficulty>? AcceptContract;
    public event Action<string>? DeclineContract;
    public event Action<string>? BuyRequisition;
    public event Action? OpenPortal;

    private readonly Label _repLabel;
    private readonly RichTextLabel _statusLabel;
    private readonly BoxContainer _activeContractContainer;
    private readonly BoxContainer _offersContainer;
    private readonly BoxContainer _requisitionContainer;

    public ContractorUplinkWindow()
    {
        Title = Loc.GetString("contractor-uplink-window-title");
        MinSize = new Vector2(720f, 560f);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        var top = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
        };

        _repLabel = new Label();
        _statusLabel = new RichTextLabel { HorizontalExpand = true };
        top.AddChild(_repLabel);
        top.AddChild(_statusLabel);
        root.AddChild(top);

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            VerticalExpand = true,
        };

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        left.AddChild(new Label { Text = Loc.GetString("contractor-uplink-active-title") });
        _activeContractContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        left.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            Children = { _activeContractContainer },
        });

        left.AddChild(new Label { Text = Loc.GetString("contractor-uplink-offers-title") });
        _offersContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        left.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            Children = { _offersContainer },
        });

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 240,
            MaxWidth = 240,
        };

        right.AddChild(new Label { Text = Loc.GetString("contractor-uplink-requisitions-title") });
        _requisitionContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        right.AddChild(_requisitionContainer);

        content.AddChild(left);
        content.AddChild(right);
        root.AddChild(content);

        ContentsContainer.AddChild(root);
    }

    public void UpdateState(ContractorUplinkBoundUserInterfaceState state)
    {
        _repLabel.Text = Loc.GetString("contractor-uplink-reputation", ("amount", state.Reputation));
        _statusLabel.SetMessage(FormattedMessage.FromMarkup(state.StatusText));

        RebuildActive(state);
        RebuildOffers(state);
        RebuildRequisitions(state);
    }

    private void RebuildActive(ContractorUplinkBoundUserInterfaceState state)
    {
        _activeContractContainer.RemoveAllChildren();

        if (!state.HasActiveContract)
        {
            _activeContractContainer.AddChild(new Label { Text = Loc.GetString("contractor-uplink-no-active") });
            return;
        }

        var active = state.ActiveContract;
        var box = BuildActiveContractPanel(active, false);

        var portalButton = new Button
        {
            Text = Loc.GetString("contractor-uplink-dispense-falsefire"),
            Disabled = !state.CanOpenPortal,
        };
        portalButton.OnPressed += _ => OpenPortal?.Invoke();
        box.AddChild(portalButton);

        _activeContractContainer.AddChild(new PanelContainer { Children = { box } });
    }

    private void RebuildOffers(ContractorUplinkBoundUserInterfaceState state)
    {
        _offersContainer.RemoveAllChildren();

        if (state.Offers.Length == 0)
        {
            _offersContainer.AddChild(new Label { Text = Loc.GetString("contractor-uplink-no-offers") });
            return;
        }

        foreach (var offer in state.Offers)
        {
            var box = BuildOfferPanel(offer, true, state.HasActiveContract);

            var buttons = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
            };

            var decline = new Button { Text = Loc.GetString("contractor-uplink-decline") };
            decline.OnPressed += _ => DeclineContract?.Invoke(offer.Id);

            buttons.AddChild(decline);
            box.AddChild(buttons);

            _offersContainer.AddChild(new PanelContainer { Children = { box } });
        }
    }

    private void RebuildRequisitions(ContractorUplinkBoundUserInterfaceState state)
    {
        _requisitionContainer.RemoveAllChildren();

        foreach (var requisition in state.Requisitions)
        {
            var panel = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 4,
            };

            panel.AddChild(new Label { Text = requisition.Name });
            var description = new RichTextLabel { MaxWidth = 220 };
            description.SetMessage(FormattedMessage.FromMarkupPermissive(requisition.Description));
            panel.AddChild(description);

            var button = new Button
            {
                Text = Loc.GetString("contractor-uplink-buy", ("cost", requisition.Cost)),
                Disabled = !requisition.Affordable,
            };
            button.OnPressed += _ => BuyRequisition?.Invoke(requisition.Id);
            panel.AddChild(button);

            _requisitionContainer.AddChild(new PanelContainer { Children = { panel } });
        }
    }

    private BoxContainer BuildActiveContractPanel(ContractorActiveContractData offer, bool includeReason)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        box.AddChild(new Label
        {
            Text = Loc.GetString("contractor-uplink-contract-header",
                ("target", offer.TargetName),
                ("job", offer.Job),
                ("difficulty", Loc.GetString($"contractor-difficulty-{offer.Difficulty.ToString().ToLowerInvariant()}"))),
        });
        box.AddChild(new Label { Text = Loc.GetString("contractor-uplink-location", ("location", offer.BeaconLabel)) });
        box.AddChild(new Label { Text = Loc.GetString("contractor-uplink-payout", ("alive", offer.AlivePayout), ("dead", offer.DeadPayout)) });

        if (includeReason)
        {
            var reason = new RichTextLabel { MaxWidth = 420 };
            reason.SetMessage(FormattedMessage.FromMarkupPermissive(offer.Reason));
            box.AddChild(reason);
        }

        return box;
    }

    private BoxContainer BuildOfferPanel(ContractorContractOfferData offer, bool includeReason, bool hasActiveContract)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        box.AddChild(new Label
        {
            Text = Loc.GetString("contractor-uplink-contract-target-header",
                ("target", offer.TargetName),
                ("job", offer.Job)),
        });

        foreach (var option in offer.Options)
        {
            var panel = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 2,
            };

            panel.AddChild(new Label
            {
                Text = Loc.GetString("contractor-uplink-difficulty-label",
                    ("difficulty", Loc.GetString($"contractor-difficulty-{option.Difficulty.ToString().ToLowerInvariant()}"))),
            });
            panel.AddChild(new Label { Text = Loc.GetString("contractor-uplink-location", ("location", option.BeaconLabel)) });
            panel.AddChild(new Label { Text = Loc.GetString("contractor-uplink-payout", ("alive", option.AlivePayout), ("dead", option.DeadPayout)) });

            if (includeReason)
            {
                var reason = new RichTextLabel { MaxWidth = 420 };
                reason.SetMessage(FormattedMessage.FromMarkupPermissive(option.Reason));
                panel.AddChild(reason);
            }

            var accept = new Button
            {
                Text = Loc.GetString("contractor-uplink-accept-difficulty",
                    ("difficulty", Loc.GetString($"contractor-difficulty-{option.Difficulty.ToString().ToLowerInvariant()}"))),
                Disabled = hasActiveContract,
            };
            accept.OnPressed += _ => AcceptContract?.Invoke(offer.Id, option.Difficulty);
            panel.AddChild(accept);

            box.AddChild(new PanelContainer { Children = { panel } });
        }

        return box;
    }
}