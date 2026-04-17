using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно выбора руны телепортации по названию.
/// </summary>
public sealed class CultTeleportSelectWindow : DefaultWindow
{
    public event Action<NetEntity>? OnRuneSelected;

    private readonly BoxContainer _vbox;

    public CultTeleportSelectWindow()
    {
        Title = Loc.GetString("cult-teleport-select-title");
        MinSize = new Vector2(260, 200);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        scroll.AddChild(_vbox);
        Contents.AddChild(scroll);
    }

    /// <summary>
    /// Обновляет список доступных рун телепортации.
    /// </summary>
    public void PopulateRunes(List<(NetEntity Entity, string Tag)> runes)
    {
        _vbox.RemoveAllChildren();

        if (runes.Count == 0)
        {
            var noRunes = new Label
            {
                Text = Loc.GetString("cult-teleport-no-rune"),
                HorizontalAlignment = HAlignment.Center,
            };
            _vbox.AddChild(noRunes);
            return;
        }

        foreach (var (entity, tag) in runes)
        {
            var capturedEntity = entity;
            var btn = new Button
            {
                Text = tag,
                HorizontalExpand = true,
                MinSize = new Vector2(0, 32),
            };
            btn.OnPressed += _ =>
            {
                OnRuneSelected?.Invoke(capturedEntity);
                Close();
            };
            _vbox.AddChild(btn);
        }
    }
}
