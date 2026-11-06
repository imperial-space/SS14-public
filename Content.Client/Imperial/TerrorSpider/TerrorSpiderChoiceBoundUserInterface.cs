using System.Numerics;
using Content.Shared.Imperial.TerrorSpider;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.TerrorSpider;

/// <summary>tgui_input_list / input() пауков Ужаса: тип яиц, размер кладки, цель обзора или кокона.</summary>
public sealed class TerrorSpiderChoiceBoundUserInterface : BoundUserInterface
{
    private TerrorSpiderChoiceWindow? _window;

    public TerrorSpiderChoiceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TerrorSpiderChoiceWindow>();
        _window.Chosen += (mode, key, count, percent) => SendMessage(new TerrorSpiderChoiceMessage(mode, key, count, percent));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is TerrorSpiderChoiceBoundUserInterfaceState choice)
            _window?.UpdateState(choice);
    }
}
