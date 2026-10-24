using Robust.Client.UserInterface;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// Окно мини-игры рыбалки. Удерживайте левую кнопку мыши на полосе, чтобы тянуть приманку вверх
/// (правую — вниз, если стоит гиростабилизированный крючок).
/// </summary>
public sealed class ImperialFishingBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private ImperialFishingMinigameControl? _control;
    private bool _closedByServer;

    public ImperialFishingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _control = new ImperialFishingMinigameControl(EntMan.System<SpriteSystem>());
        _control.OnReel += state => SendMessage(new ImperialFishingReelMessage(state));

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(new Label { Text = Loc.GetString("imperial-fishing-minigame-hint"), HorizontalAlignment = Control.HAlignment.Center });
        box.AddChild(_control);

        _window = new DefaultWindow
        {
            Title = Loc.GetString("imperial-fishing-minigame-title"),
            Resizable = false,
        };
        _window.Contents.AddChild(box);
        _window.OnClose += () =>
        {
            if (!_closedByServer)
                SendMessage(new ImperialFishingGiveUpMessage());
            Close();
        };
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ImperialFishingMinigameState minigame)
            _control?.SetState(minigame);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _closedByServer = true;
        _window?.Close();
        _window = null;
    }
}
