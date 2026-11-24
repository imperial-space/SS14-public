using System.Diagnostics.CodeAnalysis;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.UI.WindowPopOut;


/// <summary>
/// Opens <see cref="BaseWindow"/> instances in a separate OS window.
/// </summary>
public static class ImperialWindowPopOut
{
    public static bool PopOutEnabled = true;

    private static readonly AttachedProperty<PopoutState?> StateProperty = AttachedProperty<PopoutState?>.CreateNull("ImperialPopOutState", typeof(ImperialWindowPopOut));

    public static bool IsPoppedOut(BaseWindow window) =>
        TryGetState(window, out var state) && state.PopOutWindow != null;

    public static void Enable(BaseWindow window, Func<string?>? getTitle = null, TextureButton? button = null)
    {
        if (TryGetState(window, out _))
            return;

        var state = new PopoutState(window, getTitle);
        window.SetValue(StateProperty, state);

        state.Button = button ?? InsertPopOutButton(window);
        state.Button.OnPressed += _ => Toggle(window);
        window.OnClose += () => OnWindowClosed(window);
    }

    public static void Toggle(BaseWindow window)
    {
        if (IsPoppedOut(window))
            PopIn(window);
        else
            PopOut(window);
    }

    public static void PopOut(BaseWindow window)
    {
        if (!PopOutEnabled || !TryGetState(window, out var state) || state.PopOutWindow != null || !window.IsOpen)
            return;

        var clyde = IoCManager.Resolve<IClyde>();
        var uiManager = IoCManager.Resolve<IUserInterfaceManager>();
        state.PopInPosition = window.Position;

        var width = Math.Max((int)window.Size.X, (int)window.MinSize.X);
        var height = Math.Max((int)window.Size.Y, (int)window.MinSize.Y);

        state.PopOutWindow = clyde.CreateWindow(new WindowCreateParameters
        {
            Title = state.GetTitle?.Invoke() ?? "",
            Width = width,
            Height = height,
        });

        state.PopOutClosedHandler = _ => OnPopOutWindowRequestClosed(window);
        state.PopOutWindow.RequestClosed += state.PopOutClosedHandler;
        state.PopOutWindow.DisposeOnClose = false;

        window.Orphan();

        state.PopOutRoot = uiManager.CreateWindowRoot(state.PopOutWindow);
        state.PopOutRoot.AddChild(window);
        state.PopOutRoot.CreateRootControls();

        state.PopOutChildRemovedHandler = child => OnPopOutRootChildRemoved(window, child);
        state.PopOutRoot.OnChildRemoved += state.PopOutChildRemovedHandler;

        UpdateButton(window, state);
    }

    public static void PopIn(BaseWindow window)
    {
        if (!TryGetState(window, out var state) || state.PopOutWindow == null)
            return;

        DestroyPopOutWindow(state);
        window.Open(state.PopInPosition);
    }

    private static void OnWindowClosed(BaseWindow window)
    {
        if (!TryGetState(window, out var state) || state.PopOutWindow == null || state.CloseInProgress)
            return;

        DestroyPopOutWindow(state);
    }

    private static void OnPopOutRootChildRemoved(BaseWindow window, Control child)
    {
        if (child != window || !TryGetState(window, out var state) || state.CloseInProgress)
            return;

        DestroyPopOutWindow(state);
    }

    private static void OnPopOutWindowRequestClosed(BaseWindow window)
    {
        if (!TryGetState(window, out var state) || state.CloseInProgress)
            return;

        state.CloseInProgress = true;

        if (window.Parent != null)
            window.Close();

        if (state.PopOutWindow != null)
            DestroyPopOutWindow(state);

        state.CloseInProgress = false;
    }

    private static void DestroyPopOutWindow(PopoutState state)
    {
        var popOut = state.PopOutWindow;
        if (popOut == null)
            return;

        var popOutRoot = state.PopOutRoot;
        state.PopOutWindow = null;
        state.PopOutRoot = null;

        if (popOutRoot != null && state.PopOutChildRemovedHandler != null)
        {
            popOutRoot.OnChildRemoved -= state.PopOutChildRemovedHandler;
            state.PopOutChildRemovedHandler = null;
        }

        if (state.Window.Parent == popOutRoot)
            state.Window.Orphan();

        if (state.PopOutClosedHandler != null)
        {
            popOut.RequestClosed -= state.PopOutClosedHandler;
            state.PopOutClosedHandler = null;
        }

        var uiManager = IoCManager.Resolve<IUserInterfaceManager>();

        if (uiManager.GetWindowRoot(popOut) != null)
            uiManager.DestroyWindowRoot(popOut);

        popOut.Dispose();

        UpdateButton(state.Window, state);
    }

    private static bool TryGetState(BaseWindow window, [NotNullWhen(true)] out PopoutState? state)
    {
        state = window.GetValue(StateProperty);
        return state != null;
    }

    private static TextureButton InsertPopOutButton(BaseWindow window)
    {
        var closeButton = FindCloseButton(window)
            ?? throw new InvalidOperationException("Could not find window close button.");

        var parent = closeButton.Parent
            ?? throw new InvalidOperationException("Close button has no parent.");

        var popOutButton = new TextureButton
        {
            StyleClasses = { ImperialWindowPopOutStyles.StyleClassPopOutButton },
            VerticalAlignment = Control.VAlignment.Center,
        };

        parent.RemoveChild(closeButton);
        parent.AddChild(popOutButton);
        parent.AddChild(closeButton);

        return popOutButton;
    }

    private static TextureButton? FindCloseButton(Control control)
    {
        if (control is TextureButton { Name: "CloseButton" })
            return (TextureButton)control;

        foreach (var child in control.Children)
        {
            var found = FindCloseButton(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void UpdateButton(BaseWindow window, PopoutState state)
    {
        if (state.Button == null)
            return;

        state.Button.Visible = PopOutEnabled;

        if (!PopOutEnabled)
            return;

        if (state.PopOutWindow != null)
        {
            state.Button.SetOnlyStyleClass(ImperialWindowPopOutStyles.StyleClassPopInButton);
            state.Button.ToolTip = Loc.GetString("imperial-window-pop-in-tooltip");

            return;
        }

        state.Button.SetOnlyStyleClass(ImperialWindowPopOutStyles.StyleClassPopOutButton);
        state.Button.ToolTip = Loc.GetString("imperial-window-pop-out-tooltip");
    }
}
