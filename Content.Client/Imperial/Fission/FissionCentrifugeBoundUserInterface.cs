using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Imperial.Fission;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Fission;

/// <summary>show_radial_menu центрифуги: выбор продукта обогащения.</summary>
[UsedImplicitly]
public sealed class FissionCentrifugeBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private Content.Client.UserInterface.Controls.SimpleRadialMenu? _menu;

    public FissionCentrifugeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<Content.Client.UserInterface.Controls.SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not FissionCentrifugeUiState centrifuge || _menu == null)
            return;

        var options = new List<Content.Client.UserInterface.Controls.RadialMenuOptionBase>();
        foreach (var result in centrifuge.Options)
        {
            var name = _proto.TryIndex<EntityPrototype>(result, out var proto) ? proto.Name : result;
            options.Add(new Content.Client.UserInterface.Controls.RadialMenuActionOption<string>(Pick, result)
            {
                IconSpecifier = Content.Client.UserInterface.Controls.RadialMenuIconSpecifier.With(new EntProtoId(result)),
                ToolTip = name,
            });
        }

        _menu.SetButtons(options);
    }

    private void Pick(string result)
    {
        SendMessage(new FissionCentrifugeChooseMessage(result));
        Close();
    }
}
