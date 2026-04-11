using System.Numerics;
using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно выбора заклинания кровавой магии.
/// </summary>
public sealed class CultBloodMagicSelectWindow : DefaultWindow
{
    public event Action<string>? OnSpellSelected;

    private static readonly string[] _spells =
    {
        "ActionCultStun",
        "ActionCultShackles",
        "ActionCultTeleport",
        "ActionCultEmp",
        "ActionCultTwistedConstruction",
        "ActionCultSummonDagger",
        "ActionCultSummonEquipment",
        "ActionCultConcealPresence",
        "ActionCultBloodRites",
    };

    public CultBloodMagicSelectWindow()
    {
        Title = Loc.GetString("cult-blood-magic-select-title");
        MinSize = new Vector2(280, 400);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        foreach (var spellId in _spells)
        {
            var capturedId = spellId;
            var locKey = CultSpellLocKeys.Mapping.TryGetValue(spellId, out var key)
                ? key
                : "cult-spell-unknown";
            var btn = new Button
            {
                Text = Loc.GetString(locKey),
                HorizontalExpand = true,
                MinSize = new Vector2(0, 32),
            };
            btn.OnPressed += _ =>
            {
                OnSpellSelected?.Invoke(capturedId);
                Close();
            };
            vbox.AddChild(btn);
        }

        scroll.AddChild(vbox);
        Contents.AddChild(scroll);
    }
}
