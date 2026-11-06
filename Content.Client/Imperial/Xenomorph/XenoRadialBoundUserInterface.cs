using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.StatusIcon.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Xenomorph;

/// <summary>Радиальное меню ксеноморфа: выбор смолы и касты при эволюции.</summary>
[UsedImplicitly]
public sealed class XenoRadialBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;

    public XenoRadialBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not XenoRadialState radial)
            return;

        var options = new List<RadialMenuOptionBase>();
        foreach (var option in radial.Options)
        {
            options.Add(new RadialMenuActionOption<string>(OnPicked, option.Id)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(option.Icon),
                ToolTip = option.Name,
            });
        }

        _menu.SetButtons(options);
    }

    private void OnPicked(string id)
    {
        SendMessage(new XenoRadialPickMessage(id));
        Close();
    }
}
