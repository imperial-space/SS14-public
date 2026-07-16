using System.Numerics;
using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно замены заклинания — показывается когда все слоты заняты.
/// Игрок выбирает одно из уже подготовленных заклинаний для замены.
/// </summary>
public sealed class CultBloodMagicSwapWindow : DefaultWindow
{
    public event Action<string>? OnSwapSelected;

    public CultBloodMagicSwapWindow()
    {
        Title = Loc.GetString("cult-blood-magic-swap-title");
        MinSize = new Vector2(300, 200);
    }

    /// <summary>Заполняет окно кнопками подготовленных заклинаний.</summary>
    public void Populate(string newSpellId, List<string> preparedSpells)
    {
        Contents.RemoveAllChildren();

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        var label = new Label
        {
            Text = Loc.GetString("cult-blood-magic-swap-choose"),
            HorizontalExpand = true,
        };
        vbox.AddChild(label);

        foreach (var spellId in preparedSpells)
        {
            var displayName = CultSpellLocKeys.Mapping.TryGetValue(spellId, out var key)
                ? Loc.GetString(key)
                : spellId;

            var capturedId = spellId;
            var btn = new Button
            {
                Text = displayName,
                HorizontalExpand = true,
                MinSize = new Vector2(0, 32),
            };
            btn.OnPressed += _ =>
            {
                OnSwapSelected?.Invoke(capturedId);
                Close();
            };
            vbox.AddChild(btn);
        }

        Contents.AddChild(vbox);
    }
}
