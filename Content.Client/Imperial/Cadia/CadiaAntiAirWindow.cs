using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Огневой пост зенитки: радар неба слева, этаж, лента и спуск справа.</summary>
public sealed class CadiaAntiAirWindow : DefaultWindow
{
    public event Action<Vector2>? OnAim;
    public event Action<int>? OnLevel;
    public event Action<bool>? OnFire;
    public event Action<NetEntity?>? OnView;

    private readonly BoxContainer _contacts = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Button _stopView = new();

    private readonly CadiaSkyRadarControl _radar = new();
    private readonly Label _gun = new() { FontColorOverride = Color.FromHex("#e8c66a") };
    private readonly Label _status = new();
    private readonly Label _ammo = new();
    private readonly ProgressBar _ammoBar = new() { MinHeight = 14, MinValue = 0, MaxValue = 1 };
    private readonly Label _auspex = new();
    private readonly Button _levelAir = new() { ToggleMode = true };
    private readonly Button _levelOrbit = new() { ToggleMode = true };
    private readonly Button _fire = new() { MinHeight = 64 };
    private readonly RichTextLabel _hint = new();

    private CadiaAntiAirBuiState? _state;
    private string _contactsKey = string.Empty;
    private DateTime _contactsRebuild;

    public CadiaAntiAirWindow()
    {
        MinSize = SetSize = new Vector2(1180, 620);
        _stopView.Text = Loc.GetString("cadia-aa-ui-stop-view");
        _stopView.OnPressed += _ => OnView?.Invoke(null);

        _radar.HorizontalExpand = true;
        _radar.VerticalExpand = true;
        _radar.OnAim += offset => OnAim?.Invoke(offset);

        _levelAir.Text = Loc.GetString("cadia-aa-ui-level-air");
        _levelOrbit.Text = Loc.GetString("cadia-aa-ui-level-orbit");
        _levelAir.OnPressed += _ => OnLevel?.Invoke(1);
        _levelOrbit.OnPressed += _ => OnLevel?.Invoke(2);

        // Спуск: стреляет, пока кнопка зажата.
        _fire.Text = Loc.GetString("cadia-aa-ui-fire");
        _fire.StyleClasses.Add("Caution");
        _fire.OnButtonDown += _ => OnFire?.Invoke(true);
        _fire.OnButtonUp += _ => OnFire?.Invoke(false);

        _hint.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString("cadia-aa-ui-hint")), defaultColor: Color.FromHex("#9a9a8a"));

        var levels = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        _levelAir.HorizontalExpand = true;
        _levelOrbit.HorizontalExpand = true;
        levels.AddChild(_levelAir);
        levels.AddChild(_levelOrbit);

        var side = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 260,
            MaxWidth = 260,
            SeparationOverride = 8,
            Margin = new Thickness(8, 0, 0, 0),
        };
        side.AddChild(_gun);
        side.AddChild(_status);
        side.AddChild(new Label { Text = Loc.GetString("cadia-aa-ui-level"), FontColorOverride = Color.FromHex("#9a9a8a") });
        side.AddChild(levels);
        side.AddChild(_ammo);
        side.AddChild(_ammoBar);
        side.AddChild(_auspex);
        side.AddChild(new Control { VerticalExpand = true });
        side.AddChild(_hint);
        side.AddChild(_fire);

        var frame = new PanelContainer
        {
            MinSize = new Vector2(440, 440),
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#040805"), BorderColor = Color.FromHex("#3d6b45"), BorderThickness = new Thickness(2) },
        };
        frame.AddChild(_radar);

        // Список бортов на радаре: силуэт, имя, этаж и дальность. Клик — камера на борт.
        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 240, MaxWidth = 240, SeparationOverride = 6, Margin = new Thickness(8, 0, 0, 0) };
        list.AddChild(new Label { Text = Loc.GetString("cadia-aa-ui-contacts"), FontColorOverride = Color.FromHex("#e8c66a") });
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_contacts);
        list.AddChild(scroll);
        list.AddChild(_stopView);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6) };
        root.AddChild(frame);
        root.AddChild(list);
        root.AddChild(side);
        Contents.AddChild(root);
    }

    public void Update(CadiaAntiAirBuiState state)
    {
        _state = state;
        _radar.SetState(state);

        _gun.Text = state.Linked ? state.GunName : Loc.GetString("cadia-aa-ui-no-gun");
        var aligned = Math.Abs(Angle.ShortestDistance(state.Aim, state.TargetAim).Degrees) <= 4;
        _status.Text = !state.Linked ? string.Empty
            : state.AimPoint == null ? Loc.GetString("cadia-aa-ui-no-aim")
            : aligned ? Loc.GetString("cadia-aa-ui-on-target") : Loc.GetString("cadia-aa-ui-traversing");
        _status.FontColorOverride = aligned && state.AimPoint != null ? Color.FromHex("#6dff8a") : Color.FromHex("#ff9a3c");

        _levelAir.Pressed = state.AimLevel == 1;
        _levelOrbit.Pressed = state.AimLevel == 2;
        _levelOrbit.Disabled = state.Levels < 2;

        _ammo.Text = Loc.GetString("cadia-aa-ui-ammo", ("ammo", state.Ammo), ("max", state.MaxAmmo));
        _ammoBar.Value = state.MaxAmmo == 0 ? 0 : (float) state.Ammo / state.MaxAmmo;
        _auspex.Text = Loc.GetString(state.AuspexLinked ? "cadia-aa-ui-auspex-on" : "cadia-aa-ui-auspex-off");
        _auspex.FontColorOverride = state.AuspexLinked ? Color.FromHex("#6dff8a") : Color.FromHex("#9a9a8a");

        _fire.Disabled = !state.Linked || state.AimPoint == null || state.Ammo <= 0 || state.OutOfRange;
        if (state.OutOfRange)
        {
            _status.Text = Loc.GetString("cadia-aa-ui-out-of-range");
            _status.FontColorOverride = Color.FromHex("#ff5a4a");
        }

        _stopView.Visible = state.Viewing != null;
        var key = string.Join(";", state.Contacts.Select(c => $"{c.Grid}:{c.Level}:{c.Kind}")) + $"|{state.Viewing}|{state.AimGrid}";
        if (key == _contactsKey && DateTime.UtcNow < _contactsRebuild)
            return;
        _contactsKey = key;
        _contactsRebuild = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        _contacts.RemoveAllChildren();
        if (state.Contacts.Count == 0)
            _contacts.AddChild(new Label { Text = Loc.GetString("cadia-aa-ui-sky-clear"), FontColorOverride = Color.FromHex("#9a9a8a") });
        foreach (var contact in state.Contacts)
            _contacts.AddChild(ContactButton(contact, state));
    }

    private Control ContactButton(CadiaAirContact contact, CadiaAntiAirBuiState state)
    {
        var color = contact.Kind switch
        {
            CadiaAirContactKind.Enemy => Color.FromHex("#ff3a2a"),
            CadiaAirContactKind.Friendly => Color.FromHex("#5aff78"),
            _ => Color.FromHex("#9a9a8a"),
        };
        var button = new Button { ToggleMode = true, Pressed = state.Viewing == contact.Grid, MinHeight = 52 };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new CadiaShipSilhouette(contact, color) { MinSize = new Vector2(44, 44) });
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        text.AddChild(new Label { Text = contact.Name, FontColorOverride = color, ClipText = true });
        var level = Loc.GetString(contact.Level == 1 ? "cadia-aa-ui-tag-air" : "cadia-aa-ui-tag-orbit");
        var locked = state.AimGrid == contact.Grid ? " • " + Loc.GetString("cadia-aa-ui-locked") : string.Empty;
        text.AddChild(new Label { Text = Loc.GetString("imperial-cadia-cadia-anti-air-window-1", ("arg0", level), ("arg1", ((int) contact.Offset.Length())), ("arg2", locked)), FontColorOverride = Color.FromHex("#b8c8b8") });
        row.AddChild(text);
        button.AddChild(row);
        button.OnPressed += _ => OnView?.Invoke(contact.Grid);
        return button;
    }

    public override void Close()
    {
        OnFire?.Invoke(false);
        base.Close();
    }
}
