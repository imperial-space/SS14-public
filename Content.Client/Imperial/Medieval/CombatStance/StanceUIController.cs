using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.CombatStance;
using Content.Shared.Input;
using Content.Shared.Speech;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.CombatStance;

[UsedImplicitly]
public sealed class StanceUIController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    private SimpleRadialMenu? _menu;
    private RadialMenuOptionBase[] _models = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CombatStanceMenuEvent>((_, _) => ToggleStanceMenu(false));
        _models = new RadialMenuOptionBase[5];
        _models[4] = new RadialMenuNestedLayerOption(new RadialMenuOptionBase[2] {
            new RadialMenuActionOption<FactionMemberGroup>(PlacePressed, FactionMemberGroup.Alpha)
            {
                ToolTip = Loc.GetString("medieval-place-stancepoint"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "green"))
            },
            new RadialMenuActionOption<FactionMemberGroup>(RemovePressed, FactionMemberGroup.Alpha)
            {
                ToolTip = Loc.GetString("medieval-remove-stancepoints"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "red"))
            }
        })
        {
            ToolTip = Loc.GetString("medieval-place-alpha"),
            BackgroundColor = Color.FromHex("#794646")
        };
        _models[3] = new RadialMenuNestedLayerOption(new RadialMenuOptionBase[2] {
            new RadialMenuActionOption<FactionMemberGroup>(PlacePressed, FactionMemberGroup.Bravo)
            {
                ToolTip = Loc.GetString("medieval-place-stancepoint"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "green"))
            },
            new RadialMenuActionOption<FactionMemberGroup>(RemovePressed, FactionMemberGroup.Bravo)
            {
                ToolTip = Loc.GetString("medieval-remove-stancepoints"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "red"))
            }
        })
        {
            ToolTip = Loc.GetString("medieval-place-bravo"),
            BackgroundColor = Color.FromHex("#4E4679")
        };
        _models[2] = new RadialMenuNestedLayerOption(new RadialMenuOptionBase[2] {
            new RadialMenuActionOption<FactionMemberGroup>(PlacePressed, FactionMemberGroup.Delta)
            {
                ToolTip = Loc.GetString("medieval-place-stancepoint"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "green"))
            },
            new RadialMenuActionOption<FactionMemberGroup>(RemovePressed, FactionMemberGroup.Delta)
            {
                ToolTip = Loc.GetString("medieval-remove-stancepoints"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "red"))
            }
        })
        {
            ToolTip = Loc.GetString("medieval-place-delta"),
            BackgroundColor = Color.FromHex("#467953")
        };
        _models[1] = new RadialMenuNestedLayerOption(new RadialMenuOptionBase[2] {
            new RadialMenuActionOption<FactionMemberGroup>(PlacePressed, FactionMemberGroup.Gamma)
            {
                ToolTip = Loc.GetString("medieval-place-stancepoint"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "green"))
            },
            new RadialMenuActionOption<FactionMemberGroup>(RemovePressed, FactionMemberGroup.Gamma)
            {
                ToolTip = Loc.GetString("medieval-remove-stancepoints"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "red"))
            }
        })
        {
            ToolTip = Loc.GetString("medieval-place-gamma"),
            BackgroundColor = Color.FromHex("#797746")
        };
        _models[0] = new RadialMenuNestedLayerOption(new RadialMenuOptionBase[2] {
            new RadialMenuActionOption<FactionMemberGroup>(PlacePressed, FactionMemberGroup.Omega)
            {
                ToolTip = Loc.GetString("medieval-place-stancepoint"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "green"))
            },
            new RadialMenuActionOption<FactionMemberGroup>(RemovePressed, FactionMemberGroup.Omega)
            {
                ToolTip = Loc.GetString("medieval-remove-stancepoints"),
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "red"))
            }
        })
        {
            ToolTip = Loc.GetString("medieval-place-omega"),
            BackgroundColor = Color.FromHex("#6F4679")
        };
        _menu = new SimpleRadialMenu();
        _menu.OnClose += OnWindowClosed;
        _menu.OnOpen += OnWindowOpen;
        _net.RegisterNetMessage<CombatStancePointPlaceMessage>();
        _net.RegisterNetMessage<CombatStancePointRemoveMessage>();
    }
    private void PlacePressed(FactionMemberGroup group)
    {
        _placement.Clear();
        _placement.BeginPlacing(new Robust.Shared.Enums.PlacementInformation()
        {
            EntityType = "StancePoint",
            PlacementOption = "SnapgridCenter",
            IsTile = false,
            UseEditorContext = true,
            Uses = 1000
            //MobUid =
        }, new StancePointHijack(this, group));
    }
    public bool Placed(EntityCoordinates cords, FactionMemberGroup group)
    {
        _net.ClientSendMessage(new CombatStancePointPlaceMessage() { Coords = EntityManager.GetNetCoordinates(cords), Group = group });
        return true;
    }
    private void RemovePressed(FactionMemberGroup group)
    {
        _net.ClientSendMessage(new CombatStancePointRemoveMessage() { Group = group });
    }

    private void ToggleStanceMenu(bool centered)
    {
        if (!_menu!.IsOpen)
        {
            _menu.SetButtons(_models);
            _menu.Open();
            if (centered)
            {
                _menu.OpenCentered();
            }
            else
            {
                _menu.OpenOverMouseScreenPosition();
            }
        }
        else
        {
            CloseMenu();
        }
    }
    private void OnWindowClosed()
    {
        CloseMenu();
    }

    private void OnWindowOpen()
    {
    }

    private void CloseMenu()
    {
        _menu!.Close();
    }
}
