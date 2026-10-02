using System.Numerics;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Client.UserInterface.Systems.Alerts.Widgets;
using Content.Shared.Imperial.Xenomorph;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Xenomorph;

/// <summary>
/// HUD ксеноморфа поверх алертов, как в SS13 (screen_objects/alien.dm):
/// plasma_display — запас плазмы пурпурным числом на иконке power_display,
/// alien_queen_finder — глазок finder_* в сторону королевы на иконке queen_finder.
/// </summary>
public sealed class XenomorphHudSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private const string PlasmaAlert = "XenoPlasma";
    private const string QueenFinderAlert = "XenoQueenFinder";
    private const string PlasmaLabelName = "XenoPlasmaDisplay";
    private const string FinderEyeName = "XenoQueenFinderEye";

    private static readonly ResPath HudRsi = new("/Textures/Imperial/Xenomorph/hud.rsi");

    /// <summary>MAPTEXT plasma_display: &lt;font color='magenta'&gt;.</summary>
    private static readonly Color PlasmaColor = Color.FromHex("#FF00FF");

    private static readonly string[] FinderStates = { "finder_center", "finder_near", "finder_med", "finder_far" };

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player
            || !TryComp<XenomorphComponent>(player, out var xeno)
            || _ui.GetActiveUIWidgetOrNull<AlertsUI>() is not { } alerts)
        {
            return;
        }

        TryComp<XenoPlasmaComponent>(player, out var plasma);
        UpdateControls(alerts, xeno, plasma);
    }

    private void UpdateControls(Control parent, XenomorphComponent xeno, XenoPlasmaComponent? plasma)
    {
        foreach (var child in parent.Children)
        {
            if (child is not AlertControl alert)
            {
                UpdateControls(child, xeno, plasma);
                continue;
            }

            switch (alert.Alert.ID)
            {
                case PlasmaAlert when plasma != null:
                    UpdatePlasmaDisplay(alert, plasma);
                    break;
                case QueenFinderAlert:
                    UpdateQueenFinder(alert, xeno);
                    break;
            }
        }
    }

    /// <summary>update_plasma_display: round(getPlasma()) по центру иконки, сдвиг на 6 пикселей вправо.</summary>
    private void UpdatePlasmaDisplay(AlertControl alert, XenoPlasmaComponent plasma)
    {
        if (FindChild(alert, PlasmaLabelName) is not Label label)
        {
            label = new Label
            {
                Name = PlasmaLabelName,
                HorizontalAlignment = Control.HAlignment.Left,
                VerticalAlignment = Control.VAlignment.Center,
                MinWidth = 64,
                Margin = new Thickness(12, 0, 0, 0),
                Align = Label.AlignMode.Center,
                FontColorOverride = PlasmaColor,
                MouseFilter = Control.MouseFilterMode.Ignore,
            };
            alert.AddChild(label);
        }

        var text = ((int) MathF.Round(plasma.Plasma)).ToString();
        if (label.Text != text)
            label.Text = text;
    }

    /// <summary>findQueen: finder_center/near/med/far в направлении королевы, пусто — королевы нет рядом.</summary>
    private void UpdateQueenFinder(AlertControl alert, XenomorphComponent xeno)
    {
        if (FindChild(alert, FinderEyeName) is not TextureRect eye)
        {
            eye = new TextureRect
            {
                Name = FinderEyeName,
                HorizontalAlignment = Control.HAlignment.Left,
                VerticalAlignment = Control.VAlignment.Top,
                SetSize = new Vector2(64, 64),
                TextureScale = new Vector2(2, 2),
                Stretch = TextureRect.StretchMode.KeepCentered,
                MouseFilter = Control.MouseFilterMode.Ignore,
            };
            alert.AddChild(eye);
        }

        if (xeno.QueenDistance < 0 || xeno.QueenDistance >= FinderStates.Length
            || !_resources.GetResource<RSIResource>(HudRsi).RSI.TryGetState(FinderStates[xeno.QueenDistance], out var state))
        {
            eye.Texture = null;
            return;
        }

        var direction = state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : ToRsiDirection(xeno.QueenDirection);
        var frame = 0;
        if (state.DelayCount > 1)
        {
            // finder_center мигает (delay 1 тик на кадр).
            var delay = Math.Max(state.GetDelays()[0], 0.05f);
            frame = (int) (_timing.RealTime.TotalSeconds / delay) % state.DelayCount;
        }

        eye.Texture = state.GetFrame(direction, frame);
    }

    private static RsiDirection ToRsiDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => RsiDirection.North,
            Direction.East => RsiDirection.East,
            Direction.West => RsiDirection.West,
            Direction.SouthEast => RsiDirection.SouthEast,
            Direction.SouthWest => RsiDirection.SouthWest,
            Direction.NorthEast => RsiDirection.NorthEast,
            Direction.NorthWest => RsiDirection.NorthWest,
            _ => RsiDirection.South,
        };
    }

    private static Control? FindChild(Control parent, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.Name == name)
                return child;
        }

        return null;
    }
}
